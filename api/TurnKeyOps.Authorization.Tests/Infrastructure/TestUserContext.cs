using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;

namespace MedInsights.Authorization.Tests.Infrastructure;

internal sealed class TestUserContext : IUserContext
{
    public bool IsAuthenticated { get; init; } = true;
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public AppTimeZone Timezone { get; init; } = AppTimeZone.Utc;
    public string FirstName { get; init; } = "Test";
    public string LastName { get; init; } = "User";
}
