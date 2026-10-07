using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Assignments;
using Worktime.Domain.Users;

namespace Worktime.Infrastructure.Assignments;

internal sealed class AssignmentRequestConfiguration : IEntityTypeConfiguration<AssignmentRequest>
{
    public void Configure(EntityTypeBuilder<AssignmentRequest> b)
    {
        b.Property(r => r.Note).HasMaxLength(1000);
        b.Property(r => r.ResolutionReason).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(r => r.WorkerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(r => r.PreferredSupervisorId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(r => r.WorkerId).IsUnique().HasFilter("status = 'Pending'").HasDatabaseName("ix_assignment_requests_pending");
    }
}
