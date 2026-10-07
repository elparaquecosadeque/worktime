using Microsoft.EntityFrameworkCore;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.BackgroundJobs;

/// <summary>Postgres advisory-lock keys: one replica at a time does each job.</summary>
internal static class LockKeys
{
    public const long Migrate = 7_001;
    public const long DemoReset = 7_002;
    public const long PresenceSweep = 7_003;

    /// <summary>Transaction-scoped try-lock: released automatically on commit/rollback, never leaks.</summary>
    public static async Task<bool> TryXactLockAsync(WorktimeDbContext db, long key, CancellationToken ct) =>
        await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({key}) AS \"Value\"").SingleAsync(ct);
}
