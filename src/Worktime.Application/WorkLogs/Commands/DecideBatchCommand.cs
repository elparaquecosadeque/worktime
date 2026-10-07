using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.WorkLogs.Results;
using Worktime.Domain.Common;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs.Commands;

/// <summary>Each log is decided in its own transaction: one lost race never rolls back the others.</summary>
public sealed record DecideBatchCommand(Actor Actor, IReadOnlyList<Guid> WorkLogIds, Decision Decision, string? Reason)
    : ICommand<IReadOnlyList<BatchItemResult>>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (WorkLogIds is not { Count: > 0 and <= 100 }) yield return "worklog.batch_size";
    }
}

public sealed class DecideBatchHandler(IServiceScopeFactory scopes) : ICommandHandler<DecideBatchCommand, IReadOnlyList<BatchItemResult>>
{
    public async ValueTask<IReadOnlyList<BatchItemResult>> Handle(DecideBatchCommand c, CancellationToken ct)
    {
        var results = new List<BatchItemResult>(c.WorkLogIds.Count);
        foreach (var id in c.WorkLogIds.Distinct())
        {
            // A fresh scope (own DbContext) per item: a failed save must not leave stale tracked state for the next one.
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<WorkLogDecider>().DecideAsync(c.Actor, id, c.Decision, c.Reason, ct);
                results.Add(new(id, BatchOutcome.Ok, null));
            }
            catch (AppException ex)
            {
                results.Add(new(id, ex switch
                {
                    ConcurrencyConflictException => BatchOutcome.Conflict,
                    ForbiddenException => BatchOutcome.Forbidden,
                    NotFoundException => BatchOutcome.NotFound,
                    _ => BatchOutcome.Invalid,
                }, ex.Code));
            }
            catch (DomainException ex)
            {
                results.Add(new(id, ex is DomainConflictException ? BatchOutcome.Conflict : BatchOutcome.Invalid, ex.Code));
            }
        }
        return results;
    }
}
