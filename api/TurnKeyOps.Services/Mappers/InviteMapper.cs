using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class InviteMapper
    {
        public static InviteDto ToDto(Invite entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ReservedSeatMembershipId = entity.ReservedSeatMembershipId,
            SentByMembershipId = entity.SentByMembershipId,
            RedeemedByUserId = entity.RedeemedByUserId,
            InvitedEmail = entity.InvitedEmail,
            InvitedPhone = entity.InvitedPhone,
            Role = entity.Role,
            Status = entity.Status,
            InviteToken = null,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            DateCreated = entity.DateCreated,
            DateRedeemed = entity.DateRedeemed,
            DateUpdated = entity.DateUpdated
        };

        public static Invite ToEntity(InviteDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            ReservedSeatMembershipId = dto.ReservedSeatMembershipId,
            SentByMembershipId = dto.SentByMembershipId,
            RedeemedByUserId = dto.RedeemedByUserId,
            InvitedEmail = Normalize(dto.InvitedEmail),
            InvitedPhone = Normalize(dto.InvitedPhone),
            Role = dto.Role.Trim(),
            Status = dto.Status.Trim(),
            ExpiresAtUtc = dto.ExpiresAtUtc,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateRedeemed = dto.DateRedeemed,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
