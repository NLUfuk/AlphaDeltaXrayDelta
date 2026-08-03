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
        services.Configure<CrmKanban.Application.AppOptions>(config.GetSection("App"));
        services.Configure<CrmKanban.Application.Tickets.TicketOptions>(config.GetSection("Tickets"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<IPermissionService, PermissionService>();

        // File storage + CAPTCHA gate + file limits (spec §10, §12, §13). Provider is a named variation
        // point (spec §3/§12): "local" = host disk (free single-instance prod/test, MonsterASP),
        // "azure" = Azure Blob, anything else = S3-compatible (dev MinIO / any S3 store). Three real
        // implementations behind IFileStorage, so the seam earns its keep (SCOPE DISCIPLINE).
        services.Configure<CrmKanban.Application.Files.FileOptions>(config.GetSection("Files"));
        switch ((config["Files:Provider"] ?? "s3").ToLowerInvariant())
        {
            case "local":
                services.Configure<Files.LocalStorageOptions>(config.GetSection(Files.LocalStorageOptions.SectionName));
                services.AddSingleton<IFileStorage, Files.LocalFileStorage>();
                break;
            case "azure":
                services.Configure<Files.AzureBlobOptions>(config.GetSection(Files.AzureBlobOptions.SectionName));
                services.AddSingleton(sp =>
                {
                    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Files.AzureBlobOptions>>().Value;
                    var container = new Azure.Storage.Blobs.BlobContainerClient(o.ConnectionString, o.ContainerName);
                    container.CreateIfNotExists(Azure.Storage.Blobs.Models.PublicAccessType.None); // private, idempotent
                    return container;
                });
                services.AddSingleton<IFileStorage, Files.AzureBlobStorage>();
                break;
            default: // "s3" / MinIO
                services.Configure<Files.S3Options>(config.GetSection(Files.S3Options.SectionName));
                services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
                {
                    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Files.S3Options>>().Value;
                    var cfg = new Amazon.S3.AmazonS3Config { ForcePathStyle = o.ForcePathStyle };
                    // AWSSDK v4 adds CRC checksum headers on PUT by default; non-AWS S3-compatible stores
                    // (MinIO, R2, B2) reject them. Only send/validate when required.
                    cfg.RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED;
                    cfg.ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED;
                    if (!string.IsNullOrWhiteSpace(o.ServiceUrl)) cfg.ServiceURL = o.ServiceUrl;
                    else cfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);
                    return new Amazon.S3.AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials(o.AccessKey, o.SecretKey), cfg);
                });
                services.AddSingleton<IFileStorage, Files.S3FileStorage>();
                break;
        }

        services.Configure<Captcha.CaptchaOptions>(config.GetSection(Captcha.CaptchaOptions.SectionName));
        services.AddSingleton<ICaptchaValidator, Captcha.CaptchaValidator>();

        // Notifications: email sender (log in dev, SMTP in prod) + background pipeline worker (spec §14)
        services.Configure<CrmKanban.Application.Notifications.NotificationOptions>(config.GetSection("Notifications"));
        services.Configure<Email.EmailOptions>(config.GetSection(Email.EmailOptions.SectionName));
        var emailProvider = config.GetSection(Email.EmailOptions.SectionName)["Provider"] ?? "log";
        if (string.Equals(emailProvider, "smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailSender, Email.SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, Email.DevLogEmailSender>();
        // The hosted worker that drains this pipeline lives at the composition root (API/Program.cs).

        services.AddSingleton(new SeederOptions(
            SuperAdminEmail: config["SuperAdmin:Email"] ?? Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL"),
            SuperAdminPassword: config["SuperAdmin:Password"] ?? Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD")));
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DevSeeder>();

        return services;
    }
}
