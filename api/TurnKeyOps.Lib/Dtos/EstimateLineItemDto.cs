namespace TurnKeyOps.Lib.Dtos;

public class EstimateLineItemDto
{
    public Guid Id { get; set; }
    public Guid EstimateId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public double Quantity { get; set; }
    public string Unit { get; set; } = "ea";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsCalculated { get; set; }
    public string? Notes { get; set; }
}
