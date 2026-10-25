using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Tenants;

namespace Worktime.Infrastructure.Tenants;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Name).HasMaxLength(200);
        b.Property(t => t.Slug).HasMaxLength(Tenant.MaxSlugLength);
        b.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tenants_slug");
    }
}
