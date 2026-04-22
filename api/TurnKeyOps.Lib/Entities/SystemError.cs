using Azure;
using Azure.Data.Tables;

namespace MedInsights.Lib.Entities
{

    public class SystemError : ITableEntity
    {
        public string PartitionKey { get; set; } = DateTime.UtcNow.ToString("yyyyMMdd"); // group by day
        public string RowKey { get; set; } = Guid.NewGuid().ToString();

        public string Path { get; set; }
        public string Method { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string TraceId { get; set; }

        public DateTimeOffset? Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public ETag ETag { get; set; }

    }
}
