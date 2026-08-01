using System.Linq;
using CrmKanban.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmKanban.Api.Tests;

/// <summary>
/// Boots the real API in-process for HTTP-level smoke tests: full auth + validation + exception pipeline
/// + serialization, but over an InMemory database instead of SQL Server. Startup creates the schema via
/// EnsureCreated for the non-relational provider (see Program.cs), so no real DB is touched.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SuperAdminEmail = "root@test.local";
    public const string SuperAdminPassword = "Root!TestPass1";

    public ApiFactory()
    {
        // These must be present when the host reads configuration during startup — which happens BEFORE
        // WebApplicationFactory's ConfigureAppConfiguration would apply (Program reads Jwt at build time).
        // Process env vars are picked up by the default AddEnvironmentVariables source. Double underscore =
        // config section separator.
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-at-least-32-bytes-long-000000");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "crmkanban-test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "crmkanban-test");
        Environment.SetEnvironmentVariable("SuperAdmin__Email", SuperAdminEmail);
        Environment.SetEnvironmentVariable("SuperAdmin__Password", SuperAdminPassword);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // not Development → DevSeeder stays off

        builder.ConfigureServices(services =>
        {
            // Swap the SQL Server DbContext for a shared InMemory store. Remove every registration that
            // carries the SqlServer provider — the options, the context, AND the provider-config service
            // (IDbContextOptionsConfiguration<CrmDbContext>, EF Core 9+), or both providers end up
            // registered and EF refuses to start.
            var toRemove = services.Where(d =>
                (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false) ||
                d.ServiceType == typeof(CrmDbContext)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<CrmDbContext>(o => o.UseInMemoryDatabase("api-tests"));
        });
    }
}
