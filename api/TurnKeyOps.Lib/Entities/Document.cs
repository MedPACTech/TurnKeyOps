using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Lib.Entities
{
    public class Document : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid UserId { get; set; }
        public Guid? PatientId { get; set; }
        public Guid? ChatId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string? BlobUrl { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public IFormFile? File { get; set; }
        public string Category { get; set; } = string.Empty;

        public string? TextContent { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public string? TextExtractionError { get; set; }
        public string? DetectedContainerType { get; set; }
        public string? ContentNature { get; set; }
    }
}
