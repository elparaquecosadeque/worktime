using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Domain.Common;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs;

public interface IWorkLogRepository
{
    Task<WorkLog?> GetAsync(Guid id, CancellationToken ct);
    void Add(WorkLog log);
}

public sealed record WorkLogEventDto(DateTimeOffset At, Guid ActorId, string ActorName, WorkLogStatus? From, WorkLogStatus To, string? Reason);

public sealed record WorkLogDto(
    Guid Id, Guid WorkerId, string WorkerName, string WorkerTimeZoneId,
    DateTimeOffset StartAt, DateTimeOffset EndAt, WorkLogSource Source, string? Note, WorkLogStatus Status,
    IReadOnlyList<WorkLogEventDto> History);

public interface IWorkLogReads
{
    Task<IReadOnlyList<WorkLogDto>> ListForWorkerAsync(Guid workerId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <param name="supervisorId">Pending logs of this supervisor's current workers; null = every worker.</param>
    /// <param name="orphansOnly">Only workers without a supervisor (admin inbox).</param>
    Task<IReadOnlyList<WorkLogDto>> ListPendingAsync(Guid? supervisorId, bool orphansOnly, Guid? workerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct);
}

// ---- Queries ----------------------------------------------------------------

/// <summary>The worker's month, bounded in the worker's own time zone.</summary>
public sealed record MyMonthQuery(Actor Actor, int Year, int Month) : IQuery<IReadOnlyList<WorkLogDto>>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (Month is < 1 or > 12 || Year is < 2000 or > 2100) yield return "worklog.invalid_month";
    }
}

public sealed class MyMonthHandler(IUserRepository users, IWorkLogReads reads) : IQueryHandler<MyMonthQuery, IReadOnlyList<WorkLogDto>>
{
    public async ValueTask<IReadOnlyList<WorkLogDto>> Handle(MyMonthQuery q, CancellationToken ct)
    {
        var worker = await users.GetAsync(q.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");
        var (from, to) = MonthBounds(q.Year, q.Month, worker.TimeZone);
        return await reads.ListForWorkerAsync(worker.Id, from, to, ct);
    }

    public static (DateTimeOffset From, DateTimeOffset To) MonthBounds(int year, int month, TimeZoneInfo zone)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        return (new DateTimeOffset(start, zone.GetUtcOffset(start)), new DateTimeOffset(end, zone.GetUtcOffset(end)));
    }
}

public sealed record InboxQuery(Actor Actor, bool OrphansOnly, Guid? WorkerId, DateTimeOffset? From, DateTimeOffset? To) : IQuery<IReadOnlyList<WorkLogDto>>;

/// <summary>Never cached: this list feeds decisions.</summary>
public sealed class InboxHandler(IWorkLogReads reads) : IQueryHandler<InboxQuery, IReadOnlyList<WorkLogDto>>
{
    public async ValueTask<IReadOnlyList<WorkLogDto>> Handle(InboxQuery q, CancellationToken ct)
    {
        if (q.OrphansOnly && !q.Actor.SeesAllTeams) throw new ForbiddenException("worklog.orphans_admin_only");
        var supervisorId = q.Actor.SeesAllTeams ? (Guid?)null : q.Actor.Id;
        return await reads.ListPendingAsync(supervisorId, q.OrphansOnly, q.WorkerId, q.From, q.To, ct);
    }
}

// ---- Worker commands --------------------------------------------------------

public sealed record CreateManualWorkLogCommand(Actor Actor, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Note) : ICommand<Guid>;

public sealed class CreateManualWorkLogHandler(IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock)
    : ICommandHandler<CreateManualWorkLogCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateManualWorkLogCommand c, CancellationToken ct)
    {
        var worker = await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");
        var log = WorkLog.Manual(worker.Id, c.StartAt, c.EndAt, c.Note, clock.UtcNow, worker.TimeZone);
        logs.Add(log);
        await uow.SaveChangesAsync(ct); // exclusion constraint → worklog.overlap
        return log.Id;
    }
}

public sealed record EditWorkLogCommand(Actor Actor, Guid WorkLogId, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Note, string? Reason) : ICommand;

public sealed class EditWorkLogHandler(IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock, KeyedLock locks)
    : ICommandHandler<EditWorkLogCommand>
{
    public async ValueTask<Unit> Handle(EditWorkLogCommand c, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(c.WorkLogId, ct);
        var log = await logs.GetAsync(c.WorkLogId, ct) ?? throw new NotFoundException("worklog.not_found");
        var worker = await users.GetAsync(log.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
        log.Edit(c.Actor.Id, c.StartAt, c.EndAt, c.Note, c.Reason, clock.UtcNow, worker.TimeZone);
        await uow.SaveChangesAsync(ct); // a supervisor deciding meanwhile → xmin conflict
        return Unit.Value;
    }
}

// ---- Decisions --------------------------------------------------------------

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

public sealed record DecideWorkLogCommand(Actor Actor, Guid WorkLogId, Decision Decision, string? Reason) : ICommand;

public sealed class DecideWorkLogHandler(WorkLogDecider decider) : ICommandHandler<DecideWorkLogCommand>
{
    public async ValueTask<Unit> Handle(DecideWorkLogCommand c, CancellationToken ct)
    {
        await decider.DecideAsync(c.Actor, c.WorkLogId, c.Decision, c.Reason, ct);
        return Unit.Value;
    }
}

public enum BatchOutcome { Ok, Conflict, Forbidden, Invalid, NotFound }

public sealed record BatchItemResult(Guid WorkLogId, BatchOutcome Outcome, string? Code);

/// <summary>Each log is decided in its own transaction: one lost race never rolls back the others.</summary>
public sealed record DecideBatchCommand(Actor Actor, IReadOnlyList<Guid> WorkLogIds, Decision Decision, string? Reason)
    : ICommand<IReadOnlyList<BatchItemResult>>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (WorkLogIds is not { Count: > 0 and <= 100 }) yield return "worklog.batch_size";
    }
}

public sealed class DecideBatchHandler(IServiceScopeFactory scopes) : ICommandHandler<DecideBatchCommand, IReadOnlyList<BatchItemResult>>
{
    public async ValueTask<IReadOnlyList<BatchItemResult>> Handle(DecideBatchCommand c, CancellationToken ct)
    {
        var results = new List<BatchItemResult>(c.WorkLogIds.Count);
        foreach (var id in c.WorkLogIds.Distinct())
        {
            // A fresh scope (own DbContext) per item: a failed save must not leave stale tracked state for the next one.
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<WorkLogDecider>().DecideAsync(c.Actor, id, c.Decision, c.Reason, ct);
                results.Add(new(id, BatchOutcome.Ok, null));
            }
            catch (AppException ex)
            {
                results.Add(new(id, ex switch
                {
                    ConcurrencyConflictException => BatchOutcome.Conflict,
                    ForbiddenException => BatchOutcome.Forbidden,
                    NotFoundException => BatchOutcome.NotFound,
                    _ => BatchOutcome.Invalid,
                }, ex.Code));
            }
            catch (DomainException ex)
            {
                results.Add(new(id, ex is DomainConflictException ? BatchOutcome.Conflict : BatchOutcome.Invalid, ex.Code));
            }
        }
        return results;
    }
}
