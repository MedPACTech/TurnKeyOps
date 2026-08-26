namespace TurnKeyOps.Lib.Configurations;

public sealed class BobOperationsOptions
{
    public const string SectionName = "BobOperations";
    public bool Enabled { get; set; }
    public bool WriteActionsEnabled { get; set; }
    public int MaxStoredInputCharacters { get; set; } = 8_000;
}
