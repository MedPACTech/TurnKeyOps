using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MedInsights.Lib.Dtos
{
    public class PatientMilitaryFirstResponderDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        [Required] public string MilitaryService { get; set; } = string.Empty;
        public string? Branch { get; set; }
        [JsonConverter(typeof(NullableDateOnlyConverter))]
        public DateOnly? DateDischarged { get; set; }

        [JsonConverter(typeof(NullableDateOnlyConverter))]
        public DateOnly? DateEnlisted { get; set; }
        public string? MilitaryId { get; set; }

        [Required] public string FirstResponder { get; set; } = string.Empty;
        public string? FirstResponderType { get; set; }
        public string? FirstResponderDepartment { get; set; }
        public string? FirstResponderStation { get; set; }

        [Required] public string LawEnforcement { get; set; } = string.Empty;
        public string? LawEnforcementType { get; set; }
        public string? LawEnforcementAgency { get; set; }
        public string? LawEnforcementId { get; set; }
    }
}
