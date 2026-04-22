using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantSeatEntitlementMapper
    {
        public static TenantSeatEntitlementDto ToDto(TenantSeatEntitlement entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SubscriptionId = entity.SubscriptionId,
            PurchasedSeats = entity.PurchasedSeats,
            AssignedSeats = entity.AssignedSeats,
            ReservedSeats = entity.ReservedSeats,
            AvailableSeats = entity.AvailableSeats,
            NextRenewalSeatCount = entity.NextRenewalSeatCount,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };

        public static TenantSeatEntitlement ToEntity(TenantSeatEntitlementDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            SubscriptionId = dto.SubscriptionId,
            PurchasedSeats = dto.PurchasedSeats,
            AssignedSeats = dto.AssignedSeats,
            ReservedSeats = dto.ReservedSeats,
            AvailableSeats = dto.AvailableSeats,
            NextRenewalSeatCount = dto.NextRenewalSeatCount,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };
    }
}
