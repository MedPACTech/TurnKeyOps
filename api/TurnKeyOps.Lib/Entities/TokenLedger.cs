using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class TokenLedger : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public string TokenType { get; set; } = string.Empty;
        public int TokensCredited { get; set; }
        public int TokensDebited { get; set; }
        public string Description { get; set; } = string.Empty;
        public int BalanceAfterTransaction { get; set; }
    }
}
