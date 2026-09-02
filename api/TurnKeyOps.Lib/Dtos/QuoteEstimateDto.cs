namespace TurnKeyOps.Lib.Dtos;

public sealed class QuoteEstimateDto
{
    public Guid Id { get; set; }
    public Guid QuoteRequestId { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string CustomerName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceSummary { get; set; } = string.Empty;
    public string VisitFindings { get; set; } = string.Empty;
    public List<string> ScopeLineItems { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public List<string> Assumptions { get; set; } = [];
    public string Status { get; set; } = "draft";
    public string CommercialSummary { get; set; } = string.Empty;
    public List<QuoteEstimateLocationDto> Locations { get; set; } = [];
    public QuoteEstimateTotalsDto Totals { get; set; } = new();
    public DateTime SavedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? SentBy { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public QuoteEstimateDeliveryDto? Delivery { get; set; }
    public List<QuoteEstimateRevisionDto> RevisionHistory { get; set; } = [];
    public string Version { get; set; } = string.Empty;
}

public sealed class QuoteEstimateDraftInputDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceSummary { get; set; } = string.Empty;
    public string VisitFindings { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public List<QuoteEstimateLocationDto> Locations { get; set; } = [];
    public string? ExpectedVersion { get; set; }
}

public sealed class QuoteEstimateLocationDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double LengthFeet { get; set; }
    public double WidthFeet { get; set; }
    public double DepthInches { get; set; } = 4;
    public double WastePercent { get; set; } = 10;
    public int NumberOfPours { get; set; } = 1;
    public double SquareFeet { get; set; }
    public double CubicYards { get; set; }
    public double FormLinearFeet { get; set; }
    public double RebarLinearFeet { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal EstimatedTotal { get; set; }
}

public sealed class QuoteEstimateTotalsDto
{
    public double SquareFeet { get; set; }
    public double CubicYards { get; set; }
    public double FormLinearFeet { get; set; }
    public double RebarLinearFeet { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal EstimatedTotal { get; set; }
}

public sealed class QuoteEstimateDeliveryDto
{
    public string Status { get; set; } = "sent";
    public string Method { get; set; } = "review-link";
    public string ReviewUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ChangesRequestedAtUtc { get; set; }
    public string? ResponseNote { get; set; }
}

public sealed class QuoteEstimateRevisionDto
{
    public int RevisionNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceSummary { get; set; } = string.Empty;
    public string VisitFindings { get; set; } = string.Empty;
    public List<string> ScopeLineItems { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public List<string> Assumptions { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public string CommercialSummary { get; set; } = string.Empty;
    public List<QuoteEstimateLocationDto> Locations { get; set; } = [];
    public QuoteEstimateTotalsDto Totals { get; set; } = new();
    public DateTime SavedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? SentBy { get; set; }
}

public sealed class QuoteEstimateDecisionDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string? ResponseNote { get; set; }
}
