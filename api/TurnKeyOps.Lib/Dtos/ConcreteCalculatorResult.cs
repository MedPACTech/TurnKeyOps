namespace TurnKeyOps.Lib.Dtos;

public class ConcreteCalculatorResult
{
    public double Sqft { get; set; }
    public double DepthInches { get; set; }
    public double CubicYards { get; set; }
    public double CubicYardsPerPour { get; set; }
    public double RebarLinearFeet { get; set; }
    public double FormBoardLinearFeet { get; set; }
    public decimal EstimatedMaterialCost { get; set; }
    public decimal EstimatedLaborCost { get; set; }
    public decimal EstimatedTotal { get; set; }
    public int NumberOfPours { get; set; }
}
