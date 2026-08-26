namespace TurnKeyOps.Lib.Dtos;

public class TurnKeyChatDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Mode { get; set; } = "general";
    public string StateJson { get; set; } = "{}";
    public int MessageCount { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
}

public sealed class CreateTurnKeyChatDto
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = "New conversation";
    public string Mode { get; set; } = "general";
    public string StateJson { get; set; } = "{}";
}

public sealed class UpdateTurnKeyChatDto
{
    public string Title { get; set; } = string.Empty;
    public string Mode { get; set; } = "general";
    public string StateJson { get; set; } = "{}";
    public bool Archived { get; set; }
}
