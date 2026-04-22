namespace TurnKeyOps.Lib.Dtos;

public class StructuredEstimateInputDto
{
    public string? ProjectType { get; set; }
    public double? LengthFt { get; set; }
    public double? WidthFt { get; set; }
    public double? DepthIn { get; set; }
    public double? WastePercent { get; set; }
    public int? PourCount { get; set; }
    public bool? DemoRequired { get; set; }
    public bool? ExcavationRequired { get; set; }
    public bool? PumpRequired { get; set; }
    public string? ReinforcementType { get; set; }
    public string? FinishType { get; set; }
}
