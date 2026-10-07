using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Worktime.Application.Presence.Commands;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.BackgroundJobs;

/// <summary>Emits "offline" for users whose heartbeat expired (crashed replica, lost network).</summary>
public sealed class PresenceSweepService(IServiceScopeFactory scopes, ILogger<PresenceSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (!await LockKeys.TryXactLockAsync(db, LockKeys.PresenceSweep, ct)) continue;
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SweepPresenceCommand(), ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Presence sweep failed");
            }
        }
    }
}
