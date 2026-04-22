using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantMembershipMapper
    {
        public static TenantMembershipDto ToDto(TenantMembership entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            Role = entity.Role,
            MembershipStatus = entity.MembershipStatus,
            SeatStatus = entity.SeatStatus,
            InvitedEmail = entity.InvitedEmail,
            InvitedPhone = entity.InvitedPhone,
            VerifiedJoinChannel = entity.VerifiedJoinChannel,
            IsOwner = entity.IsOwner,
            IsBillingAdmin = entity.IsBillingAdmin,
            DateCreated = entity.DateCreated,
            DateInvited = entity.DateInvited,
            DateJoined = entity.DateJoined,
            DateRemoved = entity.DateRemoved,
            DateUpdated = entity.DateUpdated
        };

        public static TenantMembership ToEntity(TenantMembershipDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            UserId = dto.UserId,
            Role = dto.Role.Trim(),
            MembershipStatus = dto.MembershipStatus.Trim(),
            SeatStatus = dto.SeatStatus.Trim(),
            InvitedEmail = Normalize(dto.InvitedEmail),
            InvitedPhone = Normalize(dto.InvitedPhone),
            VerifiedJoinChannel = Normalize(dto.VerifiedJoinChannel),
            IsOwner = dto.IsOwner,
            IsBillingAdmin = dto.IsBillingAdmin,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateInvited = dto.DateInvited,
            DateJoined = dto.DateJoined,
            DateRemoved = dto.DateRemoved,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
