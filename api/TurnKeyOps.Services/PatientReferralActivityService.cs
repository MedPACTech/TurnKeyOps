using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class PatientReferralActivityService : IPatientReferralActivityService
    {
        private readonly IPatientReferralActivityRepository _activityRepository;
        private readonly IPatientReferralRepository _referralRepository;
        private readonly IUserContext _userContext;

        public PatientReferralActivityService(
            IPatientReferralActivityRepository activityRepository,
            IPatientReferralRepository referralRepository,
            IUserContext userContext)
        {
            _activityRepository = activityRepository;
            _referralRepository = referralRepository;
            _userContext = userContext;
        }

        public async Task<IReadOnlyList<PatientReferralActivityDto>> GetByReferralAsync(Guid patientReferralId, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await GetRequiredReferralAsync(patientReferralId, ct);

            var items = await _activityRepository.GetByReferralAsync(_userContext.TenantId, patientReferralId, ct);
            return items.Select(PatientReferralActivityMapper.ToDto).ToList();
        }

        public async Task<PatientReferralActivityDto> AddNoteAsync(Guid patientReferralId, CreatePatientReferralActivityNoteDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            if (string.IsNullOrWhiteSpace(dto.Note))
                throw new ArgumentException("note is required.", nameof(dto));

            var referral = await GetRequiredReferralAsync(patientReferralId, ct);

            return await AppendAsync(new CreatePatientReferralActivityDto
            {
                PatientReferralId = referral.Id,
                PatientId = referral.PatientId,
                ActivityType = PatientReferralActivityTypes.NoteAdded,
                Title = "Referral note added",
                Body = dto.Note,
                CreatedByName = string.IsNullOrWhiteSpace(dto.CreatedByName) ? CurrentActorName() : dto.CreatedByName
            }, ct);
        }

        public async Task<PatientReferralActivityDto> AppendAsync(CreatePatientReferralActivityDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            ArgumentNullException.ThrowIfNull(dto);

            var referral = await GetRequiredReferralAsync(dto.PatientReferralId, ct);
            var entity = PatientReferralActivityMapper.ToEntity(dto);
            entity.PatientId = referral.PatientId;
            entity.PartitionKey = PatientReferralActivityRepository.PartitionKeyForReferral(_userContext.TenantId, referral.Id);
            entity.RowKey = PatientReferralActivityRepository.RowKeyFor(entity.CreatedAtUtc, entity.Id);
            if (!entity.CreatedByUserId.HasValue)
                entity.CreatedByUserId = _userContext.UserId;
            if (string.IsNullOrWhiteSpace(entity.CreatedByName))
                entity.CreatedByName = CurrentActorName();

            var saved = await _activityRepository.SaveAsync(entity, ct);
            return PatientReferralActivityMapper.ToDto(saved);
        }

        private async Task<PatientReferral> GetRequiredReferralAsync(Guid patientReferralId, CancellationToken ct)
            => await _referralRepository.GetByRowKeyAsync(EntityKeyPolicy.Row(patientReferralId), ct)
                ?? throw new KeyNotFoundException("Referral not found.");

        private string CurrentActorName()
        {
            var fullName = $"{_userContext.FirstName} {_userContext.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? _userContext.UserId.ToString("D") : fullName;
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }
    }
}
