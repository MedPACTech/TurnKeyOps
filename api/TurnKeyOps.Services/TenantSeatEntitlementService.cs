using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class TenantSeatEntitlementService : ITenantSeatEntitlementService
    {
        private readonly ITenantSeatEntitlementRepository _seatRepository;
        private readonly ITenantSubscriptionRepository _subscriptionRepository;
        private readonly IUserContext _userContext;

        public TenantSeatEntitlementService(
            ITenantSeatEntitlementRepository seatRepository,
            ITenantSubscriptionRepository subscriptionRepository,
            IUserContext userContext)
        {
            _seatRepository = seatRepository;
            _subscriptionRepository = subscriptionRepository;
            _userContext = userContext;
        }

        public async Task<TenantSeatEntitlementDto?> GetCurrentAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entity = await _seatRepository.GetCurrentAsync(PartitionKey(_userContext.TenantId), ct);
            return entity is null ? null : TenantSeatEntitlementMapper.ToDto(entity);
        }

        public async Task<TenantSeatEntitlementDto> SyncPurchasedSeatsAsync(Guid tenantId, Guid subscriptionId, int purchasedSeats, CancellationToken ct = default)
        {
            if (purchasedSeats < 0)
                throw new ArgumentOutOfRangeException(nameof(purchasedSeats));

            var partitionKey = PartitionKey(tenantId);
            var existing = await _seatRepository.GetBySubscriptionIdAsync(partitionKey, subscriptionId, ct);
            var entity = existing ?? new TenantSeatEntitlement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SubscriptionId = subscriptionId,
                PartitionKey = partitionKey,
                RowKey = $"SEATS|{subscriptionId:N}",
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            entity.PurchasedSeats = purchasedSeats;
            entity.NextRenewalSeatCount = Math.Max(entity.NextRenewalSeatCount, purchasedSeats);
            entity.AvailableSeats = Math.Max(0, entity.PurchasedSeats - entity.AssignedSeats - entity.ReservedSeats);
            entity.DateUpdated = DateTime.UtcNow;

            var saved = await _seatRepository.SaveAsync(entity, ct);
            return TenantSeatEntitlementMapper.ToDto(saved);
        }

        public async Task<TenantSeatEntitlementDto> ScheduleSeatReductionAsync(Guid tenantId, int requestedSeatCount, CancellationToken ct = default)
        {
            if (requestedSeatCount < 0)
                throw new ArgumentOutOfRangeException(nameof(requestedSeatCount));

            var partitionKey = PartitionKey(tenantId);
            var entitlement = await RequireEntitlementAsync(tenantId, ct);
            entitlement.NextRenewalSeatCount = requestedSeatCount;
            entitlement.DateUpdated = DateTime.UtcNow;

            var subscription = await _subscriptionRepository.GetCurrentAsync(partitionKey, ct);
            if (subscription is not null)
            {
                subscription.NextRenewalSeatCount = requestedSeatCount;
                subscription.DateUpdated = DateTime.UtcNow;
                await _subscriptionRepository.SaveAsync(subscription, ct);
            }

            var saved = await _seatRepository.SaveAsync(entitlement, ct);
            return TenantSeatEntitlementMapper.ToDto(saved);
        }

        public async Task<TenantSeatEntitlementDto> ReserveSeatAsync(Guid tenantId, CancellationToken ct = default)
        {
            var entitlement = await RequireEntitlementAsync(tenantId, ct);
            if (entitlement.AvailableSeats < 1)
                throw new InvalidOperationException("No seats are available.");

            entitlement.ReservedSeats += 1;
            entitlement.AvailableSeats -= 1;
            entitlement.DateUpdated = DateTime.UtcNow;

            var saved = await _seatRepository.SaveAsync(entitlement, ct);
            return TenantSeatEntitlementMapper.ToDto(saved);
        }

        public async Task<TenantSeatEntitlementDto> AssignSeatAsync(Guid tenantId, CancellationToken ct = default)
        {
            var entitlement = await RequireEntitlementAsync(tenantId, ct);

            if (entitlement.ReservedSeats > 0)
            {
                entitlement.ReservedSeats -= 1;
                entitlement.AssignedSeats += 1;
            }
            else if (entitlement.AvailableSeats > 0)
            {
                entitlement.AvailableSeats -= 1;
                entitlement.AssignedSeats += 1;
            }
            else
            {
                throw new InvalidOperationException("No seats are available to assign.");
            }

            entitlement.DateUpdated = DateTime.UtcNow;
            var saved = await _seatRepository.SaveAsync(entitlement, ct);
            return TenantSeatEntitlementMapper.ToDto(saved);
        }

        public async Task<TenantSeatEntitlementDto> ReleaseSeatAsync(Guid tenantId, string previousSeatStatus, CancellationToken ct = default)
        {
            var entitlement = await RequireEntitlementAsync(tenantId, ct);

            if (string.Equals(previousSeatStatus, "Assigned", StringComparison.OrdinalIgnoreCase))
            {
                entitlement.AssignedSeats = Math.Max(0, entitlement.AssignedSeats - 1);
                entitlement.AvailableSeats += 1;
            }
            else if (string.Equals(previousSeatStatus, "Reserved", StringComparison.OrdinalIgnoreCase))
            {
                entitlement.ReservedSeats = Math.Max(0, entitlement.ReservedSeats - 1);
                entitlement.AvailableSeats += 1;
            }

            entitlement.DateUpdated = DateTime.UtcNow;
            var saved = await _seatRepository.SaveAsync(entitlement, ct);
            return TenantSeatEntitlementMapper.ToDto(saved);
        }

        public async Task<TenantSeatEntitlementDto> ApplyRenewalAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = PartitionKey(tenantId);
            var entitlement = await RequireEntitlementAsync(tenantId, ct);
            var renewedSeatCount = Math.Max(entitlement.NextRenewalSeatCount, entitlement.AssignedSeats + entitlement.ReservedSeats);

            entitlement.PurchasedSeats = renewedSeatCount;
            entitlement.AvailableSeats = Math.Max(0, renewedSeatCount - entitlement.AssignedSeats - entitlement.ReservedSeats);
            entitlement.DateUpdated = DateTime.UtcNow;

            var subscription = await _subscriptionRepository.GetCurrentAsync(partitionKey, ct);
            if (subscription is not null)
            {
                subscription.CurrentSeatCount = renewedSeatCount;
                subscription.NextRenewalSeatCount = renewedSeatCount;
                subscription.DateUpdated = DateTime.UtcNow;
                await _subscriptionRepository.SaveAsync(subscription, ct);
            }

            var saved = await _seatRepository.SaveAsync(entitlement, ct);
            return TenantSeatEntitlementMapper.ToDto(saved);
        }

        private async Task<TenantSeatEntitlement> RequireEntitlementAsync(Guid tenantId, CancellationToken ct)
            => await _seatRepository.GetCurrentAsync(PartitionKey(tenantId), ct)
               ?? throw new InvalidOperationException("Tenant seat entitlement was not found.");

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static string PartitionKey(Guid tenantId) => EntityKeyPolicy.TenantPartition(tenantId);
    }
}
