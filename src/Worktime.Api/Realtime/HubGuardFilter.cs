using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Worktime.Api.Auth;
using Worktime.Application.Common.Interfaces;
using Worktime.Infrastructure.Redis;

namespace Worktime.Api.Realtime;

/// <summary>
/// The HTTP middleware only sees the WebSocket handshake; this times every hub invocation and re-checks
/// the security stamp, so a revoked user cannot keep using a socket opened before the revocation.
/// Each hub call runs in its own DI scope, which starts with no tenant: the filter sets it from the token
/// (validated against the Host at the handshake) for connect, every invocation and disconnect.
/// </summary>
public sealed class HubGuardFilter(StampValidator stamps, ILogger<HubGuardFilter> logger) : IHubFilter
{
    public Task OnConnectedAsync(HubLifetimeContext ctx, Func<HubLifetimeContext, Task> next)
    {
        UseTenantOf(ctx.Context, ctx.ServiceProvider);
        return next(ctx);
    }

    public Task OnDisconnectedAsync(HubLifetimeContext ctx, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        UseTenantOf(ctx.Context, ctx.ServiceProvider);
        return next(ctx, exception);
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext ctx, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = ctx.Context.User!;
        UseTenantOf(ctx.Context, ctx.ServiceProvider);
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

    private static void UseTenantOf(HubCallerContext caller, IServiceProvider services) =>
        services.GetRequiredService<ITenantContext>().Use(caller.User!.TenantId());
}
