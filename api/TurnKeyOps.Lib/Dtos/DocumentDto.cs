namespace MedInsights.Lib.Dtos
{
    public class DocumentDto
    {
        public Guid Id { get; set; } = default!;
        public string FileName { get; set; } = string.Empty;
        public string BlobUrl { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string Message { get; set; } = "File uploaded successfully.";
        public string Category { get; set; } = string.Empty;
        public Guid? PatientId { get; set; }
        public Guid? ChatId { get; set; }
        public Guid UserId { get; set; } = default!;
    }
}
