using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Punch;
using Worktime.Domain.Punch;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Punch;

public static class PunchModule
{
    public static IServiceCollection AddPunchModule(this IServiceCollection services) => services
        .AddScoped<IPunchRepository, PunchRepository>();
}

internal sealed class PunchSessionConfiguration : IEntityTypeConfiguration<PunchSession>
{
    public void Configure(EntityTypeBuilder<PunchSession> b)
    {
        b.HasOne<User>().WithMany().HasForeignKey(p => p.WorkerId).OnDelete(DeleteBehavior.Restrict);
        // One open punch per worker: a double click or a second tab loses here, whatever the app did.
        b.HasIndex(p => p.WorkerId).IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName("ix_punch_sessions_open");
    }
}

internal sealed class PunchRepository(WorktimeDbContext db) : IPunchRepository
{
    public Task<PunchSession?> GetOpenAsync(Guid workerId, CancellationToken ct) =>
        db.PunchSessions.SingleOrDefaultAsync(p => p.WorkerId == workerId && p.EndedAt == null, ct);

    public void Add(PunchSession session) => db.PunchSessions.Add(session);
}
