using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Worktime.Api.Auth;
using Worktime.Infrastructure.Redis;

namespace Worktime.Api.Realtime;

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
