using Worktime.Application.Users.Results;

namespace Worktime.Application.Users.Interfaces;

public interface IUserReads
{
    /// <param name="supervisorId">Only this supervisor's workers; null = everyone.</param>
    Task<IReadOnlyList<UserDto>> ListAsync(Guid? supervisorId, CancellationToken ct);
    Task<UserDto?> GetAsync(Guid id, CancellationToken ct);
    Task<MeDto?> GetMeAsync(Guid id, IReadOnlyList<string> permissions, CancellationToken ct);
}
