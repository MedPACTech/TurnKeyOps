using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MedInsights.Lib.Utils;

namespace MedInsights.Lib.Dtos
{
    public class PatientDiagnosisDto
    {
        public Guid Id { get; set; }

        [Required] public Guid PatientId { get; set; }
        [Required] public Guid DiagnosisCodeId { get; set; }
        public string DiagnosisCode { get; set; } = string.Empty;

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly? DateDiagnosed { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int DiagnosisStatusId { get; set; }
        public string DiagnosisStatus { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string LongDescription { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }
}
