using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Infrastructure.WorkLogs;

internal sealed class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
{
    public void Configure(EntityTypeBuilder<WorkLog> b)
    {
        b.Property(l => l.Note).HasMaxLength(1000);
        b.Property(l => l.Version).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(l => l.WorkerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(l => l.History).WithOne().HasForeignKey(e => e.WorkLogId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(l => l.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(l => new { l.WorkerId, l.StartAt });
        b.HasIndex(l => l.Status);
        // The no-overlap EXCLUDE constraint lives in the migration: EF has no fluent API for it.
    }
}
