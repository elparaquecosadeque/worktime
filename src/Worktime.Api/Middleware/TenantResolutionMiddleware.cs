using Microsoft.Extensions.Options;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Tenants.Interfaces;

namespace Worktime.Api.Middleware;

/// <summary>
/// Host → slug → tenant, before authentication (which then requires the token's tenant to match). An unknown slug or
/// the root host leave the scope without a tenant: tenant-owned data is invisible and /api/tenant answers 404.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenant, ITenantDirectory directory)
    {
        if (options.Value.SlugOf(ctx.Request.Host.Host) is { } slug && await directory.FindBySlugAsync(slug, ctx.RequestAborted) is { } found)
            tenant.Use(found.Id);
        await next(ctx);
    }
}
