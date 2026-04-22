// using MedPACTech.Identity;
// using Microsoft.Extensions.Caching.Distributed;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;

// namespace MedInsights.Lib
// {
//     public sealed class DistributedCacheTokenRevocationStore : ITokenRevocationStore
//     {
//         private readonly IDistributedCache _cache;
//         public DistributedCacheTokenRevocationStore(IDistributedCache cache) => _cache = cache;
//         private static string Key(string jti) => $"revoked:{jti}";

//         public async Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken ct = default)
//         {
//             var opts = new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt };
//             await _cache.SetStringAsync(Key(jti), "1", opts, ct);
//         }

//         public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default) =>
//             (await _cache.GetStringAsync(Key(jti), ct)) is not null;
//     }
// }
