using System.Runtime.ExceptionServices;
using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities.Interfaces;

namespace MedInsights.Lib.Entities
{
    public class PromptTemplate : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public string Entity { get; set; }
        public string PromptTemplateName { get; set; }
        public string Action { get; set; }
        public string Prompt { get; set; }
    }
}
