using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Worktime.Api.Auth;
using Worktime.Application.Common;
using Worktime.Application.Presence.Commands;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Api.Realtime;

[Authorize]
public sealed class WorktimeHub(IMediator mediator, WorktimeMetrics metrics) : Hub
{
    public const string Path = "/hubs/worktime";

    public override async Task OnConnectedAsync()
    {
        var actor = Context.User!.ToActor();
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(actor.Id));
        if (actor.Role == Role.Supervisor) await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Supervisor(actor.Id));
        if (actor.Has(Perms.MonitorAll)) await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Admins);

        metrics.ConnectionOpened();
        await mediator.Send(new ConnectedCommand(actor.Id, actor.Role), Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        metrics.ConnectionClosed();
        var actor = Context.User!.ToActor();
        await mediator.Send(new DisconnectedCommand(actor.Id, actor.Role), CancellationToken.None);
    }

    /// <summary>Clients call this every 30 s; presence expires 60 s after the last one.</summary>
    public async Task Heartbeat() => await mediator.Send(new HeartbeatCommand(Context.User!.UserId()), Context.ConnectionAborted);
}
