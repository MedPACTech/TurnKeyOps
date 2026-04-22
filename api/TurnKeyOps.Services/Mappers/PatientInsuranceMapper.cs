using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientInsuranceMapper
    {
        public static PatientInsuranceDto ToDto(PatientInsurance entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientInsuranceDto
            {
                Id = id,
                PatientId = entity.PatientId,
                CardImage = entity.CardImage,
                EffectiveDate = entity.EffectiveDate,
                VerificationDate = entity.VerificationDate,
                Carrier = entity.Carrier,
                PolicyNumber = entity.PolicyNumber,
                GroupNumber = entity.GroupNumber,
                InsuredType = entity.InsuredType,
                VerificationPhone = entity.VerificationPhone,
                FirstName = entity.FirstName,
                MiddleName = entity.MiddleName,
                LastName = entity.LastName,
                Relationship = entity.Relationship,
                InsuranceProviderId = entity.InsuranceProviderId
            };
        }

        public static PatientInsurance ToEntity(PatientInsuranceDto dto)
        {
            return new PatientInsurance
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                CardImage = dto.CardImage,
                EffectiveDate = dto.EffectiveDate,
                VerificationDate = dto.VerificationDate,
                Carrier = dto.Carrier,
                PolicyNumber = dto.PolicyNumber,
                GroupNumber = dto.GroupNumber,
                InsuredType = dto.InsuredType,
                VerificationPhone = dto.VerificationPhone,
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                LastName = dto.LastName,
                Relationship = dto.Relationship,
                InsuranceProviderId = dto.InsuranceProviderId,
                IsDeleted = false
            };
        }
    }
}
