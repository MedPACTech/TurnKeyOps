using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedInsights.Lib.Dtos
{
 public class TokenLedgerDto
    {
        public Guid Id { get; set; }                // Public-facing identifier
        public Guid TenantId { get; set; }          // Tenant association
        public Guid UserId { get; set; }            // User affected
        public DateTime Date { get; set; }            // Transaction timestamp
        public string TokenType { get; set; }         // e.g., "Purchase", "Usage", "Bonus"
        public int TokensCredited { get; set; }       // Tokens added
        public int TokensDebited { get; set; }        // Tokens removed
        public string Description { get; set; }       // Narrative for the transaction
        public int BalanceAfterTransaction { get; set; } // Running balance
    }
}