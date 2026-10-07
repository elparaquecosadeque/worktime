using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Worktime.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without the host.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<WorktimeDbContext>
{
    public WorktimeDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<WorktimeDbContext>()
            .UseNpgsql("Host=localhost;Database=worktime;Username=worktime;Password=worktime")
            .UseSnakeCaseNamingConvention()
            .Options);
}
