using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Punch;
using Worktime.Domain.Users;

namespace Worktime.Infrastructure.Punch;

internal sealed class PunchSessionConfiguration : IEntityTypeConfiguration<PunchSession>
{
    public void Configure(EntityTypeBuilder<PunchSession> b)
    {
        b.HasOne<User>().WithMany().HasForeignKey(p => p.WorkerId).OnDelete(DeleteBehavior.Restrict);
        // One open punch per worker: a double click or a second tab loses here, whatever the app did.
        b.HasIndex(p => p.WorkerId).IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName("ix_punch_sessions_open");
    }
}
