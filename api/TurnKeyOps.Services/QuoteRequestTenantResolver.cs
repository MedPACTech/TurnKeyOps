using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class QuoteRequestTenantResolver : IQuoteRequestTenantResolver
{
    private readonly QuoteRequestTenantOptions _options;

    public QuoteRequestTenantResolver(IOptions<QuoteRequestTenantOptions> options) =>
        _options = options.Value;

    public QuoteRequestTenantDefinition Resolve(string tenantSlug)
    {
        var slug = tenantSlug?.Trim() ?? string.Empty;
        if (!_options.Tenants.TryGetValue(slug, out var tenant) || tenant.TenantId == Guid.Empty)
            throw new ArgumentException("The quote request tenant is not configured.", nameof(tenantSlug));

        return tenant;
    }
}
