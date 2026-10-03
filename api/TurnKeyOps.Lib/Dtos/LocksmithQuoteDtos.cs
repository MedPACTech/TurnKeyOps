namespace TurnKeyOps.Lib.Dtos;

public sealed class LocksmithQuoteInputDto
{
    public string JobType { get; set; } = string.Empty;
    public List<LocksmithOpeningDto> Openings { get; set; } = [];
    public List<LocksmithQuoteItemInputDto> Items { get; set; } = [];
    public decimal LaborHours { get; set; }
    public string? LaborOverrideReason { get; set; }
    public decimal DiscountPercent { get; set; }
}
public sealed class LocksmithQuoteItemInputDto
{
    public string CatalogItemId { get; set; } = string.Empty;
    public string OpeningName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
public sealed class LocksmithCatalogItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> JobTypes { get; set; } = [];
    public decimal UnitPrice { get; set; }
    public bool Sample { get; set; } = true;
}
public sealed class LocksmithQuoteContextDto
{
    public List<string> JobTypes { get; set; } = [];
    public List<LocksmithCatalogItemDto> Catalog { get; set; } = [];
    public string PolicyVersion { get; set; } = string.Empty;
    public decimal? LaborRatePerHour { get; set; }
    public decimal? TaxPercent { get; set; }
    public Dictionary<string, Dictionary<string, decimal>> LaborHoursByJobType { get; set; } = [];
}
public sealed class LocksmithPricedLineDto
{
    public string CatalogItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OpeningName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
public sealed class LocksmithPricingSnapshotDto
{
    public string JobType { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public List<LocksmithOpeningDto> Openings { get; set; } = [];
    public List<LocksmithPricedLineDto> Lines { get; set; } = [];
    public decimal LaborHours { get; set; }
    public decimal StandardLaborHours { get; set; }
    public string? LaborOverrideReason { get; set; }
    public decimal LaborRatePerHour { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public List<string> ApprovalReasons { get; set; } = [];
    public bool RequiresOfficeApproval => ApprovalReasons.Count > 0;
    public DateTime? OfficeApprovedAtUtc { get; set; }
    public string? OfficeApprovedBy { get; set; }
}

public sealed class LocksmithOpeningDto
{
 public string Id { get; set; } = string.Empty;
 public string Name { get; set; } = string.Empty;
 public string Service { get; set; } = string.Empty;
 public Dictionary<string, LocksmithMeasurementDto> Measurements { get; set; } = [];
 public string Handing { get; set; } = string.Empty;
 public string Notes { get; set; } = string.Empty;
 public string CommercialNotes { get; set; } = string.Empty;
}
public sealed class LocksmithMeasurementDto
{
 public string Value { get; set; } = string.Empty;
 public string Certainty { get; set; } = string.Empty;
}
