namespace Worktime.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string Section = "Seed";
    public bool Enabled { get; set; } = true;
    public TimeSpan ResetInterval { get; set; } = TimeSpan.FromHours(24);
    public string DemoPassword { get; set; } = "Demo1234!";
}
