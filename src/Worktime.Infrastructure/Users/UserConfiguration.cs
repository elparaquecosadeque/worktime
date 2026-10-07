using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worktime.Domain.Users;

namespace Worktime.Infrastructure.Users;

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
