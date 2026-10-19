namespace Worktime.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string Section = "Persistence";
    public ReadsAdapter Reads { get; set; } = ReadsAdapter.Ef;
}
