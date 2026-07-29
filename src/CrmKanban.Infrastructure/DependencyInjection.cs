using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Domain.Entities;
using CrmKanban.Infrastructure.Identity;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Interceptors;
using CrmKanban.Infrastructure.Persistence.Seed;
using CrmKanban.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrmKanban.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<PasswordHasher<User>>();

        // Default caller = anonymous (no access). The API layer replaces this with an HttpContext-backed
        // service. Scoped so the DbContext's tenant filter reads per-request identity.
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();

        services.AddDbContext<CrmDbContext>((sp, options) =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName))
                   .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

        // Auth / authorization services
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.Configure<AuthOptions>(config.GetSection("Auth"));
        services.Configure<CrmKanban.Application.Tickets.TicketOptions>(config.GetSection("Tickets"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<IPermissionService, PermissionService>();

        services.AddSingleton(new SeederOptions(
            SuperAdminEmail: config["SuperAdmin:Email"] ?? Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL"),
            SuperAdminPassword: config["SuperAdmin:Password"] ?? Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD")));
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
