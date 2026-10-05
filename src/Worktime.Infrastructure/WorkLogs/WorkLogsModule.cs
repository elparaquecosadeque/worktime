using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.WorkLogs;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.WorkLogs;

public static class WorkLogsModule
{
    public static IServiceCollection AddWorkLogsModule(this IServiceCollection services) => services
        .AddScoped<IWorkLogRepository, WorkLogRepository>()
        .AddScoped<IWorkLogReads, WorkLogReads>();
}

internal sealed class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
{
    public void Configure(EntityTypeBuilder<WorkLog> b)
    {
        b.Property(l => l.Note).HasMaxLength(1000);
        b.Property(l => l.Version).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(l => l.WorkerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(l => l.History).WithOne().HasForeignKey(e => e.WorkLogId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(l => l.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(l => new { l.WorkerId, l.StartAt });
        b.HasIndex(l => l.Status);
        // The no-overlap EXCLUDE constraint lives in the migration: EF has no fluent API for it.
    }
}

internal sealed class WorkLogEventConfiguration : IEntityTypeConfiguration<WorkLogEvent>
{
    public void Configure(EntityTypeBuilder<WorkLogEvent> b)
    {
        // Ids are generated client-side. Without this, EF treats an event appended to a tracked log as an
        // existing row (non-default key) and issues UPDATE instead of INSERT → 0 rows → false concurrency error.
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Reason).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkLogRepository(WorktimeDbContext db) : IWorkLogRepository
{
    public Task<WorkLog?> GetAsync(Guid id, CancellationToken ct) =>
        db.WorkLogs.Include(l => l.History).SingleOrDefaultAsync(l => l.Id == id, ct);

    public void Add(WorkLog log) => db.WorkLogs.Add(log);
}

internal sealed class WorkLogReads(WorktimeDbContext db) : IWorkLogReads
{
    private IQueryable<WorkLogDto> Project(IQueryable<WorkLog> logs) =>
        from l in logs
        join w in db.Users on l.WorkerId equals w.Id
        orderby l.StartAt
        select new WorkLogDto(
            l.Id, l.WorkerId, w.Name, w.TimeZoneId, l.StartAt, l.EndAt, l.Source, l.Note, l.Status,
            l.History.OrderBy(h => h.At).Select(h => new WorkLogEventDto(
                h.At, h.ActorId, db.Users.Where(a => a.Id == h.ActorId).Select(a => a.Name).First(), h.From, h.To, h.Reason)).ToList());

    public async Task<IReadOnlyList<WorkLogDto>> ListForWorkerAsync(Guid workerId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await Project(db.WorkLogs.AsNoTracking().Where(l => l.WorkerId == workerId && l.StartAt >= fromUtc && l.StartAt < toUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<WorkLogDto>> ListPendingAsync(Guid? supervisorId, bool orphansOnly, Guid? workerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct)
    {
        var workers = db.Users.Where(u => u.IsActive && u.Role == Role.Worker);
        if (supervisorId is not null) workers = workers.Where(u => u.SupervisorId == supervisorId);
        if (orphansOnly) workers = workers.Where(u => u.SupervisorId == null);
        if (workerId is not null) workers = workers.Where(u => u.Id == workerId);

        var logs = db.WorkLogs.AsNoTracking()
            .Where(l => l.Status == WorkLogStatus.Pending && workers.Any(w => w.Id == l.WorkerId));
        if (fromUtc is not null) logs = logs.Where(l => l.StartAt >= fromUtc);
        if (toUtc is not null) logs = logs.Where(l => l.StartAt < toUtc);
        return await Project(logs).ToListAsync(ct);
    }
}
