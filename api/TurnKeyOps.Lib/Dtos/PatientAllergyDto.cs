using System.ComponentModel.DataAnnotations;

namespace MedInsights.Lib.Dtos
{
    public class PatientAllergyDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        [Required] public string AllergyType { get; set; } = string.Empty;
        public string? Severity { get; set; }
        [Required] public string Description { get; set; } = string.Empty;
        public string? Reaction { get; set; }
        [Required] public DateTime DateNoted { get; set; }
    }
}
