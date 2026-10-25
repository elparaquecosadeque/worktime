using System.Text.RegularExpressions;
using Worktime.Domain.Common;

namespace Worktime.Domain.Tenants;

/// <summary>An organization. Its slug is its subdomain: <c>{slug}-worktime.gerardoleon.dev</c>.</summary>
public sealed partial class Tenant : Entity
{
    /// <summary>63 (DNS label) − 9 ("-worktime").</summary>
    public const int MaxSlugLength = 54;

    public static readonly IReadOnlySet<string> ReservedSlugs = new HashSet<string> { "www", "api", "app", "admin", "billing", "metrics", "status", "mail" };

    private Tenant() { } // EF

    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public TenantStatus Status { get; private set; }
    /// <summary>Seeded and reset by DemoResetService; nothing else is ever wiped.</summary>
    public bool IsDemo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(Guid id, string name, string slug, TenantStatus status, bool isDemo, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("tenant.name_required", "Name is required.");
        slug = slug.Trim().ToLowerInvariant();
        if (!IsValidSlug(slug)) throw new DomainException("tenant.slug_invalid", "Slug must be 1-54 lowercase letters, digits or inner hyphens.");
        if (ReservedSlugs.Contains(slug)) throw new DomainException("tenant.slug_reserved", "This slug is reserved.");
        return new Tenant { Id = id, Name = name.Trim(), Slug = slug, Status = status, IsDemo = isDemo, CreatedAt = now };
    }

    public static bool IsValidSlug(string slug) => SlugPattern().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,52}[a-z0-9])?$")]
    private static partial Regex SlugPattern();
}
