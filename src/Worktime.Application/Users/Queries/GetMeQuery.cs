using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;

namespace Worktime.Application.Users.Queries;

public sealed record GetMeQuery(Actor Actor) : IQuery<MeDto>;

public sealed class GetMeHandler(IUserReads reads) : IQueryHandler<GetMeQuery, MeDto>
{
    public async ValueTask<MeDto> Handle(GetMeQuery q, CancellationToken ct) =>
        await reads.GetMeAsync(q.Actor.Id, q.Actor.Permissions.Order().ToList(), ct) ?? throw new NotFoundException("user.not_found");
}
