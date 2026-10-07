using Worktime.Domain.Common.Interfaces;

namespace Worktime.Application.Common.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
