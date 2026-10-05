using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Worktime.Api.Auth;
using Worktime.Application.Common;
using Worktime.Domain.Common;

namespace Worktime.Api.Middleware;

public sealed class DiagnosticsOptions
{
    public const string Section = "Diagnostics";
    public int SlowRequestMs { get; set; } = 500;
}

/// <summary>Reads or mints X-Correlation-Id and puts it in every log line of the request (JSON console scopes).</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers[Header].FirstOrDefault() is { Length: > 0 and <= 64 } incoming ? incoming : Guid.NewGuid().ToString("N");
        ctx.TraceIdentifier = id;
        ctx.Response.OnStarting(() => { ctx.Response.Headers[Header] = id; return Task.CompletedTask; });
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            await next(ctx);
    }
}

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

/// <summary>Every failure becomes a ProblemDetails with a stable <c>code</c> the client translates.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, code) = ex switch
        {
            ValidationException v => (StatusCodes.Status422UnprocessableEntity, v.Code),
            ForbiddenException { Code: "auth.invalid_credentials" } f => (StatusCodes.Status401Unauthorized, f.Code),
            ForbiddenException f => (StatusCodes.Status403Forbidden, f.Code),
            NotFoundException n => (StatusCodes.Status404NotFound, n.Code),
            ConcurrencyConflictException c => (StatusCodes.Status409Conflict, c.Code),
            DomainConflictException d => (StatusCodes.Status409Conflict, d.Code),
            DomainException d => (StatusCodes.Status422UnprocessableEntity, d.Code),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "request.malformed"),
            OperationCanceledException when ctx.RequestAborted.IsCancellationRequested => (499, "request.cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "server.error"),
        };
        if (status == 500) logger.LogError(ex, "Unhandled exception");

        ctx.Response.StatusCode = status;
        var details = new ProblemDetails { Status = status, Title = code, Detail = status == 500 ? null : ex.Message };
        details.Extensions["code"] = code;
        details.Extensions["correlationId"] = ctx.TraceIdentifier;
        if (ex is ValidationException ve) details.Extensions["codes"] = ve.Codes;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = details, Exception = ex });
    }
}
