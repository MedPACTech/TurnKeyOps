using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MedInsights.Lib.Dtos
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; } = default!;
        public Guid ChatId { get; set; } = default!;
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public int TokensUsed { get; set; }
    }
}