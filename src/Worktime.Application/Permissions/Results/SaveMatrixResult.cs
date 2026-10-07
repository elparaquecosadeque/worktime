using Worktime.Domain.Users;

namespace Worktime.Application.Permissions.Results;

public sealed record SaveMatrixResult(IReadOnlyList<Role> ChangedRoles, int UsersSignedOut);
