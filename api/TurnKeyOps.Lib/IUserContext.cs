
namespace MedInsights.Lib.Utils
{
    public interface IUserContext
    {
        bool IsAuthenticated { get; }
        Guid TenantId { get; }
        Guid UserId { get; }
        AppTimeZone Timezone { get; } 
        string FirstName { get; }
        string LastName { get; }

        //TODO: add roles in the future
        //IReadOnlyCollection<string> Roles { get; } // optional, if you want role checks here
        // bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
