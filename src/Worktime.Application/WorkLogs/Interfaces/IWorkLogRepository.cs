using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs.Interfaces;

public interface IWorkLogRepository
{
    Task<WorkLog?> GetAsync(Guid id, CancellationToken ct);
    void Add(WorkLog log);
}
