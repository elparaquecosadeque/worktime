using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Worktime.Application.Common;

/// <summary>Logs every use case with its duration; HTTP-level timing lives in the middleware.</summary>
public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(message, ct);
            logger.LogInformation("UseCase {UseCase} ok in {ElapsedMs:0.0}ms", typeof(TMessage).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("UseCase {UseCase} failed in {ElapsedMs:0.0}ms: {Error}", typeof(TMessage).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.GetType().Name);
            throw;
        }
    }
}
