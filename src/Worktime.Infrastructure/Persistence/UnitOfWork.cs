using Microsoft.EntityFrameworkCore;
using Npgsql;
using Worktime.Application.Common;
using Worktime.Domain.Common;
using Worktime.Domain.Common.Interfaces;

namespace Worktime.Infrastructure.Persistence;

public sealed class UnitOfWork(WorktimeDbContext db, IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    /// <summary>DB constraints are the real guarantees; their names map to stable client codes.</summary>
    public static readonly IReadOnlyDictionary<string, string> ConstraintCodes = new Dictionary<string, string>
    {
        ["ix_users_email"] = "user.email_taken",
        ["ix_punch_sessions_open"] = "punch.already_open",
        ["ix_assignment_requests_pending"] = "assignment.already_pending",
        ["ex_work_logs_no_overlap"] = "worklog.overlap",
    };

    private readonly List<IDomainEvent> _pending = [];

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        var raisers = db.ChangeTracker.Entries<Entity>().Select(e => e.Entity).Where(e => e.DomainEvents.Count > 0).ToList();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // xmin no longer matches: someone else committed first (possibly on another replica).
            throw new ConcurrencyConflictException("concurrency.stale");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation } pg)
        {
            throw new ConcurrencyConflictException(ConstraintCodes.GetValueOrDefault(pg.ConstraintName ?? "", "db.constraint"));
        }

        foreach (var entity in raisers)
        {
            _pending.AddRange(entity.DomainEvents);
            entity.ClearDomainEvents();
        }
        if (db.Database.CurrentTransaction is null) await FlushAsync(ct);
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) return await work();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        T result;
        try
        {
            result = await work();
            await tx.CommitAsync(ct);
        }
        catch
        {
            _pending.Clear(); // rolled back: nobody may hear about it
            throw;
        }
        await FlushAsync(ct);
        return result;
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (_pending.Count == 0) return;
        var events = _pending.ToList();
        _pending.Clear();
        await dispatcher.DispatchAsync(events, ct);
    }
}
