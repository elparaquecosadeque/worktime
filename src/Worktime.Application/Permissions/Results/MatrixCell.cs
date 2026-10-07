using Worktime.Domain.Users;

namespace Worktime.Application.Permissions.Results;

public sealed record MatrixCell(Role Role, string Permission, bool Granted, bool Locked);
