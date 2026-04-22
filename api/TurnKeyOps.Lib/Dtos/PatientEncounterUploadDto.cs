using Microsoft.AspNetCore.Http;

namespace MedInsights.Lib.Dtos
{
    public class PatientEncounterUploadRequest
    {
        public IFormFile File { get; set; } = default!;
        public Guid? EncounterId { get; set; }
        public Guid? PatientId { get; set; }
    }
}
