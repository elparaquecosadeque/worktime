using Mediator;
using Worktime.Application.Common;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Users;

public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    /// <summary>Reads the row with <c>FOR SHARE</c>: a concurrent reassignment waits until the caller's transaction ends.</summary>
    Task<User?> GetForShareAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<User>> ListByRolesAsync(IReadOnlyCollection<Role> roles, CancellationToken ct);
    void Add(User user);
}

public sealed record UserDto(Guid Id, string Name, string Email, Role Role, bool IsActive, Guid? SupervisorId, string? SupervisorName, string TimeZoneId);

public sealed record PendingRequestDto(Guid Id, DateTimeOffset CreatedAt, string? Note, Guid? PreferredSupervisorId);

public sealed record MeDto(UserDto User, IReadOnlyList<string> Permissions, DateTimeOffset? WorkingSince, PendingRequestDto? PendingRequest);

public interface IUserReads
{
    /// <param name="supervisorId">Only this supervisor's workers; null = everyone.</param>
    Task<IReadOnlyList<UserDto>> ListAsync(Guid? supervisorId, CancellationToken ct);
    Task<UserDto?> GetAsync(Guid id, CancellationToken ct);
    Task<MeDto?> GetMeAsync(Guid id, IReadOnlyList<string> permissions, CancellationToken ct);
}

// ---- Queries ----------------------------------------------------------------

public sealed record ListUsersQuery(Actor Actor) : IQuery<IReadOnlyList<UserDto>>;

public sealed class ListUsersHandler(IUserReads reads) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async ValueTask<IReadOnlyList<UserDto>> Handle(ListUsersQuery q, CancellationToken ct) =>
        await reads.ListAsync(q.Actor.Has(Perms.UsersManage) ? null : q.Actor.Id, ct);
}

public sealed record GetMeQuery(Actor Actor) : IQuery<MeDto>;

public sealed class GetMeHandler(IUserReads reads) : IQueryHandler<GetMeQuery, MeDto>
{
    public async ValueTask<MeDto> Handle(GetMeQuery q, CancellationToken ct) =>
        await reads.GetMeAsync(q.Actor.Id, q.Actor.Permissions.Order().ToList(), ct) ?? throw new NotFoundException("user.not_found");
}

// ---- Commands ---------------------------------------------------------------

/// <summary>Admins (users:manage) create workers or supervisors; supervisors (workers:manage) create workers assigned to themselves.</summary>
public sealed record CreateUserCommand(Actor Actor, string Name, string Email, string Password, Role Role, string? TimeZoneId)
    : ICommand<UserDto>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (Password is not { Length: >= 8 }) yield return "user.password_too_short";
        if (Role == Role.Admin) yield return "user.role_not_allowed";
    }
}

public sealed class CreateUserHandler(IUserRepository users, IUnitOfWork uow, IPasswordHasher hasher, IClock clock, IUserReads reads)
    : ICommandHandler<CreateUserCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(CreateUserCommand c, CancellationToken ct)
    {
        var global = c.Actor.Has(Perms.UsersManage);
        if (!global && c.Role != Role.Worker) throw new ForbiddenException("user.role_not_allowed");

        var user = User.Create(c.Name, c.Email, c.Role, hasher.Hash(c.Password), clock.UtcNow, c.TimeZoneId);
        if (!global)
            user.AssignTo(await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found"));

        users.Add(user);
        await uow.SaveChangesAsync(ct); // unique email index → user.email_taken
        return (await reads.GetAsync(user.Id, ct))!;
    }
}

public sealed record UpdateUserCommand(Actor Actor, Guid UserId, string Name, string Email, string TimeZoneId) : ICommand<UserDto>;

public sealed class UpdateUserHandler(UserScope scope, IUnitOfWork uow, IUserReads reads) : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(UpdateUserCommand c, CancellationToken ct)
    {
        var user = await scope.LoadManagedAsync(c.Actor, c.UserId, ct);
        user.UpdateProfile(c.Name, c.Email, c.TimeZoneId);
        await uow.SaveChangesAsync(ct);
        return (await reads.GetAsync(user.Id, ct))!;
    }
}

/// <summary>Users are never deleted: deactivation keeps logs and history, and kills live sessions via the stamp.</summary>
public sealed record SetUserActiveCommand(Actor Actor, Guid UserId, bool Active) : ICommand<UserDto>;

public sealed class SetUserActiveHandler(UserScope scope, IUnitOfWork uow, IUserReads reads) : ICommandHandler<SetUserActiveCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(SetUserActiveCommand c, CancellationToken ct)
    {
        if (c.UserId == c.Actor.Id) throw new ForbiddenException("user.self_deactivation");
        // Only global admins reactivate: a supervisor's deactivation is final from their side.
        if (c.Active && !c.Actor.Has(Perms.UsersManage)) throw new ForbiddenException("user.reactivate_admin_only");

        var user = await scope.LoadManagedAsync(c.Actor, c.UserId, ct);
        if (c.Active) user.Reactivate(); else user.Deactivate();
        await uow.SaveChangesAsync(ct);
        return (await reads.GetAsync(user.Id, ct))!;
    }
}

/// <summary>Admin-only (re)assignment. Also closes the worker's pending assignment request, if any.</summary>
public sealed record AssignWorkerCommand(Actor Actor, Guid WorkerId, Guid SupervisorId) : ICommand<UserDto>;

public sealed class AssignWorkerHandler(IUserRepository users, Assignments.IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock, IUserReads reads)
    : ICommandHandler<AssignWorkerCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(AssignWorkerCommand c, CancellationToken ct)
    {
        if (!c.Actor.Has(Perms.UsersManage)) throw new ForbiddenException("assignment.admin_only");
        var worker = await users.GetAsync(c.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
        var supervisor = await users.GetAsync(c.SupervisorId, ct) ?? throw new NotFoundException("user.not_found");

        worker.AssignTo(supervisor);
        if (await requests.GetPendingForWorkerAsync(worker.Id, ct) is { } pending)
            pending.Fulfill(c.Actor.Id, clock.UtcNow);

        await uow.SaveChangesAsync(ct);
        return (await reads.GetAsync(worker.Id, ct))!;
    }
}

/// <summary>
/// The supervisor lets a worker go (or an admin detaches them). The xmin token on the user row plus the
/// FOR SHARE read in approvals make "approve while being unassigned" resolve to exactly one order.
/// </summary>
public sealed record UnassignWorkerCommand(Actor Actor, Guid WorkerId) : ICommand<UserDto>;

public sealed class UnassignWorkerHandler(UserScope scope, IUnitOfWork uow, IUserReads reads) : ICommandHandler<UnassignWorkerCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(UnassignWorkerCommand c, CancellationToken ct)
    {
        var worker = await scope.LoadManagedAsync(c.Actor, c.WorkerId, ct);
        worker.Unassign();
        await uow.SaveChangesAsync(ct);
        return (await reads.GetAsync(worker.Id, ct))!;
    }
}

/// <summary>Who may manage whom: users:manage reaches everyone, workers:manage only one's own workers.</summary>
public sealed class UserScope(IUserRepository users)
{
    public async Task<User> LoadManagedAsync(Actor actor, Guid userId, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct) ?? throw new NotFoundException("user.not_found");
        if (actor.Has(Perms.UsersManage)) return user;
        if (actor.Has(Perms.WorkersManage) && user.Role == Role.Worker && user.SupervisorId == actor.Id) return user;
        throw new ForbiddenException("user.not_managed");
    }
}
