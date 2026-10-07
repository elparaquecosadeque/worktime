using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;

namespace Worktime.Application.Users.Commands;

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
