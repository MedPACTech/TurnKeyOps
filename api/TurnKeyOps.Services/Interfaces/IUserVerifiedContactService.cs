using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IUserVerifiedContactService
    {
        Task<UserContactChangeRequestDto> RequestChangeAsync(RequestUserContactChangeDto dto, CancellationToken ct = default);
        Task<UserProfileDto> VerifyChangeAsync(VerifyUserContactChangeDto dto, CancellationToken ct = default);
    }
}
