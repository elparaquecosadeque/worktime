using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Infrastructure.WorkLogs;

internal sealed class WorkLogEventConfiguration : IEntityTypeConfiguration<WorkLogEvent>
{
    public void Configure(EntityTypeBuilder<WorkLogEvent> b)
    {
        // Ids are generated client-side. Without this, EF treats an event appended to a tracked log as an
        // existing row (non-default key) and issues UPDATE instead of INSERT → 0 rows → false concurrency error.
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Reason).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
