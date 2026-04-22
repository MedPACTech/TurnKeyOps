namespace TurnKeyOps.Lib.Dtos;

public class TurnKeyChatMessageDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? DateCreated { get; set; }
}
