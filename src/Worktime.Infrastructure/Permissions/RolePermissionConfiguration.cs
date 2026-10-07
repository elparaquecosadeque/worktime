using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Permissions;

namespace Worktime.Infrastructure.Permissions;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.HasKey(e => new { e.Role, e.Permission });
        b.Property(e => e.Permission).HasMaxLength(64);
    }
}
