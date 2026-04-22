using System.ComponentModel.DataAnnotations;

namespace MedInsights.Lib.Dtos
{
    public class PatientMedicationDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        [Required] public Guid ProviderId { get; set; }
        [Required] public Guid PrescriberId { get; set; }
        [Required] public string PrescriberName { get; set; } = string.Empty;
        [Required] public DateTime DateNoted { get; set; }
        [Required] public Guid MedicationId { get; set; }
        [Required] public string Medication { get; set; } = string.Empty;
        [Required] public string Strength { get; set; } = string.Empty;
        [Required] public string Route { get; set; } = string.Empty;
        [Required] public string Frequency { get; set; } = string.Empty;
        public bool IsEnded { get; set; }
        [Required] public DateTime DateEnded { get; set; }
        public bool IsLocked { get; set; }
    }
}
