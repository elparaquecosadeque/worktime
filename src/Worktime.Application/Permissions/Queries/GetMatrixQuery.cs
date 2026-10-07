using Mediator;
using Worktime.Application.Permissions.Interfaces;
using Worktime.Application.Permissions.Results;
using Worktime.Application.Users.Interfaces;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Permissions.Queries;

public sealed record GetMatrixQuery : IQuery<MatrixDto>;

public sealed class GetMatrixHandler(IRolePermissionRepository matrix, IUserRepository users) : IQueryHandler<GetMatrixQuery, MatrixDto>
{
    public async ValueTask<MatrixDto> Handle(GetMatrixQuery q, CancellationToken ct)
    {
        var granted = (await matrix.GetAllAsync(ct)).Select(e => (e.Role, e.Permission)).ToHashSet();
        var cells = Enum.GetValues<Role>()
            .SelectMany(r => Perms.All.Select(p => new MatrixCell(r, p, granted.Contains((r, p)), PermissionMatrix.Locked.Contains((r, p)))))
            .ToList();
        var counts = (await users.ListByRolesAsync(Enum.GetValues<Role>(), ct))
            .Where(u => u.IsActive)
            .GroupBy(u => u.Role)
            .ToDictionary(g => g.Key, g => g.Count());
        return new MatrixDto(Perms.All, cells, counts);
    }
}
