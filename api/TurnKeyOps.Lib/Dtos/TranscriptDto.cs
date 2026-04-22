
namespace MedInsights.Lib.Dtos
{
    public class TranscriptDto
    {
        public Guid Id { get; set; } = default!;

        public Guid ChatId { get; set; } = default!;

        public string Role { get; set; } = "";  // "user" | "assistant" | "system"

        public string Content { get; set; } = "";

        public DateTime DateAt { get; set; }
            
        public int TokensUsed { get; set; }
    }
}