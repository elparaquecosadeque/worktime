using Microsoft.EntityFrameworkCore;
using Worktime.Application.Punch.Interfaces;
using Worktime.Domain.Punch;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Punch;

internal sealed class PunchRepository(WorktimeDbContext db) : IPunchRepository
{
    public Task<PunchSession?> GetOpenAsync(Guid workerId, CancellationToken ct) =>
        db.PunchSessions.SingleOrDefaultAsync(p => p.WorkerId == workerId && p.EndedAt == null, ct);

    public void Add(PunchSession session) => db.PunchSessions.Add(session);
}
