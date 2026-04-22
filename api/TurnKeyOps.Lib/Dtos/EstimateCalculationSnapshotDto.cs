namespace TurnKeyOps.Lib.Dtos;

public class EstimateCalculationSnapshotDto
{
    public double SquareFeet { get; set; }
    public double CubicFeet { get; set; }
    public double CubicYards { get; set; }
    public double CubicYardsWithWaste { get; set; }
    public decimal ConcreteMaterialCost { get; set; }
    public decimal DeliveredConcreteCost { get; set; }
    public decimal SitePrepSubtotal { get; set; }
    public decimal ReinforcementSubtotal { get; set; }
    public decimal FormworkSubtotal { get; set; }
    public decimal FinishSubtotal { get; set; }
    public decimal TotalLaborHours { get; set; }
    public decimal RegularLaborCost { get; set; }
    public decimal OvertimeLaborCost { get; set; }
    public decimal LaborSubtotal { get; set; }
    public decimal EquipmentSubtotal { get; set; }
    public decimal DirectCost { get; set; }
    public decimal OverheadAmount { get; set; }
    public decimal ContingencyAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalEstimatedPrice { get; set; }
    public decimal PricePerSquareFoot { get; set; }
    public decimal PricePerYard { get; set; }
}
