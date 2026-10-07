namespace Worktime.Application.Team;

public sealed class CacheOptions
{
    public const string Section = "Cache";
    public int TeamSummarySeconds { get; set; } = 5;
}
