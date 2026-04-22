using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MedInsights.Lib
{
    public sealed class ConversationSummaryDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime LastMessageAt { get; set; }
        public string ConversationType { get; set; } = "chat"; // chat | summary | report
    }
}
