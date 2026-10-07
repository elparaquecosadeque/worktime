using Worktime.Domain.Users;

namespace Worktime.Application.Permissions.Results;

/// <param name="ActiveUsersByRole">Lets the client warn "saving forces N users to sign in again" before saving.</param>
public sealed record MatrixDto(IReadOnlyList<string> Permissions, IReadOnlyList<MatrixCell> Cells, IReadOnlyDictionary<Role, int> ActiveUsersByRole);
