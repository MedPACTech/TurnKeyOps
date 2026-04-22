using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MedInsights.Lib.Utils;

namespace MedInsights.Lib.Dtos
{
    public class PatientMaritalHistoryDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        public DateTime DateNoted { get; set; }
        [Required] public string MaritalStatus { get; set; } = string.Empty;

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly? DateMarried { get; set; }
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly? DateDivorced { get; set; }
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly? DateWidowed { get; set; }

        public string? SpouseName { get; set; }
        public string? HasChildren { get; set; }
        public int? NumberOfChildren { get; set; }
        public string? Notes { get; set; }
    }
}
