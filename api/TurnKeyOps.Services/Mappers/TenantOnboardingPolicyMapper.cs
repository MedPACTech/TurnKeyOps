using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantOnboardingPolicyMapper
    {
        public static TenantOnboardingPolicyDto ToDto(TenantOnboardingPolicy entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            ReserveSeatAtInviteTime = entity.ReserveSeatAtInviteTime,
            AutoAssignSeatOnActivation = entity.AutoAssignSeatOnActivation,
            DefaultInviteExpiryDays = entity.DefaultInviteExpiryDays,
            ExpiredInviteHandling = entity.ExpiredInviteHandling,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };
    }
}
