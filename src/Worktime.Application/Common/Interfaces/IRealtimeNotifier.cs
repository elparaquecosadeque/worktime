namespace Worktime.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task SendAsync(IReadOnlyCollection<string> groups, string eventName, object payload, CancellationToken ct);
}
