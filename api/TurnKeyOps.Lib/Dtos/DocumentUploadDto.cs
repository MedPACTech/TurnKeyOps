using Microsoft.AspNetCore.Http;

namespace MedInsights.Lib.Dtos
{
    public class DocumentUploadDto
    {
        public Guid UserId { get; set; } = default!;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public IFormFile File { get; set; }
        public string? Category { get; set; }
        public Guid? PatientId { get; set; }
        public Guid? ChatId { get; set; }
    }
}
