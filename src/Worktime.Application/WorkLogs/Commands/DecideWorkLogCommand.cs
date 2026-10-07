using Mediator;
using Worktime.Application.Common;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs.Commands;

public sealed record DecideWorkLogCommand(Actor Actor, Guid WorkLogId, Decision Decision, string? Reason) : ICommand;

public sealed class DecideWorkLogHandler(WorkLogDecider decider) : ICommandHandler<DecideWorkLogCommand>
{
    public async ValueTask<Unit> Handle(DecideWorkLogCommand c, CancellationToken ct)
    {
        await decider.DecideAsync(c.Actor, c.WorkLogId, c.Decision, c.Reason, ct);
        return Unit.Value;
    }
}
