using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MedInsights.Lib.Utils;

namespace MedInsights.Lib.Dtos
{
    public class PatientOrderDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        [Required]
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly DateOrdered { get; set; }
        [Required] public Guid OrderingProviderId { get; set; }
        [Required] public string OrderingProviderName { get; set; } = string.Empty;
        [Required] public string LabProvider { get; set; } = string.Empty;
        [Required] public string LabType { get; set; } = string.Empty;
        [Required] public Guid LabId { get; set; }
        public bool IsComplete { get; set; }
    }
}
