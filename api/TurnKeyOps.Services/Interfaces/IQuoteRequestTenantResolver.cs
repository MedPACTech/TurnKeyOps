using TurnKeyOps.Lib.Configurations;

namespace TurnKeyOps.Services.Interfaces;

public interface IQuoteRequestTenantResolver
{
    QuoteRequestTenantDefinition Resolve(string tenantSlug);
}
