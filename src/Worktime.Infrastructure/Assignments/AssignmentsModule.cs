using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Assignments.Results;
using Worktime.Domain.Assignments;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Assignments;

public static class AssignmentsModule
{
    public static IServiceCollection AddAssignmentsModule(this IServiceCollection services) => services
        .AddScoped<IAssignmentRequestRepository, AssignmentRequestRepository>()
        .AddScoped<IAssignmentReads, AssignmentReads>();
}

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

internal sealed class AssignmentRequestRepository(WorktimeDbContext db) : IAssignmentRequestRepository
{
    public async Task<AssignmentRequest?> GetAsync(Guid id, CancellationToken ct) => await db.AssignmentRequests.FindAsync([id], ct);

    public Task<AssignmentRequest?> GetPendingForWorkerAsync(Guid workerId, CancellationToken ct) =>
        db.AssignmentRequests.SingleOrDefaultAsync(r => r.WorkerId == workerId && r.Status == AssignmentStatus.Pending, ct);

    public void Add(AssignmentRequest request) => db.AssignmentRequests.Add(request);
}

internal sealed class AssignmentReads(WorktimeDbContext db) : IAssignmentReads
{
    public async Task<IReadOnlyList<AssignmentRequestDto>> ListPendingAsync(CancellationToken ct) =>
        await (from r in db.AssignmentRequests.AsNoTracking()
               join w in db.Users on r.WorkerId equals w.Id
               where r.Status == AssignmentStatus.Pending
               orderby r.CreatedAt
               select new AssignmentRequestDto(r.Id, r.WorkerId, w.Name, r.Note, r.PreferredSupervisorId,
                   db.Users.Where(s => s.Id == r.PreferredSupervisorId).Select(s => s.Name).FirstOrDefault(), r.CreatedAt))
            .ToListAsync(ct);
}
