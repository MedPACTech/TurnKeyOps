using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MedInsights.Lib.Dtos
{
    public class ChatDto
    {
        public Guid Id { get; set; } = default!;
        public string Title { get; set; } = "";
        public List<Guid> AttachedDocuments { get; set; } = new List<Guid>();
        public string Summary { get; set; } = "";
        public DateTime UpdatedUtc { get; set; }
        public int TokensUsed { get; set; }
        public Guid? PatientId{ get; set; } = default!;
    }
}