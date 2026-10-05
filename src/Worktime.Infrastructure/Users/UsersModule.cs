using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Domain.Assignments;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services) => services
        .AddScoped<IUserRepository, UserRepository>()
        .AddScoped<IUserReads, UserReads>()
        .AddScoped<IPasswordHasher, PasswordHasherAdapter>();
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Name).HasMaxLength(200);
        b.Property(u => u.Email).HasMaxLength(320);
        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email");
        b.Property(u => u.SecurityStamp).HasMaxLength(64);
        b.Property(u => u.TimeZoneId).HasMaxLength(64);
        b.Property(u => u.Version).IsRowVersion(); // Npgsql maps uint row versions to the xmin system column
        b.HasOne<User>().WithMany().HasForeignKey(u => u.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(u => u.SupervisorId);
    }
}

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

internal sealed class UserReads(WorktimeDbContext db) : IUserReads
{
    private IQueryable<UserDto> Project(IQueryable<User> users) =>
        users.OrderBy(u => u.Role).ThenBy(u => u.Name).Select(u => new UserDto(
            u.Id, u.Name, u.Email, u.Role, u.IsActive, u.SupervisorId,
            db.Users.Where(s => s.Id == u.SupervisorId).Select(s => s.Name).FirstOrDefault(),
            u.TimeZoneId));

    public async Task<IReadOnlyList<UserDto>> ListAsync(Guid? supervisorId, CancellationToken ct) =>
        await Project(db.Users.AsNoTracking().Where(u => supervisorId == null || u.SupervisorId == supervisorId)).ToListAsync(ct);

    public Task<UserDto?> GetAsync(Guid id, CancellationToken ct) =>
        Project(db.Users.AsNoTracking().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<MeDto?> GetMeAsync(Guid id, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        if (await GetAsync(id, ct) is not { } user) return null;
        var workingSince = await db.PunchSessions.AsNoTracking()
            .Where(p => p.WorkerId == id && p.EndedAt == null).Select(p => (DateTimeOffset?)p.StartedAt).FirstOrDefaultAsync(ct);
        var pending = await db.AssignmentRequests.AsNoTracking()
            .Where(r => r.WorkerId == id && r.Status == AssignmentStatus.Pending)
            .Select(r => new PendingRequestDto(r.Id, r.CreatedAt, r.Note, r.PreferredSupervisorId))
            .FirstOrDefaultAsync(ct);
        return new MeDto(user, permissions, workingSince, pending);
    }
}

internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    // PBKDF2 with per-hash salt and automatic upgrade marker; we never roll our own crypto.
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> _inner = new();
    private static readonly object Subject = new();

    public string Hash(string password) => _inner.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(Subject, hash, password) != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
}
