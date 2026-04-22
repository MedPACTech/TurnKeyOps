using System.Globalization;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientDiagnosisMapper
    {
        public static PatientDiagnosisDto ToDto(PatientDiagnosis entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientDiagnosisDto
            {
                Id = id,
                PatientId = entity.PatientId,
                DiagnosisCodeId = entity.DiagnosisCodeId,
                DiagnosisCode = entity.DiagnosisCode,
                DateDiagnosed = ParseDateDiagnosed(entity.DateDiagnosed),
                DiagnosisStatusId = entity.DiagnosisStatusId,
                DiagnosisStatus = entity.DiagnosisStatus,
                ShortDescription = entity.ShortDescription,
                LongDescription = entity.LongDescription,
                IsDeleted = entity.IsDeleted
            };
        }

        public static PatientDiagnosis ToEntity(PatientDiagnosisDto dto)
        {
            return new PatientDiagnosis
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                DiagnosisCodeId = dto.DiagnosisCodeId,
                DiagnosisCode = dto.DiagnosisCode,
                DateDiagnosed = dto.DateDiagnosed?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                DiagnosisStatusId = dto.DiagnosisStatusId,
                DiagnosisStatus = dto.DiagnosisStatus,
                ShortDescription = dto.ShortDescription,
                LongDescription = dto.LongDescription,
                IsDeleted = dto.IsDeleted
            };
        }

        private static DateOnly? ParseDateDiagnosed(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            var normalized = rawValue.Trim().Trim('"');

            if (DateOnly.TryParseExact(
                    normalized,
                    new[] { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dateOnly))
            {
                return dateOnly;
            }

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                return DateOnly.FromDateTime(dateTime);

            return null;
        }
    }
}
