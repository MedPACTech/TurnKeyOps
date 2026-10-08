namespace TurnKeyOps.Lib.Dtos;

// Optional extension of the existing packet, never a parallel estimate aggregate.
public sealed class EstimateDocumentDto
{
    public string TradeProfile { get; set; } = "general";
    public Guid? SiteId { get; set; }
    public Guid? OwnerMembershipId { get; set; }
    public string Scope { get; set; } = "";
    public string Terms { get; set; } = "";
    public string Exclusions { get; set; } = "";
    public string Timing { get; set; } = "";
    public int ValidDays { get; set; } = 30;
    public decimal DepositPercent { get; set; }
    public string Source { get; set; } = "";
    public string Referral { get; set; } = "";
    public Dictionary<string,string> LeadContext { get; set; } = [];
    public Dictionary<string,EstimateInputValueDto> Inputs { get; set; } = [];
    public List<EstimateSuggestionDto> Suggestions { get; set; } = [];
    public List<EstimateOptionDto> Options { get; set; } = [new()];
    public List<EstimateAttachmentDto> Attachments { get; set; } = [];
    public string NarrativeSource { get; set; } = "Lead scope; estimator review required";
    public string CompanyName { get; set; } = "";
}
public sealed class EstimateInputValueDto { public decimal Value { get; set; } public bool Confirmed { get; set; } public string Provenance { get; set; } = "Estimator"; }
public sealed class EstimateSuggestionDto { public string Key { get; set; } = ""; public string Text { get; set; } = ""; public decimal? Value { get; set; } public string Provenance { get; set; } = ""; }
public sealed class EstimateAttachmentDto { public string BlobName { get; set; } = ""; public string ContentType { get; set; } = "application/octet-stream"; public long SizeBytes { get; set; } public Guid Id { get; set; } public string Name { get; set; } = ""; public string ContentHash { get; set; } = ""; }
public sealed class EstimateOptionDto
{
    public string ExclusiveGroup { get; set; } = "";
    public string Id { get; set; } = "base";
    public string Name { get; set; } = "Base scope";
    public bool Required { get; set; } = true;
    public List<EstimateSelectionDto> Items { get; set; } = [];
}
public sealed class EstimateSelectionDto
{
    public string CatalogId { get; set; } = "";
    public decimal Quantity { get; set; }
    public string QuantityKey { get; set; } = "manual";
    public bool Confirmed { get; set; }
    public decimal? OverridePrice { get; set; }
    public string OverrideReason { get; set; } = "";
}
public sealed class EstimateCatalogItemDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "material";
    public string Unit { get; set; } = "each";
    public string TradeProfile { get; set; } = "general";
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? MarkupPercent { get; set; }
    public bool Enabled { get; set; }
    public bool Taxable { get; set; } = true;
    public bool Sample { get; set; }
}
public sealed class EstimatePricingPolicyDto
{
    public string CompanyName { get; set; } = "";
    public string PublicOrigin { get; set; } = "";
    public List<EstimateCatalogItemDto> Catalog { get; set; } = [];
    public decimal? TaxPercent { get; set; }
    public decimal MaxDiscountPercent { get; set; }
    public decimal? MinimumMarginPercent { get; set; }
    public decimal? ApprovalAboveTotal { get; set; }
    public decimal MinimumCharge { get; set; }
    public decimal DepositPercent { get; set; }
    public bool AllowManualOverrides { get; set; }
    public string Terms { get; set; } = "";
    public Dictionary<string,decimal> CustomerDiscounts { get; set; } = [];
    public Dictionary<string,decimal> CustomerPrices { get; set; } = [];
    public Dictionary<string,Dictionary<string,string>> RequiredFields { get; set; } = [];
    public Dictionary<string,string> StageLabels { get; set; } = [];
}
public sealed class EstimatePriceLineDto
{
    public string CatalogId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public decimal? Cost { get; set; }
    public bool Taxable { get; set; }
    public string Rule { get; set; } = "";
}
public sealed class EstimateOptionPriceDto
{
    public string ExclusiveGroup { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Required { get; set; }
    public List<EstimatePriceLineDto> Lines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal? Cost { get; set; }
    public decimal? MarginPercent { get; set; }
}
public sealed class EstimatePricingDto
{
    public string RuleVersion { get; set; } = "";
    public DateTime CalculatedAtUtc { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public List<EstimateOptionPriceDto> Options { get; set; } = [];
    public List<string> Blockers { get; set; } = [];
    public List<string> ApprovalReasons { get; set; } = [];
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public decimal BaseTotal => Options.Where(x => x.Required).Sum(x => x.Total);
}
public sealed class EstimateWorkspaceInputDto
{
    public string ExpectedVersion { get; set; } = "";
    public EstimateDocumentDto Document { get; set; } = new();
    public decimal DiscountPercent { get; set; }
}
public sealed class EstimateTaskDto { public string ExpectedVersion { get; set; } = ""; public string Text { get; set; } = ""; public string Channel { get; set; } = "email"; }
public sealed class EstimateEventDto { public string Type { get; set; } = ""; public string Text { get; set; } = ""; public string Actor { get; set; } = ""; public int Revision { get; set; } public DateTime AtUtc { get; set; } = DateTime.UtcNow; }
public sealed record EstimateFieldDto(string Key, string Label, string Unit);
public sealed class EstimateWorkspaceDto
{
    public QuoteEstimateDto Packet { get; set; } = new();
    public IReadOnlyList<EstimateFieldDto> Fields { get; set; } = [];
    public List<EstimateCatalogItemDto> Catalog { get; set; } = [];
    public string State { get; set; } = "DRAFT";
    public string StateLabel { get; set; } = "";
    public string NextAction { get; set; } = "Complete scope";
    public bool CanWrite { get; set; }
    public bool CanApprove { get; set; }
    public bool CanViewCosts { get; set; }
}
