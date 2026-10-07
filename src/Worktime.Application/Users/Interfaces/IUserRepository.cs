using Worktime.Domain.Users;

namespace Worktime.Application.Users.Interfaces;

public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    /// <summary>Reads the row with <c>FOR SHARE</c>: a concurrent reassignment waits until the caller's transaction ends.</summary>
    Task<User?> GetForShareAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<User>> ListByRolesAsync(IReadOnlyCollection<Role> roles, CancellationToken ct);
    void Add(User user);
}
