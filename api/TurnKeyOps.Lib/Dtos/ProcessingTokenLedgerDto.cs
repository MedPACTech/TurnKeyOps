using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedInsights.Lib.Dtos
{
    public class ProcessingTokenLedgerDto
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public Guid MessageId { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public int TokensCredited { get; set; }
        public int TokensDebited { get; set; }
        public string TokenType { get; set; } = default!;
        public string Description { get; set; } = default!;
        public DateTime RequestedAt { get; set; }
    }

}