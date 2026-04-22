using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;

namespace MedInsights.Lib.Utils
{
    public sealed class UserContext : IUserContext
    {
        private readonly Guid? _tenantId;
        private readonly Guid? _userId;

        public bool IsAuthenticated { get; }
        public AppTimeZone Timezone { get; }

        public Guid TenantId => _tenantId
            ?? throw new UnauthorizedAccessException("TenantId is not available for anonymous requests.");

        public Guid UserId => _userId
            ?? throw new UnauthorizedAccessException("UserId is not available for anonymous requests.");

        public string FirstName { get; }
        public string LastName  { get; }

        private UserContext(
            bool isAuth,
            Guid? tenantId,
            Guid? userId,
            AppTimeZone tz,
            string? firstName,
            string? lastName)
        {
            IsAuthenticated = isAuth;
            _tenantId = tenantId;
            _userId = userId;
            Timezone = tz;
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
        }

        public static UserContext Anonymous(AppTimeZone tz = AppTimeZone.Utc)
            => new UserContext(false, null, null, tz, null, null);

        public static UserContext FromHttp(IHttpContextAccessor http)
        {
            var ctx = http.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
            var principal = ctx.User;
            var isAuth = principal?.Identity?.IsAuthenticated == true;

            var header = ctx.Request.Headers["X-Time-Zone"].ToString();
            var tzEnum = DateTimeHelper.ParseAppTimeZoneOrDefault(header, AppTimeZone.Utc);

            if (!isAuth)
            {
                return Anonymous(tzEnum);
            }

            // IBeam + JwtBearer can surface tenant in different claim types depending on mapping.
            var tenantId = GetRequiredGuidClaim(
                principal!,
                "tenant_id",
                "tenant",
                "tid",
                "http://schemas.microsoft.com/identity/claims/tenantid");
            var userId   = GetRequiredGuidClaim(principal!, ClaimTypes.NameIdentifier, "uid", JwtRegisteredClaimNames.Sub);

            // Prefer JWT registered names (what your CreateAsync emits)
            var firstName = GetOptionalStringClaim(
                principal!,
                JwtRegisteredClaimNames.GivenName,  // "given_name"
                ClaimTypes.GivenName,
                "first_name", "fname");

            var lastName = GetOptionalStringClaim(
                principal!,
                JwtRegisteredClaimNames.FamilyName, // "family_name"
                ClaimTypes.Surname,
                "last_name", "lname");

            return new UserContext(true, tenantId, userId, tzEnum, firstName, lastName);
        }

        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = _tenantId ?? Guid.Empty;
            return _tenantId.HasValue;
        }

        public bool TryGetUserId(out Guid userId)
        {
            userId = _userId ?? Guid.Empty;
            return _userId.HasValue;
        }

        private static Guid GetRequiredGuidClaim(ClaimsPrincipal principal, params string[] claimTypes)
        {
            foreach (var type in claimTypes)
            {
                // Some handlers map claims, some keep original names, some duplicate values.
                foreach (var claim in principal.FindAll(type))
                {
                    if (TryParseGuid(claim.Value, out var g))
                    {
                        return g;
                    }
                }
            }
            throw new UnauthorizedAccessException(
                $"Required GUID claim missing/invalid. Tried: {string.Join(", ", claimTypes)}");
        }

        private static bool TryParseGuid(string? input, out Guid guid)
        {
            guid = Guid.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var s = input.Trim().Trim('{', '}');
            if (Guid.TryParse(s, out guid) && guid != Guid.Empty)
            {
                return true;
            }

            // Defensive: support accidental JSON-array encoded claim values like ["guid","guid"].
            if (s.StartsWith("[", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            var candidate = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                            if (!string.IsNullOrWhiteSpace(candidate) &&
                                Guid.TryParse(candidate, out guid) &&
                                guid != Guid.Empty)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore malformed JSON and treat as non-GUID.
                }
            }

            guid = Guid.Empty;
            return false;
        }

        private static string? GetOptionalStringClaim(ClaimsPrincipal principal, params string[] claimTypes)
        {
            foreach (var type in claimTypes)
            {
                var raw = principal.FindFirst(type)?.Value;
                if (!string.IsNullOrWhiteSpace(raw))
                    return raw.Trim();
            }
            return null;
        }
    }
}
