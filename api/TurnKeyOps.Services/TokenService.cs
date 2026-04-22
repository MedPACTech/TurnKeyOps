// using MedInsights.Lib.Configurations;
// using MedPACTech.Identity;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Options;
// using Microsoft.IdentityModel.Tokens;
// using System;
// using System.Collections.Generic;
// using System.IdentityModel.Tokens.Jwt;
// using System.Linq;
// using System.Security.Claims;
// using System.Text;
// using System.Threading.Tasks;

// namespace MedInsights.Services
// {
//     public class MedInisightsTokenService : ITokenService
//     {
//         private readonly UserManager<ApplicationUser> _users;

//         private readonly JwtSettings _jwtSettings;

//         public MedInisightsTokenService(UserManager<ApplicationUser> users, IOptions<JwtSettings> jwtSettings)
//         {
//             _users = users;
//             _jwtSettings = jwtSettings.Value;
//         }

//         public async Task<string> CreateAsync(ApplicationUser user)
//         {
//             var roles = await _users.GetRolesAsync(user);
//             var userClaims = await _users.GetClaimsAsync(user);

//             var claims = new List<Claim>
//             {
//                 new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
//                 new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
//                 new(JwtRegisteredClaimNames.Iat,
//                     DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
//                     ClaimValueTypes.Integer64),

//                 new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
//                 new(ClaimTypes.NameIdentifier, user.Id.ToString()),
//                 new(ClaimTypes.Name, user.DisplayName ?? user.Email ?? string.Empty),

//                 new("tenant", user.TenantId?.ToString() ?? string.Empty),
//                 // security stamp for "logout all" invalidation
//                 new("ss", user.SecurityStamp ?? string.Empty),

//                 new(JwtRegisteredClaimNames.GivenName, user.FirstName ?? string.Empty),
//                 new(JwtRegisteredClaimNames.FamilyName, user.LastName ?? string.Empty)
//             };

//             claims.AddRange(userClaims);
//             claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

//             var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));

//             var rawKey = _jwtSettings.Key;
//             var keyHash8 = Convert.ToHexString(
//                 System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))
//             )[..8];
//             Console.WriteLine($"[TokenService] keyHash8={keyHash8}");

//             var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//             var token = new JwtSecurityToken(
//                 issuer: _jwtSettings.Issuer,
//                 audience: _jwtSettings.Audience,
//                 claims: claims,
//                 notBefore: DateTime.UtcNow,
//                 expires: DateTime.UtcNow.AddHours(12),
//                 signingCredentials: creds);

//             return new JwtSecurityTokenHandler().WriteToken(token);
//         }
//     }
// }


