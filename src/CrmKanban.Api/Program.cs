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
    // [ApiController] answers a model-binding failure (missing field, malformed JSON, a Guid that isn't
    // one) with its own ProblemDetails 400 — before any filter or middleware of ours runs. That body has
    // no `code`, so the SPA's envelope parser fell through to its "no response at all" branch and told
    // the user "Sunucuya ulaşılamadı" for what was really "you left a field blank". Same envelope as
    // ExceptionHandlingMiddleware, same shape as a FluentValidation failure, so the client has one
    // format to understand and one code to key off.
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
        o.InvalidModelStateResponseFactory = context => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
        {
            code = "validation.failed",
            message = "Gönderilen bilgiler geçersiz.",
            // Deliberately NOT forwarding ModelState's own text: it is English and half of it is
            // serializer internals ("The JSON value could not be converted to System.Guid. Path: $.userId").
            // The SPA renders `details` to the user, so the field name is all that travels.
            details = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .Select(e => new { field = e.Key, error = "Geçersiz veya eksik değer." }),
        }));

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
        // Login gets its own, looser window: it is the one anonymous endpoint real users hit repeatedly,
        // and a whole office behind one NAT address shares the partition key — 5/min would lock them out
        // every morning. 20/min per IP still turns password brute force from "unbounded" into pointless.
        // ponytail: per-IP only. Add per-account lockout (failed-attempt counter on User) if credential
        // stuffing from a botnet — many IPs, one account — actually shows up in the audit log.
        options.AddPolicy("login", httpContext =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
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
        //
        // BEST EFFORT, deliberately. Schema (migrate) and the real seed (permissions, statuses, super
        // admin) must succeed or the app is broken and should refuse to start. Demo data is a
        // convenience: if it throws, the site still has to come up. Learned the hard way — a bad
        // query in the demo seeder took the live site down with a 500.30, and the app itself was fine.
        if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Seed:Demo"))
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Demo seeding failed; continuing startup without it.");
            }
        }
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
    // ForwardLimit=1 (the default, set explicitly because the rate limiter depends on it): the middleware
    // walks X-Forwarded-For RIGHT to left and takes ONE entry — the one our own proxy appended. A client
    // that sends "X-Forwarded-For: 1.2.3.4" gets it pushed left by the proxy's append and ignored, so the
    // rate-limit partition key (Program.cs "public-form") can't be rotated per request to bypass the limit.
    // Raising this, or clearing it, re-opens that bypass. Verify with /health "ip" after any proxy change:
    // curl -H "X-Forwarded-For: 1.2.3.4" https://host/health must NOT echo 1.2.3.4.
    forwarded.ForwardLimit = 1;
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
    // index.html must be revalidated on every visit. Without an explicit Cache-Control the host sends
    // only ETag/Last-Modified, browsers fall back to heuristic caching, and a returning user keeps
    // running the PREVIOUS SPA bundle after a deploy (seen twice on 2026-08-13). The hashed assets
    // under /assets are content-addressed, so they stay freely cacheable — only the entry document
    // needs the round-trip, and a 304 makes that round-trip cheap. The same options go to the SPA
    // fallback below: that endpoint serves index.html too and does NOT inherit this middleware's.
    var spaFiles = new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
        },
    };
    app.UseDefaultFiles();
    app.UseStaticFiles(spaFiles);
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    // "ip" echoes the caller its OWN resolved address (no other client's data) so the X-Forwarded-For
    // trust chain above stays verifiable in prod — it is what the rate limiter partitions on.
    app.MapGet("/health", (HttpContext ctx) =>
        Results.Ok(new { status = "ok", ip = ctx.Connection.RemoteIpAddress?.ToString() })).AllowAnonymous();
    app.MapFallbackToFile("index.html", spaFiles); // SPA client-side routes (404 if no wwwroot, e.g. Docker API)

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
