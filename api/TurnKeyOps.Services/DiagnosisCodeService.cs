using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services
{
    public class DiagnosisCodeService : IDiagnosisCodeService
    {
        private const string CacheKey = "DiagnosisCodes:All";
        private static readonly SemaphoreSlim CacheLoadLock = new(1, 1);

        private readonly IDiagnosisCodeRepository _repository;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DiagnosisCodeService> _logger;

        public DiagnosisCodeService(
            IDiagnosisCodeRepository repository,
            IMemoryCache memoryCache,
            ILogger<DiagnosisCodeService> logger)
        {
            _repository = repository;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task WarmCacheAsync(CancellationToken ct = default)
        {
            _ = await GetAllCachedAsync(ct);
        }

        public async Task<DiagnosisCodeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var cachedData = await GetAllCachedAsync(ct);
            var result = cachedData.FirstOrDefault(x => x.Id == id);
            return result == null ? null : DiagnosisCodeMapper.ToDto(result);
        }

        public async Task<IReadOnlyList<DiagnosisCodeDto>> SearchAsync(string? searchInput, int limit = 50, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchInput))
                return Array.Empty<DiagnosisCodeDto>();

            var clampedLimit = Math.Clamp(limit, 1, 200);

            var terms = searchInput
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (terms.Count == 0)
                return Array.Empty<DiagnosisCodeDto>();

            var normalizedInput = Normalize(searchInput);
            var cachedData = await GetAllCachedAsync(ct);

            var results = cachedData
                .Select(code => new
                {
                    Code = code,
                    Combined = BuildSearchText(code),
                    NormalizedCode = Normalize(code.Code)
                })
                .Where(x => terms.All(term => x.Combined.Contains(term)))
                .OrderByDescending(x => Score(x.NormalizedCode, x.Combined, normalizedInput))
                .ThenBy(x => x.Code.Code, StringComparer.OrdinalIgnoreCase)
                .Take(clampedLimit)
                .Select(x => DiagnosisCodeMapper.ToDto(x.Code))
                .ToList();

            return results;
        }

        private async Task<IReadOnlyList<DiagnosisCode>> GetAllCachedAsync(CancellationToken ct)
        {
            if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<DiagnosisCode>? cachedData) &&
                cachedData is not null &&
                cachedData.Count > 0)
            {
                return cachedData;
            }

            await CacheLoadLock.WaitAsync(ct);
            try
            {
                if (_memoryCache.TryGetValue(CacheKey, out cachedData) &&
                    cachedData is not null &&
                    cachedData.Count > 0)
                {
                    return cachedData;
                }

                var loaded = await _repository.GetAllAsync(ct);
                var normalized = loaded
                    .Select(SetIdFromRowKeyIfMissing)
                    .Where(x => x.Id != Guid.Empty)
                    .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _memoryCache.Set(
                    CacheKey,
                    normalized,
                    new MemoryCacheEntryOptions
                    {
                        Priority = CacheItemPriority.NeverRemove
                    });

                _logger.LogInformation("Loaded {Count} diagnosis codes into cache.", normalized.Count);
                return normalized;
            }
            finally
            {
                CacheLoadLock.Release();
            }
        }

        private static DiagnosisCode SetIdFromRowKeyIfMissing(DiagnosisCode code)
        {
            if (code.Id == Guid.Empty && !string.IsNullOrWhiteSpace(code.RowKey) && Guid.TryParse(code.RowKey, out var parsed))
            {
                code.Id = parsed;
            }

            return code;
        }

        private static string BuildSearchText(DiagnosisCode code)
        {
            return $"{Normalize(code.Code)} {Normalize(code.ShortDescription)} {Normalize(code.LongDescription)}";
        }

        private static int Score(string normalizedCode, string combined, string normalizedInput)
        {
            if (string.Equals(normalizedCode, normalizedInput, StringComparison.Ordinal))
                return 300;

            if (normalizedCode.StartsWith(normalizedInput, StringComparison.Ordinal))
                return 200;

            if (normalizedCode.Contains(normalizedInput, StringComparison.Ordinal))
                return 150;

            if (combined.StartsWith(normalizedInput, StringComparison.Ordinal))
                return 100;

            return 50;
        }

        private static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return input
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Trim()
                .ToUpperInvariant();
        }
    }
}
