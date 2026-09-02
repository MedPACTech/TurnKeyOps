using System.Text.Json;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class TenantSettingsService : ITenantSettingsService
{
    private const int MaxPayloadBytes = 256 * 1024;
    private const int CurrentSchemaVersion = 1;
    private static readonly string[] SensitiveKeyFragments =
    [
        "secret", "password", "credential", "privatekey", "apikey", "accesskey", "token"
    ];

    private readonly ITenantSettingsRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IRoleAccessService _roleAccess;
    private readonly IAuditService _audit;

    public TenantSettingsService(
        ITenantSettingsRepository repository,
        IUserContext userContext,
        IRoleAccessService roleAccess,
        IAuditService audit)
    {
        _repository = repository;
        _userContext = userContext;
        _roleAccess = roleAccess;
        _audit = audit;
    }

    public async Task<TenantSettingsDocumentDto> GetPublicAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        }

        var entity = await _repository.GetAsync(
            Partition(tenantId),
            Row(TenantSettingKinds.PublicContent),
            ct);

        return entity is null || entity.IsDeleted
            ? CreateDefault(TenantSettingKinds.PublicContent, isPublic: true)
            : ToDto(entity, exposeConfiguredSecrets: false);
    }

    public async Task<TenantSettingsDocumentDto> GetProtectedAsync(string kind, CancellationToken ct = default)
    {
        kind = NormalizeProtectedKind(kind);
        await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.TenantSettingsRead, ct);

        var entity = await _repository.GetAsync(Partition(_userContext.TenantId), Row(kind), ct);
        return entity is null || entity.IsDeleted
            ? CreateDefault(kind, isPublic: false)
            : ToDto(entity, exposeConfiguredSecrets: true);
    }

    public async Task<TenantSettingsDocumentDto> UpsertAsync(
        string kind,
        UpdateTenantSettingsDocumentDto input,
        CancellationToken ct = default)
    {
        kind = NormalizeKind(kind);
        await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.TenantSettingsManage, ct);
        ValidateInput(kind, input);

        var partitionKey = Partition(_userContext.TenantId);
        var rowKey = Row(kind);
        var existing = await _repository.GetAsync(partitionKey, rowKey, ct, includeDeleted: true);
        ValidateVersion(existing, input.ExpectedVersion);

        var now = DateTime.UtcNow;
        var entity = existing ?? new TenantSettingsDocument
        {
            Id = DeterministicId(_userContext.TenantId, kind),
            TenantId = _userContext.TenantId,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            DateCreated = now
        };

        entity.Kind = kind;
        entity.SchemaVersion = input.SchemaVersion;
        entity.IsPublic = string.Equals(kind, TenantSettingKinds.PublicContent, StringComparison.Ordinal);
        entity.ValuesJson = input.Values.GetRawText();
        entity.SecretReferencesJson = JsonSerializer.Serialize(input.SecretReferences);
        entity.IsDeleted = false;
        entity.DateUpdated = now;
        if (existing is not null)
        {
            entity.ETag = existing.ETag;
        }

        var saved = await _repository.SaveAsync(entity, ct);
        await _audit.RecordAsync(new RecordAuditEventRequestDto
        {
            TenantId = _userContext.TenantId,
            Category = "tenant-settings",
            Action = existing is null ? "created" : "updated",
            TargetType = "tenant-settings",
            TargetId = kind,
            Source = "api",
            Description = $"Tenant {kind} settings were {(existing is null ? "created" : "updated")}.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                kind,
                schemaVersion = input.SchemaVersion,
                configuredSecretCount = input.SecretReferences.Count
            })
        }, ct);

        return ToDto(saved, exposeConfiguredSecrets: !entity.IsPublic);
    }

    private static void ValidateInput(string kind, UpdateTenantSettingsDocumentDto input)
    {
        if (input.SchemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.SchemaVersion),
                input.SchemaVersion,
                $"Schema version {CurrentSchemaVersion} is required.");
        }

        if (input.Values.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Settings values must be a JSON object.", nameof(input.Values));
        }

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(input.Values);
        if (payloadBytes.Length > MaxPayloadBytes)
        {
            throw new ArgumentException($"Settings values cannot exceed {MaxPayloadBytes} bytes.", nameof(input.Values));
        }

        RejectSensitiveValues(input.Values, "$values");
        ValidateSecretReferences(kind, input.SecretReferences);

        if (string.Equals(kind, TenantSettingKinds.PublicContent, StringComparison.Ordinal))
        {
            ValidatePublicContent(input.Values);
        }

        if (string.Equals(kind, TenantSettingKinds.Billing, StringComparison.Ordinal))
        {
            if (!input.Values.TryGetProperty("depositPercentRequired", out var deposit) ||
                deposit.ValueKind != JsonValueKind.Number ||
                !deposit.TryGetDecimal(out var percent) ||
                percent is < 0 or > 100)
            {
                throw new ArgumentException(
                    "Billing settings require depositPercentRequired between 0 and 100.",
                    nameof(input.Values));
            }
        }

        if (string.Equals(kind, TenantSettingKinds.Operational, StringComparison.Ordinal))
        {
            ValidateOperationalValues(input.Values);
        }
    }

    private static void ValidatePublicContent(JsonElement values)
    {
        var requiredSections = new[] { "navigation", "hero", "services", "quoteForm", "footer" };
        foreach (var section in requiredSections)
        {
            if (!values.TryGetProperty(section, out var value) || value.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException(
                    $"Public content requires an object-valued {section} section.",
                    nameof(values));
            }
        }
    }

    private static void ValidateOperationalValues(JsonElement values)
    {
        foreach (var property in values.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetDecimal(out var number) &&
                number < 0)
            {
                throw new ArgumentException($"Operational setting {property.Name} cannot be negative.", nameof(values));
            }

            if (property.Value.ValueKind == JsonValueKind.Array &&
                property.Value.EnumerateArray().Any(item =>
                    item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
            {
                throw new ArgumentException(
                    $"Operational setting {property.Name} must contain non-empty strings.",
                    nameof(values));
            }
        }

        if (values.TryGetProperty("defaultCrewSize", out var crewSize) &&
            (!crewSize.TryGetInt32(out var crew) || crew < 1))
        {
            throw new ArgumentException("defaultCrewSize must be at least one.", nameof(values));
        }

        if (values.TryGetProperty("depositPercentRequired", out var deposit) &&
            (!deposit.TryGetDecimal(out var percent) || percent is < 0 or > 100))
        {
            throw new ArgumentException("depositPercentRequired must be between 0 and 100.", nameof(values));
        }
    }

    private static void RejectSensitiveValues(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal)
                    .Replace("-", string.Empty, StringComparison.Ordinal)
                    .ToLowerInvariant();
                if (SensitiveKeyFragments.Any(normalized.Contains))
                {
                    throw new ArgumentException(
                        $"Sensitive value {path}.{property.Name} must be stored as a secret reference.",
                        nameof(value));
                }

                RejectSensitiveValues(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                RejectSensitiveValues(item, $"{path}[{index++}]");
            }
        }
    }

    private static void ValidateSecretReferences(string kind, IReadOnlyDictionary<string, string> references)
    {
        if (string.Equals(kind, TenantSettingKinds.PublicContent, StringComparison.Ordinal) && references.Count > 0)
        {
            throw new ArgumentException("Public content cannot contain secret references.", nameof(references));
        }

        foreach (var (key, value) in references)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            {
                throw new ArgumentException("Secret reference keys must be between 1 and 100 characters.", nameof(references));
            }

            var reference = value.Trim();
            if (reference.Length > 500 ||
                !(reference.StartsWith("secret://", StringComparison.OrdinalIgnoreCase) ||
                  reference.StartsWith("keyvault://", StringComparison.OrdinalIgnoreCase) ||
                  reference.StartsWith("env://", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"Secret reference {key} must use secret://, keyvault://, or env://.",
                    nameof(references));
            }
        }
    }

    private static void ValidateVersion(TenantSettingsDocument? existing, string? expectedVersion)
    {
        if (existing is null)
        {
            if (!string.IsNullOrWhiteSpace(expectedVersion))
            {
                throw new ArgumentException("A version cannot be supplied when creating settings.", nameof(expectedVersion));
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(expectedVersion) ||
            !string.Equals(existing.ETag.ToString(), expectedVersion.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The settings changed after they were loaded. Refresh and try again.",
                nameof(expectedVersion));
        }
    }

    private static TenantSettingsDocumentDto ToDto(
        TenantSettingsDocument entity,
        bool exposeConfiguredSecrets)
    {
        using var values = JsonDocument.Parse(entity.ValuesJson);
        var secretKeys = exposeConfiguredSecrets
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(entity.SecretReferencesJson)?.Keys
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? []
            : [];

        return new TenantSettingsDocumentDto
        {
            Kind = entity.Kind,
            SchemaVersion = entity.SchemaVersion,
            IsPublic = entity.IsPublic,
            Values = values.RootElement.Clone(),
            ConfiguredSecretKeys = secretKeys,
            Version = entity.ETag.ToString(),
            UpdatedUtc = entity.DateUpdated
        };
    }

    private static TenantSettingsDocumentDto CreateDefault(string kind, bool isPublic)
    {
        var values = kind switch
        {
            TenantSettingKinds.Billing => JsonSerializer.SerializeToElement(new { depositPercentRequired = 50m }),
            _ => JsonSerializer.SerializeToElement(new { })
        };

        return new TenantSettingsDocumentDto
        {
            Kind = kind,
            SchemaVersion = CurrentSchemaVersion,
            IsPublic = isPublic,
            Values = values,
            UpdatedUtc = DateTime.UnixEpoch
        };
    }

    private static string NormalizeKind(string kind)
    {
        var normalized = kind?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!TenantSettingKinds.All.Contains(normalized))
        {
            throw new ArgumentException("Unknown tenant settings kind.", nameof(kind));
        }

        return normalized;
    }

    private static string NormalizeProtectedKind(string kind)
    {
        var normalized = NormalizeKind(kind);
        if (string.Equals(normalized, TenantSettingKinds.PublicContent, StringComparison.Ordinal))
        {
            throw new ArgumentException("Use the public content endpoint for public settings.", nameof(kind));
        }

        return normalized;
    }

    private static string Partition(Guid tenantId) => RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private static string Row(string kind) => $"SETTINGS|{kind.ToUpperInvariant()}";

    private static Guid DeterministicId(Guid tenantId, string kind)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{tenantId:N}:{kind}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
