using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Domain.Common;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs;

/// <summary>
/// The double-approval defense, in layers:
/// 1. KeyedLock serializes racing decisions on the same log inside this instance (optimization).
/// 2. The worker row is read FOR SHARE, so an unassignment racing this decision cannot interleave.
/// 3. The domain refuses to decide a log that is no longer Pending.
/// 4. xmin on the log makes the loser's UPDATE affect zero rows across replicas → 409 (the guarantee).
/// </summary>
public sealed class WorkLogDecider(IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock, KeyedLock locks, WorktimeMetrics metrics)
{
    public async Task DecideAsync(Actor actor, Guid workLogId, Decision decision, string? reason, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(workLogId, ct);
        try
        {
            await uow.InTransactionAsync(async () =>
            {
                var log = await logs.GetAsync(workLogId, ct) ?? throw new NotFoundException("worklog.not_found");
                var worker = await users.GetForShareAsync(log.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
                if (!actor.Oversees(worker)) throw new ForbiddenException("worklog.not_your_worker");

                log.Decide(decision, actor.Id, reason, clock.UtcNow);
                await uow.SaveChangesAsync(ct);
                return true;
            }, ct);
            metrics.Decision(decision.ToString());
        }
        catch (ConcurrencyConflictException ex)
        {
            metrics.Conflict(ex.Code);
            throw;
        }
        catch (DomainConflictException ex)
        {
            metrics.Conflict(ex.Code);
            throw;
        }
    }
}
