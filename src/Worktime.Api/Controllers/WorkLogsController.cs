using Mediator;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.WorkLogs.Commands;
using Worktime.Application.WorkLogs.Queries;
using Worktime.Application.WorkLogs.Results;
using Worktime.Domain.Permissions;
using Worktime.Domain.WorkLogs;

namespace Worktime.Api.Controllers;

[ApiController, Route("api/worklogs")]
public sealed class WorkLogsController(IMediator mediator) : ControllerBase
{
    public sealed record WorkLogRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string? Note, string? Reason);
    public sealed record DecisionRequest(Decision Decision, string? Reason);
    public sealed record BatchDecisionRequest(IReadOnlyList<Guid> WorkLogIds, Decision Decision, string? Reason);

    [HttpGet("mine"), RequirePermission(Perms.WorkLogsSubmit)]
    public async Task<IReadOnlyList<WorkLogDto>> Mine(int year, int month, CancellationToken ct) =>
        await mediator.Send(new MyMonthQuery(User.ToActor(), year, month), ct);

    [HttpPost, RequirePermission(Perms.WorkLogsSubmit)]
    public async Task<object> Create(WorkLogRequest body, CancellationToken ct) =>
        new { id = await mediator.Send(new CreateManualWorkLogCommand(User.ToActor(), body.StartAt, body.EndAt, body.Note), ct) };

    [HttpPut("{id:guid}"), RequirePermission(Perms.WorkLogsSubmit)]
    public async Task<IActionResult> Edit(Guid id, WorkLogRequest body, CancellationToken ct)
    {
        await mediator.Send(new EditWorkLogCommand(User.ToActor(), id, body.StartAt, body.EndAt, body.Note, body.Reason), ct);
        return NoContent();
    }

    [HttpGet("inbox"), RequirePermission(Perms.WorkLogsApprove)]
    public async Task<IReadOnlyList<WorkLogDto>> Inbox(bool orphans, Guid? workerId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct) =>
        await mediator.Send(new InboxQuery(User.ToActor(), orphans, workerId, from, to), ct);

    [HttpPost("{id:guid}/decision"), RequirePermission(Perms.WorkLogsApprove)]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest body, CancellationToken ct)
    {
        await mediator.Send(new DecideWorkLogCommand(User.ToActor(), id, body.Decision, body.Reason), ct);
        return NoContent();
    }

    /// <summary>Partial success by design: 200 with one outcome per log ("7 approved, 3 already decided").</summary>
    [HttpPost("decisions"), RequirePermission(Perms.WorkLogsApprove)]
    public async Task<IReadOnlyList<BatchItemResult>> DecideBatch(BatchDecisionRequest body, CancellationToken ct) =>
        await mediator.Send(new DecideBatchCommand(User.ToActor(), body.WorkLogIds, body.Decision, body.Reason), ct);
}
