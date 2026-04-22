using System.ComponentModel.DataAnnotations;

namespace MedInsights.Lib.Dtos
{
    public class PatientFamilyMedicalHistoryDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        [Required] public DateTime DateNoted { get; set; }
        [Required] public string Description { get; set; } = string.Empty;
        [Required] public string Relationship { get; set; } = string.Empty;
    }
}
