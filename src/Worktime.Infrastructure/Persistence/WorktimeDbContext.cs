using Microsoft.EntityFrameworkCore;
using Worktime.Application.Common.Interfaces;
using Worktime.Domain.Assignments;
using Worktime.Domain.Common;
using Worktime.Domain.Permissions;
using Worktime.Domain.Punch;
using Worktime.Domain.Tenants;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Infrastructure.Persistence;

public sealed class WorktimeDbContext(DbContextOptions<WorktimeDbContext> options, ITenantContext tenant) : DbContext(options)
{
    /// <summary>Shadow property on every tenant-owned table: the domain never sees it, EF filters and stamps it.</summary>
    public const string TenantIdProperty = "TenantId";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<WorkLogEvent> WorkLogEvents => Set<WorkLogEvent>();
    public DbSet<PunchSession> PunchSessions => Set<PunchSession>();
    public DbSet<AssignmentRequest> AssignmentRequests => Set<AssignmentRequest>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>Read by the query filters on every execution (EF parameterizes context members), so a job may switch tenant.</summary>
    private Guid TenantId => tenant.Id;

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Enums are stored as text: readable in psql, and the overlap constraint filters on 'Rejected'.
        b.Properties<Role>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<WorkLogStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<WorkLogSource>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<AssignmentStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<TenantStatus>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("btree_gist"); // lets the exclusion constraint compare uuids with '='
        b.ApplyConfigurationsFromAssembly(typeof(WorktimeDbContext).Assembly);

        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
            b.Entity(type.ClrType).Ignore(nameof(Entity.DomainEvents));

        OwnedByTenant<User>(b);
        OwnedByTenant<WorkLog>(b);
        OwnedByTenant<WorkLogEvent>(b);
        OwnedByTenant<PunchSession>(b);
        OwnedByTenant<AssignmentRequest>(b);
        OwnedByTenant<RolePermission>(b);
    }

    /// <summary>
    /// Isolation for every EF query (FromSql included). Dapper bypasses it: each hand-written query filters
    /// <c>tenant_id</c> itself, and ReadsEquivalenceTests / TenantIsolationTests hold both adapters to it.
    /// </summary>
    private void OwnedByTenant<T>(ModelBuilder b) where T : class
    {
        b.Entity<T>().Property<Guid>(TenantIdProperty);
        b.Entity<T>().HasOne<Tenant>().WithMany().HasForeignKey(TenantIdProperty).OnDelete(DeleteBehavior.Cascade);
        b.Entity<T>().HasQueryFilter(e => EF.Property<Guid>(e, TenantIdProperty) == TenantId);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>New rows belong to the scope's tenant; inserting with no tenant in scope is a bug, never a default.</summary>
    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Metadata.FindProperty(TenantIdProperty) is not null))
        {
            var property = entry.Property(TenantIdProperty);
            if ((Guid)property.CurrentValue! != Guid.Empty) continue;
            if (TenantId == Guid.Empty) throw new InvalidOperationException($"Inserting {entry.Metadata.ClrType.Name} with no tenant in scope.");
            property.CurrentValue = TenantId;
        }
    }
}
