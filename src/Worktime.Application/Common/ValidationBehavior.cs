using Mediator;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Application.Common;

public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    {
        if (message is IValidatable v && v.Validate().ToList() is { Count: > 0 } errors)
            throw new ValidationException(errors);
        return next(message, ct);
    }
}
