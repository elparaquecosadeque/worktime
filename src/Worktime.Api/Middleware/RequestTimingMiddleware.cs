using System.Diagnostics;
using Microsoft.Extensions.Options;
using Worktime.Api.Auth;
using Worktime.Application.Common;

namespace Worktime.Api.Middleware;

/// <summary>Stopwatch around the rest of the pipeline: structured log, slow-request warning, histogram.</summary>
public sealed class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger, WorktimeMetrics metrics, IOptions<DiagnosticsOptions> options)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(ctx);
        }
        finally
        {
            var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            // Route template, not raw path: keeps metric cardinality bounded (no ids in labels).
            var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? ctx.Request.Path.Value ?? "";
            var status = ctx.Response.StatusCode;
            var userId = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.UserId().ToString() : null;
            metrics.Request(route, status, ms);

            var level = ms > options.Value.SlowRequestMs ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level, "HTTP {Method} {Route} → {Status} in {ElapsedMs:0.0}ms (user {UserId})", ctx.Request.Method, route, status, ms, userId);
        }
    }
}
