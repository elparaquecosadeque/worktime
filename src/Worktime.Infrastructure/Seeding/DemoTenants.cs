namespace Worktime.Infrastructure.Seeding;

/// <summary>
/// Fixed ids on purpose: billing (Fase 2+) keeps one Stripe customer per demo tenant across resets.
/// Both get the same seed shape; emails are unique per tenant, so the demo accounts repeat.
/// </summary>
public static class DemoTenants
{
    public static readonly Guid Demo = Guid.Parse("0d3e0000-0000-4000-8000-000000000001");
    public static readonly Guid Acme = Guid.Parse("0d3e0000-0000-4000-8000-000000000002");

    public static readonly IReadOnlyList<(Guid Id, string Name, string Slug)> All =
    [
        (Demo, "Demo", "demo"),
        (Acme, "Acme", "acme"),
    ];
}
