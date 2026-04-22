using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IInviteService
    {
        Task<IEnumerable<InviteDto>> GetAllAsync(CancellationToken ct = default);
        Task<InviteDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<InviteAcceptanceContextDto> GetAcceptanceContextAsync(Guid id, string inviteToken, CancellationToken ct = default);
        Task<InviteDto> CreateAsync(CreateInviteRequestDto dto, CancellationToken ct = default);
        Task<InviteDto> ResendAsync(Guid id, CancellationToken ct = default);
        Task<InviteDto> CancelAsync(Guid id, CancellationToken ct = default);
        Task<TenantMembershipDto> RedeemAsync(Guid id, RedeemInviteRequestDto dto, CancellationToken ct = default);
    }
}
