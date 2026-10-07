using Microsoft.EntityFrameworkCore;
using Worktime.Application.Users.Interfaces;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Users;

internal sealed class UserRepository(WorktimeDbContext db) : IUserRepository
{
    public async Task<User?> GetAsync(Guid id, CancellationToken ct) => await db.Users.FindAsync([id], ct);

    public Task<User?> GetForShareAsync(Guid id, CancellationToken ct) =>
        // xmin is a system column: "*" does not include it, and EF needs it as the concurrency token.
        db.Users.FromSql($"SELECT *, xmin FROM users WHERE id = {id} FOR SHARE").SingleOrDefaultAsync(ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> ListByRolesAsync(IReadOnlyCollection<Role> roles, CancellationToken ct) =>
        await db.Users.Where(u => roles.Contains(u.Role)).ToListAsync(ct);

    public void Add(User user) => db.Users.Add(user);
}
