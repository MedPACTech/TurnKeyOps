using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientMilitaryFirstResponderMapper
    {
        public static PatientMilitaryFirstResponderDto ToDto(PatientMilitaryFirstResponder entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            var militaryService = ResolveMilitaryService(entity);
            var branch = militaryService == "Active"
                ? EmptyToNull(entity.ActiveMilitaryBranch)
                : militaryService == "Veteran"
                    ? EmptyToNull(entity.MilitaryVeteranBranch)
                    : null;

            return new PatientMilitaryFirstResponderDto
            {
                Id = id,
                PatientId = entity.PatientId,
                MilitaryService = militaryService,
                Branch = branch,
                DateDischarged = entity.DateDischarged,
                DateEnlisted = entity.DateEnlisted,
                MilitaryId = EmptyToNull(entity.MilitaryId),
                FirstResponder = NormalizeStatus(entity.FirstResponder),
                FirstResponderType = EmptyToNull(entity.FirstResponderType),
                FirstResponderDepartment = EmptyToNull(entity.FirstResponderDepartment),
                FirstResponderStation = EmptyToNull(entity.FirstResponderStation),
                LawEnforcement = NormalizeStatus(entity.LawEnforcement),
                LawEnforcementType = EmptyToNull(entity.LawEnforcementType),
                LawEnforcementAgency = EmptyToNull(entity.LawEnforcementAgency),
                LawEnforcementId = EmptyToNull(entity.LawEnforcementId)
            };
        }

        public static PatientMilitaryFirstResponder ToEntity(PatientMilitaryFirstResponderDto dto)
        {
            var militaryService = NormalizeStatus(dto.MilitaryService);
            var branch = NullToEmpty(dto.Branch);

            return new PatientMilitaryFirstResponder
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                MilitaryVeteran = ToMilitaryVeteranFlag(militaryService),
                MilitaryVeteranBranch = militaryService == "Veteran" ? branch : string.Empty,
                DateDischarged = dto.DateDischarged,
                ActiveMilitary = ToActiveMilitaryFlag(militaryService),
                ActiveMilitaryBranch = militaryService == "Active" ? branch : string.Empty,
                DateEnlisted = dto.DateEnlisted,
                MilitaryId = NullToEmpty(dto.MilitaryId),
                FirstResponder = NormalizeStatus(dto.FirstResponder),
                FirstResponderType = NullToEmpty(dto.FirstResponderType),
                FirstResponderDepartment = NullToEmpty(dto.FirstResponderDepartment),
                FirstResponderStation = NullToEmpty(dto.FirstResponderStation),
                LawEnforcement = NormalizeStatus(dto.LawEnforcement),
                LawEnforcementType = NullToEmpty(dto.LawEnforcementType),
                LawEnforcementAgency = NullToEmpty(dto.LawEnforcementAgency),
                LawEnforcementId = NullToEmpty(dto.LawEnforcementId),
                IsDeleted = false
            };
        }

        private static string ResolveMilitaryService(PatientMilitaryFirstResponder entity)
        {
            if (IsActiveValue(entity.ActiveMilitary))
                return "Active";

            if (IsVeteranValue(entity.MilitaryVeteran))
                return "Veteran";

            if (IsUnknownValue(entity.ActiveMilitary) || IsUnknownValue(entity.MilitaryVeteran))
                return "Unknown";

            return "No";
        }

        private static bool IsActiveValue(string? value)
        {
            return value != null
                && (value.Equals("Active", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("Yes", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsVeteranValue(string? value)
        {
            return value != null
                && (value.Equals("Veteran", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("Yes", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsUnknownValue(string? value)
        {
            return value != null && value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            if (value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                return "Active";

            if (value.Equals("No", StringComparison.OrdinalIgnoreCase))
                return "No";

            if (value.Equals("Active", StringComparison.OrdinalIgnoreCase))
                return "Active";

            if (value.Equals("Veteran", StringComparison.OrdinalIgnoreCase))
                return "Veteran";

            if (value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                return "Unknown";

            return value.Trim();
        }

        private static string ToMilitaryVeteranFlag(string militaryService)
        {
            return militaryService switch
            {
                "Veteran" => "Yes",
                "Unknown" => "Unknown",
                _ => "No"
            };
        }

        private static string ToActiveMilitaryFlag(string militaryService)
        {
            return militaryService switch
            {
                "Active" => "Yes",
                "Unknown" => "Unknown",
                _ => "No"
            };
        }

        private static string NullToEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
