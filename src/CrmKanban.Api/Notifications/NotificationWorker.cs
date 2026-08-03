using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Notifications;
using CrmKanban.Infrastructure.Identity;
using CrmKanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Api.Notifications;

/// <summary>
/// Background worker that drains the notification pipeline off the request path (spec §14). Each tick
/// it builds a SYSTEM-scoped DbContext (like the seeder) so it sees every tenant's events, then runs
/// the fan-out + send passes. A failing tick is logged and retried next interval — the worker never
/// dies on a transient error.
/// </summary>
public sealed class NotificationWorker(
    IServiceProvider services,
    IEmailSender sender,
    IClock clock,
    IOptions<NotificationOptions> notifOptions,
    IOptions<CrmKanban.Application.AppOptions> appOptions,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private readonly NotificationOptions _opt = notifOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(_opt.PollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A scope per tick: DbContextOptions is scoped and its captured service provider must stay
                // alive while the context uses it, so the scope wraps the whole tick (resolving options from
                // a scope we then dispose would leave the context pointing at a dead provider). The context
                // itself is built with the System caller (like the seeder) so it sees every tenant's events.
                using var scope = services.CreateScope();
                var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<CrmDbContext>>();
                await using var db = new CrmDbContext(dbOptions, SystemCurrentUserService.System);
                var service = new NotificationService(db, sender, clock, notifOptions, appOptions);
                await service.RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Notification worker tick failed; will retry.");
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
