namespace TurnKeyOps.Lib.Dtos;

/// <summary>Input for the concrete CY calculator.</summary>
public class ConcreteCalculatorRequest
{
    public double LengthFeet { get; set; }
    public double WidthFeet { get; set; }
    public double DepthInches { get; set; } = 4;
    public double WastePercent { get; set; } = 0.05;
    public int NumberOfPours { get; set; } = 1;
    public decimal? ReadyMixPricePerCy { get; set; }
    public decimal? LaborPricePerSqft { get; set; }
    public decimal? RebarPricePerSqft { get; set; }
}
