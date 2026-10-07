using Microsoft.EntityFrameworkCore;
using Worktime.Domain.Assignments;
using Worktime.Domain.Common;
using Worktime.Domain.Permissions;
using Worktime.Domain.Punch;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Infrastructure.Persistence;

public sealed class WorktimeDbContext(DbContextOptions<WorktimeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<WorkLogEvent> WorkLogEvents => Set<WorkLogEvent>();
    public DbSet<PunchSession> PunchSessions => Set<PunchSession>();
    public DbSet<AssignmentRequest> AssignmentRequests => Set<AssignmentRequest>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Enums are stored as text: readable in psql, and the overlap constraint filters on 'Rejected'.
        b.Properties<Role>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<WorkLogStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<WorkLogSource>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<AssignmentStatus>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("btree_gist"); // lets the exclusion constraint compare uuids with '='
        b.ApplyConfigurationsFromAssembly(typeof(WorktimeDbContext).Assembly);

        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
            b.Entity(type.ClrType).Ignore(nameof(Entity.DomainEvents));
    }
}
