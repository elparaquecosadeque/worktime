using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Users.Commands;

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
