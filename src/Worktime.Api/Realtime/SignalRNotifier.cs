using Microsoft.AspNetCore.SignalR;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Api.Realtime;

public sealed class SignalRNotifier(IHubContext<WorktimeHub> hub) : IRealtimeNotifier
{
    public Task SendAsync(IReadOnlyCollection<string> groups, string eventName, object payload, CancellationToken ct) =>
        hub.Clients.Groups(groups.Distinct().ToList()).SendAsync(eventName, payload, ct);

    public Task BroadcastAsync(string eventName, object payload, CancellationToken ct) =>
        hub.Clients.All.SendAsync(eventName, payload, ct);
}
