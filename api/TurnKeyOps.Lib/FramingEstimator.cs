namespace TurnKeyOps.Lib.Utils;

/// <summary>
/// Framing estimation formulas.
/// Studs = wallLength × 0.75 (16" OC)
/// Plates = wallLength × 3 (bottom + double top)
/// Sheathing sheets = area / 32
/// </summary>
public static class FramingEstimator
{
    /// <summary>Number of studs for 16" OC spacing.</summary>
    public static int CalculateStudCount(double wallLengthFeet)
        => (int)Math.Ceiling(wallLengthFeet * 0.75) + 1; // +1 for end stud

    /// <summary>Linear feet of plate material (1 bottom + 2 top).</summary>
    public static double CalculatePlateLinearFeet(double wallLengthFeet)
        => wallLengthFeet * 3;

    /// <summary>Number of 4×8 sheathing sheets.</summary>
    public static int CalculateSheathingSheets(double wallAreaSqft)
        => (int)Math.Ceiling(wallAreaSqft / 32.0);

    /// <summary>Number of headers needed (one per opening).</summary>
    public static int CalculateHeaders(int doorCount, int windowCount)
        => doorCount + windowCount;
}
