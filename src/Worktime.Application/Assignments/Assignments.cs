using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Domain.Assignments;

namespace Worktime.Application.Assignments;

public interface IAssignmentRequestRepository
{
    Task<AssignmentRequest?> GetAsync(Guid id, CancellationToken ct);
    Task<AssignmentRequest?> GetPendingForWorkerAsync(Guid workerId, CancellationToken ct);
    void Add(AssignmentRequest request);
}

public sealed record AssignmentRequestDto(
    Guid Id, Guid WorkerId, string WorkerName, string? Note,
    Guid? PreferredSupervisorId, string? PreferredSupervisorName, DateTimeOffset CreatedAt);

public interface IAssignmentReads
{
    Task<IReadOnlyList<AssignmentRequestDto>> ListPendingAsync(CancellationToken ct);
}

public sealed record ListPendingAssignmentsQuery : IQuery<IReadOnlyList<AssignmentRequestDto>>;

public sealed class ListPendingAssignmentsHandler(IAssignmentReads reads) : IQueryHandler<ListPendingAssignmentsQuery, IReadOnlyList<AssignmentRequestDto>>
{
    public async ValueTask<IReadOnlyList<AssignmentRequestDto>> Handle(ListPendingAssignmentsQuery q, CancellationToken ct) =>
        await reads.ListPendingAsync(ct);
}

/// <summary>A second pending request loses on the partial unique index → assignment.already_pending (409).</summary>
public sealed record RequestAssignmentCommand(Actor Actor, string? Note, Guid? PreferredSupervisorId) : ICommand<Guid>;

public sealed class RequestAssignmentHandler(IUserRepository users, IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock)
    : ICommandHandler<RequestAssignmentCommand, Guid>
{
    public async ValueTask<Guid> Handle(RequestAssignmentCommand c, CancellationToken ct)
    {
        var worker = await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");
        var request = AssignmentRequest.Create(worker, c.Note, c.PreferredSupervisorId, clock.UtcNow);
        requests.Add(request);
        await uow.SaveChangesAsync(ct);
        return request.Id;
    }
}

public sealed record FulfillAssignmentCommand(Actor Actor, Guid RequestId, Guid SupervisorId) : ICommand;

public sealed class FulfillAssignmentHandler(IUserRepository users, IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock)
    : ICommandHandler<FulfillAssignmentCommand>
{
    public async ValueTask<Unit> Handle(FulfillAssignmentCommand c, CancellationToken ct)
    {
        var request = await requests.GetAsync(c.RequestId, ct) ?? throw new NotFoundException("assignment.not_found");
        var worker = await users.GetAsync(request.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
        var supervisor = await users.GetAsync(c.SupervisorId, ct) ?? throw new NotFoundException("user.not_found");

        worker.AssignTo(supervisor);
        request.Fulfill(c.Actor.Id, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record DismissAssignmentCommand(Actor Actor, Guid RequestId, string? Reason) : ICommand;

public sealed class DismissAssignmentHandler(IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock)
    : ICommandHandler<DismissAssignmentCommand>
{
    public async ValueTask<Unit> Handle(DismissAssignmentCommand c, CancellationToken ct)
    {
        var request = await requests.GetAsync(c.RequestId, ct) ?? throw new NotFoundException("assignment.not_found");
        request.Dismiss(c.Actor.Id, c.Reason, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
