using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MedInsights.Lib.Utils;

namespace MedInsights.Lib.Dtos
{
    public class PatientLabsDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        [Required] public Guid DocumentId { get; set; }
        [Required] public string LabType { get; set; } = string.Empty;
        [Required] public string LabProvider { get; set; } = string.Empty;
        [Required]
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly DateLabCompleted { get; set; }
        [Required] public string LabStatus { get; set; } = string.Empty;
        public DateTime? DateUploaded { get; set; }
    }
}
