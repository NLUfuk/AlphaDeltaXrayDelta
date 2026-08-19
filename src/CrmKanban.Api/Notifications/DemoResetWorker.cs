using CrmKanban.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Options;

namespace CrmKanban.Api.Notifications;

/// <summary>
/// Periodically returns the demo tenants to their seeded state (<see cref="DemoResetService"/>).
/// Registered only when <c>Seed:ResetHours</c> is greater than zero, so a real deployment that never
/// sets it carries no such worker at all.
///
/// <para>The first reset happens one interval AFTER startup, not at startup: the app already seeds on
/// boot, and an app pool that recycles (shared hosting does this often) would otherwise wipe the demo
/// several times a day at unpredictable moments — including in the middle of someone's demo.</para>
/// </summary>
public sealed class DemoResetWorker(
    IServiceProvider services,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoResetWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, demoOptions.Value.ResetHours));
        logger.LogInformation("Demo reset worker started; interval {Hours}h.", interval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DemoResetService>().ResetAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Same rule as the demo seed itself: this is a convenience, it must never take the site
                // down. A failed reset just means the tenants stay as they are until the next tick.
                logger.LogError(ex, "Demo reset failed; will retry next interval.");
            }
        }
    }
}
