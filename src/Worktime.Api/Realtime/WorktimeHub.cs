using System.Diagnostics;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Worktime.Api.Auth;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence.Commands;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Redis;

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

public sealed class SignalRNotifier(IHubContext<WorktimeHub> hub) : IRealtimeNotifier
{
    public Task SendAsync(IReadOnlyCollection<string> groups, string eventName, object payload, CancellationToken ct) =>
        hub.Clients.Groups(groups.Distinct().ToList()).SendAsync(eventName, payload, ct);

    public Task BroadcastAsync(string eventName, object payload, CancellationToken ct) =>
        hub.Clients.All.SendAsync(eventName, payload, ct);
}

/// <summary>
/// The HTTP middleware only sees the WebSocket handshake; this times every hub invocation and re-checks
/// the security stamp, so a revoked user cannot keep using a socket opened before the revocation.
/// </summary>
public sealed class HubGuardFilter(StampValidator stamps, ILogger<HubGuardFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext ctx, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = ctx.Context.User!;
        if (!await stamps.IsCurrentAsync(user.UserId(), user.FindFirst(WorktimeClaims.Stamp)?.Value ?? "", ctx.Context.ConnectionAborted))
        {
            ctx.Context.Abort();
            throw new HubException("auth.stale_session");
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            return await next(ctx);
        }
        finally
        {
            logger.LogDebug("Hub {Method} in {ElapsedMs:0.0}ms", ctx.HubMethodName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
