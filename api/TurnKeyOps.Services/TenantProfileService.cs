using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Authorization;
using System.Text.Json;

namespace MedInsights.Services
{
    public sealed class TenantProfileService : ITenantProfileService
    {
        private readonly ITenantProfileRepository _repository;
        private readonly IUserContext _userContext;
        private readonly IRoleAccessService _roleAccess;

        public TenantProfileService(
            ITenantProfileRepository repository,
            IUserContext userContext,
            IRoleAccessService roleAccess)
        {
            _repository = repository;
            _userContext = userContext;
            _roleAccess = roleAccess;
        }

        public async Task<TenantProfileDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            if (id != _userContext.TenantId)
                throw new KeyNotFoundException("Tenant profile not found.");

            return await EnsureProfileExistsAsync(ct);
        }

        public Task<TenantProfileDto?> GetCurrentAsync(CancellationToken ct = default)
            => GetAsync(_userContext.TenantId, ct);

        public async Task<TenantProfileDto> EnsureProfileExistsAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var tenantId = _userContext.TenantId;
            var partitionKey = PartitionKeyForTenant();
            var rowKey = EntityKeyPolicy.Row(tenantId);
            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);
            if (existing is not null)
                return TenantProfileMapper.ToDto(existing);

            var now = DateTime.UtcNow;
            var entity = new Lib.Entities.TenantProfile
            {
                Id = tenantId,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                TenantName = string.Empty,
                LogoUrl = null,
                Website = null,
                AddressLine1 = null,
                AddressLine2 = null,
                City = null,
                State = null,
                PostalCode = null,
                PointOfContactName = null,
                PointOfContactEmail = null,
                PointOfContactPhone = null,
                BusinessLegalName = null,
                BillingContactName = null,
                BillingContactEmail = null,
                BillingContactPhone = null,
                BillingEmail = null,
                BillingAddressLine1 = null,
                BillingAddressLine2 = null,
                BillingCity = null,
                BillingState = null,
                BillingPostalCode = null,
                BillingCountry = null,
                TaxRegistrationNumber = null,
                TaxRegion = null,
                IsTaxExempt = false,
                EnterpriseAccountNumber = null,
                EnterpriseCustomerCode = null,
                PurchaseOrderNumber = null,
                DefaultNoteTypeId = null,
                IsActive = true,
                IsDeleted = false,
                DateCreated = now,
                DateUpdated = now
            };

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantProfileMapper.ToDto(saved);
        }

        public async Task<TenantProfileDto> CreateAsync(TenantProfileDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.TenantManage, ct);
            ValidateForCreate(dto);

            var tenantId = ResolveTenantId(dto.Id);
            var partitionKey = PartitionKeyForTenant();
            var rowKey = EntityKeyPolicy.Row(tenantId);

            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);
            if (existing is not null)
                throw new InvalidOperationException("Tenant profile already exists.");

            var now = DateTime.UtcNow;
            dto.Id = tenantId;
            dto.DateCreated = now;
            dto.DateUpdated = now;

            var entity = TenantProfileMapper.ToEntity(dto, partitionKey, rowKey);
            entity.DateCreated = now;
            entity.DateUpdated = now;

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantProfileMapper.ToDto(saved);
        }

        public async Task<TenantProfileDto> UpdateAsync(Guid id, JsonElement payload, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.TenantManage, ct);
            var tenantId = ResolveTenantId(id);
            var partitionKey = PartitionKeyForTenant();
            var rowKey = EntityKeyPolicy.Row(tenantId);

            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);
            if (existing is null)
            {
                await EnsureProfileExistsAsync(ct);
                existing = await _repository.GetAsync(partitionKey, rowKey, ct)
                    ?? throw new KeyNotFoundException("Tenant profile not found.");
            }

            var dto = TenantProfileMapper.ToDto(existing);
            ApplyPatch(dto, payload, tenantId);

            var entity = TenantProfileMapper.ToEntity(dto, partitionKey, rowKey);
            entity.Id = tenantId;
            entity.DateCreated = existing.DateCreated;
            entity.DateUpdated = DateTime.UtcNow;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantProfileMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.TenantManage, ct);
            if (id != _userContext.TenantId)
                throw new KeyNotFoundException("Tenant profile not found.");

            var existing = await _repository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(id), ct)
                ?? throw new KeyNotFoundException("Tenant profile not found.");

            existing.IsDeleted = true;
            existing.DateUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(existing, ct);
        }

        private string PartitionKeyForTenant() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);

        private Guid ResolveTenantId(Guid id)
        {
            if (id == Guid.Empty)
                return _userContext.TenantId;

            if (id != _userContext.TenantId)
                throw new InvalidOperationException("Tenant id mismatch.");

            return id;
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static void ValidateForCreate(TenantProfileDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TenantName))
                throw new ArgumentException("Tenant name is required.", nameof(dto));
        }

        private static void ApplyPatch(TenantProfileDto dto, JsonElement payload, Guid tenantId)
        {
            if (payload.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Tenant profile payload must be a JSON object.", nameof(payload));

            foreach (var property in payload.EnumerateObject())
            {
                if (property.NameEquals("id") || property.NameEquals("Id"))
                {
                    var value = ReadGuid(property.Value, property.Name);
                    if (value != Guid.Empty && value != tenantId)
                        throw new ArgumentException("ID mismatch.", nameof(payload));

                    continue;
                }

                if (property.NameEquals("tenantName") || property.NameEquals("TenantName"))
                {
                    dto.TenantName = ReadRequiredString(property.Value);
                    continue;
                }

                if (property.NameEquals("logoUrl") || property.NameEquals("LogoUrl"))
                {
                    dto.LogoUrl = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("website") || property.NameEquals("Website"))
                {
                    dto.Website = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("addressLine1") || property.NameEquals("AddressLine1"))
                {
                    dto.AddressLine1 = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("addressLine2") || property.NameEquals("AddressLine2"))
                {
                    dto.AddressLine2 = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("city") || property.NameEquals("City"))
                {
                    dto.City = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("state") || property.NameEquals("State"))
                {
                    dto.State = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("postalCode") || property.NameEquals("PostalCode"))
                {
                    dto.PostalCode = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("pointOfContactName") || property.NameEquals("PointOfContactName"))
                {
                    dto.PointOfContactName = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("pointOfContactEmail") || property.NameEquals("PointOfContactEmail"))
                {
                    dto.PointOfContactEmail = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("pointOfContactPhone") || property.NameEquals("PointOfContactPhone"))
                {
                    dto.PointOfContactPhone = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("businessLegalName") || property.NameEquals("BusinessLegalName"))
                {
                    dto.BusinessLegalName = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingContactName") || property.NameEquals("BillingContactName"))
                {
                    dto.BillingContactName = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingContactEmail") || property.NameEquals("BillingContactEmail"))
                {
                    dto.BillingContactEmail = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingContactPhone") || property.NameEquals("BillingContactPhone"))
                {
                    dto.BillingContactPhone = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingEmail") || property.NameEquals("BillingEmail"))
                {
                    dto.BillingEmail = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingAddressLine1") || property.NameEquals("BillingAddressLine1"))
                {
                    dto.BillingAddressLine1 = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingAddressLine2") || property.NameEquals("BillingAddressLine2"))
                {
                    dto.BillingAddressLine2 = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingCity") || property.NameEquals("BillingCity"))
                {
                    dto.BillingCity = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingState") || property.NameEquals("BillingState"))
                {
                    dto.BillingState = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingPostalCode") || property.NameEquals("BillingPostalCode"))
                {
                    dto.BillingPostalCode = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("billingCountry") || property.NameEquals("BillingCountry"))
                {
                    dto.BillingCountry = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("taxRegistrationNumber") || property.NameEquals("TaxRegistrationNumber"))
                {
                    dto.TaxRegistrationNumber = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("taxRegion") || property.NameEquals("TaxRegion"))
                {
                    dto.TaxRegion = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("isTaxExempt") || property.NameEquals("IsTaxExempt"))
                {
                    dto.IsTaxExempt = ReadBoolean(property.Value, property.Name);
                    continue;
                }

                if (property.NameEquals("enterpriseAccountNumber") || property.NameEquals("EnterpriseAccountNumber"))
                {
                    dto.EnterpriseAccountNumber = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("enterpriseCustomerCode") || property.NameEquals("EnterpriseCustomerCode"))
                {
                    dto.EnterpriseCustomerCode = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("purchaseOrderNumber") || property.NameEquals("PurchaseOrderNumber"))
                {
                    dto.PurchaseOrderNumber = ReadNullableString(property.Value);
                    continue;
                }

                if (property.NameEquals("defaultNoteTypeId") || property.NameEquals("DefaultNoteTypeId"))
                {
                    dto.DefaultNoteTypeId = ReadNullableGuid(property.Value, property.Name);
                    continue;
                }

                if (property.NameEquals("isActive") || property.NameEquals("IsActive"))
                {
                    dto.IsActive = ReadBoolean(property.Value, property.Name);
                }
            }
        }

        private static string ReadRequiredString(JsonElement value)
            => value.ValueKind == JsonValueKind.Null ? string.Empty : value.GetString() ?? string.Empty;

        private static string? ReadNullableString(JsonElement value)
            => value.ValueKind == JsonValueKind.Null ? null : value.GetString();

        private static Guid ReadGuid(JsonElement value, string propertyName)
        {
            if (value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var guid))
                return guid;

            throw new ArgumentException($"'{propertyName}' must be a valid GUID.", propertyName);
        }

        private static Guid? ReadNullableGuid(JsonElement value, string propertyName)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return null;

            if (value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var guid))
                return guid;

            throw new ArgumentException($"'{propertyName}' must be null or a valid GUID.", propertyName);
        }

        private static bool ReadBoolean(JsonElement value, string propertyName)
        {
            if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                return value.GetBoolean();

            throw new ArgumentException($"'{propertyName}' must be a boolean.", propertyName);
        }
    }
}
