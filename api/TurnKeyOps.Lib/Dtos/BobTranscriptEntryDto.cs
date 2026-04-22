namespace TurnKeyOps.Lib.Dtos;

public class BobTranscriptEntryDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
}
