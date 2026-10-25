using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Permissions;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Permissions;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        // Each tenant owns its matrix.
        b.Property<Guid>(WorktimeDbContext.TenantIdProperty);
        b.HasKey(WorktimeDbContext.TenantIdProperty, nameof(RolePermission.Role), nameof(RolePermission.Permission));
        b.Property(e => e.Permission).HasMaxLength(64);
    }
}
