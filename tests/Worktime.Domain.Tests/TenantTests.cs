using Worktime.Domain.Common;
using Worktime.Domain.Tenants;

namespace Worktime.Domain.Tests;

public class TenantTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("acme")]
    [InlineData("a")]
    [InlineData("mi-empresa-2")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcd")] // 54
    public void Valid_slugs_are_dns_labels_that_fit_before_the_worktime_suffix(string slug) =>
        Assert.True(Tenant.IsValidSlug(slug));

    [Theory]
    [InlineData("")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("Acme")]
    [InlineData("mi_empresa")]
    [InlineData("acme.com")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcde")] // 55
    public void Anything_else_is_rejected(string slug) => Assert.False(Tenant.IsValidSlug(slug));

    [Fact]
    public void Create_normalizes_the_slug_and_refuses_reserved_ones()
    {
        Assert.Equal("acme", Tenant.Create(Guid.NewGuid(), "Acme", "  ACME ", TenantStatus.Active, false, Now).Slug);
        var ex = Assert.Throws<DomainException>(() => Tenant.Create(Guid.NewGuid(), "X", "billing", TenantStatus.Active, false, Now));
        Assert.Equal("tenant.slug_reserved", ex.Code);
    }
}
