using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;
using Worktime.Domain.Permissions;

namespace Worktime.Application.Users.Queries;

public sealed record ListUsersQuery(Actor Actor) : IQuery<IReadOnlyList<UserDto>>;

public sealed class ListUsersHandler(IUserReads reads) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async ValueTask<IReadOnlyList<UserDto>> Handle(ListUsersQuery q, CancellationToken ct) =>
        await reads.ListAsync(q.Actor.Has(Perms.UsersManage) ? null : q.Actor.Id, ct);
}
