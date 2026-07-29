using CrmKanban.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CrmKanban.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations` build the context without the full app host. Migrations don't run
/// query filters, so an anonymous current user is fine. Connection string comes from the
/// CRM_MIGRATIONS_CONNECTION env var or falls back to the local dev SQL Server.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CRM_MIGRATIONS_CONNECTION")
            ?? "Server=localhost;Database=CrmKanban;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CrmDbContext(options, new SystemCurrentUserService());
    }
}
