using System.Security.Cryptography;
using System.Text;
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

public sealed class ContactAccessGrantService : IContactAccessGrantService
{
    private static readonly IReadOnlySet<string> Roles = new HashSet<string>(StringComparer.Ordinal)
    {
        "none", "field", "office-admin", "owner"
    };

    private readonly IContactAccessGrantRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IRoleAccessService _roleAccess;
    private readonly IAuditService _audit;

    public ContactAccessGrantService(
        IContactAccessGrantRepository repository,
        IUserContext userContext,
        IRoleAccessService roleAccess,
        IAuditService audit)
    {
        _repository = repository;
        _userContext = userContext;
        _roleAccess = roleAccess;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ContactAccessGrantDto>> ListAsync(CancellationToken ct = default)
    {
        await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
        var entities = await _repository.ListByTenantAsync(_userContext.TenantId, ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<ContactAccessGrantDto?> GetAsync(string contactId, CancellationToken ct = default)
    {
        await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
        var normalized = NormalizeContactId(contactId);
        var entity = await _repository.GetAsync(Partition(), Row(normalized), ct);
        return entity is null || entity.IsDeleted ? null : ToDto(entity);
    }

    public async Task<ContactAccessGrantDto> UpsertAsync(
        string contactId,
        UpdateContactAccessGrantDto input,
        CancellationToken ct = default)
    {
        await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
        var normalizedContactId = NormalizeContactId(contactId);
        var role = input.Role?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Roles.Contains(role))
        {
            throw new ArgumentException("Contact access role must be none, field, office-admin, or owner.", nameof(input.Role));
        }

        if (string.Equals(role, "owner", StringComparison.Ordinal))
        {
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipOwnerGrant, ct);
        }

        var partitionKey = Partition();
        var rowKey = Row(normalizedContactId);
        var existing = await _repository.GetAsync(partitionKey, rowKey, ct, includeDeleted: true);
        ValidateVersion(existing, input.ExpectedVersion);

        var now = DateTime.UtcNow;
        var entity = existing ?? new ContactAccessGrant
        {
            Id = DeterministicId(_userContext.TenantId, normalizedContactId),
            TenantId = _userContext.TenantId,
            ContactId = normalizedContactId,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            DateCreated = now
        };

        entity.Role = role;
        entity.Enabled = input.Enabled && role != "none";
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
            Category = "contact-access",
            Action = existing is null ? "created" : "updated",
            TargetType = "contact-access-grant",
            TargetId = HashForAudit(normalizedContactId),
            Source = "api",
            Description = "A tenant contact access grant was updated.",
            MetadataJson = JsonSerializer.Serialize(new { role, enabled = entity.Enabled })
        }, ct);

        return ToDto(saved);
    }

    private string Partition() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    private static string NormalizeContactId(string contactId)
    {
        var normalized = contactId?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 200)
        {
            throw new ArgumentException("Contact id must be between 1 and 200 characters.", nameof(contactId));
        }

        return normalized;
    }

    private static string Row(string contactId) => $"CONTACT|{Hash(contactId)}";
    private static string HashForAudit(string contactId) => Hash(contactId)[..16];

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Guid DeterministicId(Guid tenantId, string contactId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}:{contactId}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static void ValidateVersion(ContactAccessGrant? existing, string? expectedVersion)
    {
        if (existing is null)
        {
            if (!string.IsNullOrWhiteSpace(expectedVersion))
            {
                throw new ArgumentException("A version cannot be supplied when creating a grant.", nameof(expectedVersion));
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(expectedVersion) ||
            !string.Equals(existing.ETag.ToString(), expectedVersion.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The contact grant changed after it was loaded. Refresh and try again.",
                nameof(expectedVersion));
        }
    }

    private static ContactAccessGrantDto ToDto(ContactAccessGrant entity) => new()
    {
        ContactId = entity.ContactId,
        Role = entity.Role,
        Enabled = entity.Enabled,
        Version = entity.ETag.ToString(),
        UpdatedUtc = entity.DateUpdated
    };
}
