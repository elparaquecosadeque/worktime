using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;
using Worktime.Domain.Permissions;

namespace Worktime.Application.Users.Commands;

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
