namespace TurnKeyOps.Lib.Dtos;

public class TurnKeyChatDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}
