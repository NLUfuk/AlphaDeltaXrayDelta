using System.Globalization;
using System.Text;
using CrmKanban.Api.Auth;
using CrmKanban.Api.Middleware;
using CrmKanban.Application;
using CrmKanban.Application.Abstractions;
using CrmKanban.Infrastructure;
using CrmKanban.Infrastructure.Identity;
using CrmKanban.Infrastructure.Persistence;
using CrmKanban.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
        .AddJsonOptions(o =>
        {
            // All timestamps are UTC; emit a 'Z' so the SPA can localize them (see UtcDateTimeConverter).
            o.JsonSerializerOptions.Converters.Add(new CrmKanban.Api.UtcDateTimeConverter());
            o.JsonSerializerOptions.Converters.Add(new CrmKanban.Api.NullableUtcDateTimeConverter());
        });
    builder.Services.AddOpenApi();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<CrmKanban.Api.Notifications.NotificationWorker>();

    // Per-request caller identity from the JWT (overrides the anonymous default from AddInfrastructure).
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // keep raw claim names ("sub", "company_id", ...)
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });
    builder.Services.AddAuthorization();

    // Rate limit the anonymous public form (spec §10) — 5 requests/min per client IP.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("public-form", httpContext =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    var app = builder.Build();

    // Apply migrations and run the idempotent seed on startup (dev-friendly; multi-instance prod
    // moves this to a one-shot migration step — see PROGRESS tech debt).
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        // Relational DB → apply migrations; a non-relational provider (InMemory, used by integration
        // tests) has no migrations, so just create the schema.
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        // Demo data: always in Development, and on demand elsewhere via Seed:Demo=true (e.g. a review
        // deployment where you want the kanban/reports populated). Off by default in production.
        if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Seed:Demo"))
            await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync();
    }

    // Behind a reverse proxy (IIS/ASP.NET Core Module on MonsterASP.NET, or nginx in Docker) so the
    // real client scheme/IP arrive in X-Forwarded-*; trust them (proxy identity isn't known on shared
    // hosting, so the known-list is cleared).
    var forwarded = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                         | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);

    // Request logging OUTSIDE the exception handler: the handler turns a domain exception into its real
    // status (401/403/404/400) before the log line is written. The other order let every domain exception
    // reach Serilog unhandled, so the log said "responded 500" while the client correctly got a 403 —
    // false 500 alarms in monitoring (tech debt #35).
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // Single-site hosting (spec §17.8, deploy): when the SPA build is copied into wwwroot (MonsterASP.NET),
    // ASP.NET serves it and falls back to index.html for client routes. In Docker nginx does this and
    // wwwroot is empty, so these are no-ops there. HTTPS redirect only when not already forwarded https
    // (behind a TLS-terminating proxy the X-Forwarded-Proto is already https → no redirect loop).
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
    app.MapFallbackToFile("index.html"); // SPA client-side routes (404 if no wwwroot, e.g. Docker API)

    app.Run();
}
// Don't swallow the sentinel the host-capture (WebApplicationFactory integration tests) throws to abort
// startup after building the host — it's an internal "StopTheHostException"/HostAbortedException. Catching
// it would leave the test server unstarted.
catch (Exception ex) when (ex.GetType().Name is not ("HostAbortedException" or "StopTheHostException"))
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
