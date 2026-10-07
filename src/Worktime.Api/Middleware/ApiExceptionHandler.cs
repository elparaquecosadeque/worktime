using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Worktime.Application.Common;
using Worktime.Domain.Common;

namespace Worktime.Api.Middleware;

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
