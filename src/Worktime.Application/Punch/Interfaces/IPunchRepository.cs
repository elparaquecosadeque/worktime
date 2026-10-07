using Worktime.Domain.Punch;

namespace Worktime.Application.Punch.Interfaces;

public interface IPunchRepository
{
    Task<PunchSession?> GetOpenAsync(Guid workerId, CancellationToken ct);
    void Add(PunchSession session);
}
