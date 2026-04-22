using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantProfileMapper
    {
        public static TenantProfileDto ToDto(TenantProfile entity)
        {
            return new TenantProfileDto
            {
                Id = entity.Id,
                TenantName = entity.TenantName,
                LogoUrl = entity.LogoUrl,
                Website = entity.Website,
                AddressLine1 = entity.AddressLine1,
                AddressLine2 = entity.AddressLine2,
                City = entity.City,
                State = entity.State,
                PostalCode = entity.PostalCode,
                PointOfContactName = entity.PointOfContactName,
                PointOfContactEmail = entity.PointOfContactEmail,
                PointOfContactPhone = entity.PointOfContactPhone,
                BusinessLegalName = entity.BusinessLegalName,
                BillingContactName = entity.BillingContactName,
                BillingContactEmail = entity.BillingContactEmail,
                BillingContactPhone = entity.BillingContactPhone,
                BillingEmail = entity.BillingEmail,
                BillingAddressLine1 = entity.BillingAddressLine1,
                BillingAddressLine2 = entity.BillingAddressLine2,
                BillingCity = entity.BillingCity,
                BillingState = entity.BillingState,
                BillingPostalCode = entity.BillingPostalCode,
                BillingCountry = entity.BillingCountry,
                TaxRegistrationNumber = entity.TaxRegistrationNumber,
                TaxRegion = entity.TaxRegion,
                IsTaxExempt = entity.IsTaxExempt,
                EnterpriseAccountNumber = entity.EnterpriseAccountNumber,
                EnterpriseCustomerCode = entity.EnterpriseCustomerCode,
                PurchaseOrderNumber = entity.PurchaseOrderNumber,
                DefaultNoteTypeId = entity.DefaultNoteTypeId,
                IsActive = entity.IsActive,
                DateCreated = entity.DateCreated,
                DateUpdated = entity.DateUpdated
            };
        }

        public static TenantProfile ToEntity(TenantProfileDto dto, string partitionKey, string rowKey)
        {
            return new TenantProfile
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                TenantName = dto.TenantName.Trim(),
                LogoUrl = Normalize(dto.LogoUrl),
                Website = Normalize(dto.Website),
                AddressLine1 = Normalize(dto.AddressLine1),
                AddressLine2 = Normalize(dto.AddressLine2),
                City = Normalize(dto.City),
                State = Normalize(dto.State),
                PostalCode = Normalize(dto.PostalCode),
                PointOfContactName = Normalize(dto.PointOfContactName),
                PointOfContactEmail = Normalize(dto.PointOfContactEmail),
                PointOfContactPhone = Normalize(dto.PointOfContactPhone),
                BusinessLegalName = Normalize(dto.BusinessLegalName),
                BillingContactName = Normalize(dto.BillingContactName),
                BillingContactEmail = Normalize(dto.BillingContactEmail),
                BillingContactPhone = Normalize(dto.BillingContactPhone),
                BillingEmail = Normalize(dto.BillingEmail),
                BillingAddressLine1 = Normalize(dto.BillingAddressLine1),
                BillingAddressLine2 = Normalize(dto.BillingAddressLine2),
                BillingCity = Normalize(dto.BillingCity),
                BillingState = Normalize(dto.BillingState),
                BillingPostalCode = Normalize(dto.BillingPostalCode),
                BillingCountry = Normalize(dto.BillingCountry),
                TaxRegistrationNumber = Normalize(dto.TaxRegistrationNumber),
                TaxRegion = Normalize(dto.TaxRegion),
                IsTaxExempt = dto.IsTaxExempt,
                EnterpriseAccountNumber = Normalize(dto.EnterpriseAccountNumber),
                EnterpriseCustomerCode = Normalize(dto.EnterpriseCustomerCode),
                PurchaseOrderNumber = Normalize(dto.PurchaseOrderNumber),
                DefaultNoteTypeId = dto.DefaultNoteTypeId,
                IsActive = dto.IsActive,
                IsDeleted = false
            };
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
