namespace TurnKeyOps.Lib.Utils;

/// <summary>
/// Concrete estimation formulas.
/// CY = (Length_ft × Width_ft × Depth_in / 12) / 27
/// </summary>
public static class ConcreteEstimator
{
    /// <summary>Calculate cubic yards from square footage and depth.</summary>
    public static double CalculateCubicYards(double sqft, double depthInches, double wastePercent = 0.05)
    {
        var volumeCuFt = sqft * (depthInches / 12.0);
        var cubicYards = volumeCuFt / 27.0;
        return Math.Ceiling(cubicYards * (1 + wastePercent) * 10) / 10; // round up to 0.1
    }

    /// <summary>Estimate rebar quantity (sqft-based, 12" grid).</summary>
    public static double EstimateRebarLinearFeet(double sqft)
    {
        // Rough: sqrt(sqft) for each direction × 2, plus 10% overlap
        var side = Math.Sqrt(sqft);
        var barsPerDirection = Math.Ceiling(side); // 1ft spacing
        var totalLf = barsPerDirection * side * 2 * 1.1;
        return Math.Ceiling(totalLf);
    }

    /// <summary>Estimate form boards (perimeter-based).</summary>
    public static double EstimateFormBoardLinearFeet(double sqft)
    {
        // Assume roughly square: perimeter ≈ 4 × sqrt(area)
        return Math.Ceiling(4 * Math.Sqrt(sqft) * 1.1); // 10% waste
    }

    /// <summary>Quick price estimate with typical rates.</summary>
    public static decimal QuickPriceEstimate(double sqft, double depthInches,
        decimal readyMixPerCy = 165m, decimal laborPerSqft = 4m, decimal rebarPerSqft = 0.75m)
    {
        var cy = CalculateCubicYards(sqft, depthInches);
        var materialCost = (decimal)cy * readyMixPerCy;
        var laborCost = (decimal)sqft * laborPerSqft;
        var rebarCost = (decimal)sqft * rebarPerSqft;
        return Math.Round(materialCost + laborCost + rebarCost, 2);
    }
}
