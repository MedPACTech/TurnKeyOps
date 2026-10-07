namespace TurnKeyOps.Lib.Dtos;

// Separate domain records share a versioned commit boundary, never a mutable stock quantity.
public sealed class SupplyState
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public string? PreviousBlob { get; set; }
    public List<CatalogItem> Catalog { get; set; } = [];
    public List<InventoryLocation> Locations { get; set; } = [];
    public List<VendorSupplyProfile> Vendors { get; set; } = [];
    public List<VendorItem> VendorItems { get; set; } = [];
    public List<MaterialDemand> Demands { get; set; } = [];
    public List<InventoryMovement> Movements { get; set; } = [];
    public List<StockReservation> Reservations { get; set; } = [];
    public List<PurchaseRequest> Requests { get; set; } = [];
    public List<PurchaseOrder> Orders { get; set; } = [];
    public List<SupplyReceipt> Receipts { get; set; } = [];
    public List<SupplyAudit> Audit { get; set; } = [];
    public SupplyPolicy Policy { get; set; } = new();
}
public sealed class CatalogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Kind { get; set; } = "MATERIAL";
    public string Category { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Unit { get; set; } = "each";
    public bool Active { get; set; } = true;
    public bool Stocked { get; set; } = true;
    public bool JobSpecific { get; set; }
    public decimal? DefaultCost { get; set; }
    public decimal? DefaultSellPrice { get; set; }
    public Guid? PreferredVendorId { get; set; }
    public string[] Trades { get; set; } = [];
    public Dictionary<string,string> Metadata { get; set; } = [];
    public Dictionary<string,string> ExternalIds { get; set; } = [];
    // Exact item-specific conversion to base unit; never inferred by AI.
    public Dictionary<string,decimal> UnitConversions { get; set; } = [];
    public string SourceSystem { get; set; } = "manual";
    public string ExternalId { get; set; } = "";
}
public sealed class InventoryLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Type { get; set; } = "warehouse";
    public bool Active { get; set; } = true;
    public Guid? ParentId { get; set; }
    public Guid? JobId { get; set; }
    public string Address { get; set; } = "";
    public string Responsible { get; set; } = "";
}
public sealed class VendorSupplyProfile
{
    public Guid ContactId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public bool Preferred { get; set; }
    public string VendorNumber { get; set; } = "";
    public string OrderingEmail { get; set; } = "";
    public string OrderingPhone { get; set; } = "";
    public string Terms { get; set; } = "";
    public string Notes { get; set; } = "";
    public string[] Trades { get; set; } = [];
    public int LeadDays { get; set; }
    public bool Delivers { get; set; }
}
public sealed class VendorItem
{
    public string ItemId { get; set; } = "";
    public Guid VendorId { get; set; }
    public string VendorItemNumber { get; set; } = "";
    public string Unit { get; set; } = "each";
    public decimal PackSize { get; set; } = 1;
    public decimal? Cost { get; set; }
    public int LeadDays { get; set; }
    public bool Preferred { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
public sealed class MaterialDemand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Guid RequirementId { get; set; }
    public string JobName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ItemId { get; set; }
    public string Trade { get; set; } = "general";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "each";
    public DateTime? RequiredAtUtc { get; set; }
    public Guid? RequiredEventId { get; set; }
    public string Strategy { get; set; } = "";
    public string Gate { get; set; } = "blocker";
    public string Source { get; set; } = "Job requirement";
    public bool Cancelled { get; set; }
    public string OverrideReason { get; set; } = "";
}
public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ItemId { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "each";
    public string Type { get; set; } = "";
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? DemandId { get; set; }
    public Guid? SourceId { get; set; }
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime AtUtc { get; set; }
}
public sealed class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandId { get; set; }
    public string ItemId { get; set; } = "";
    public Guid LocationId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "active";
    public DateTime? RequiredAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
public sealed class PurchaseRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "REQUESTED";
    public string Reason { get; set; } = "";
    public string Requester { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public DateTime? RequiredAtUtc { get; set; }
    public bool Rush { get; set; }
}
public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SupplyDispatch? Dispatch { get; set; }
    public string ApprovalPolicyHash { get; set; } = "";
    public List<SupplyFile> Files { get; set; } = [];
    public string Number { get; set; } = "";
    public Guid VendorId { get; set; }
    public Guid? LocationId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTime? ExpectedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Buyer { get; set; } = "";
    public string VendorReference { get; set; } = "";
    public string DeliveryInstructions { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public List<string> ApprovalReasons { get; set; } = [];
    public List<PurchaseOrderLine> Lines { get; set; } = [];
    public decimal Shipping { get; set; }
    public decimal Tax { get; set; }
    public decimal Total => Lines.Sum(l=>l.Quantity*l.UnitCost)+Shipping+Tax;
}
public sealed class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? RequestId { get; set; }
    public Guid DemandId { get; set; }
    public string? ItemId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "each";
    public decimal UnitCost { get; set; }
    public bool DirectToJob { get; set; }
}
public sealed class SupplyReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid LineId { get; set; }
    public decimal Accepted { get; set; }
    public decimal Damaged { get; set; }
    public Guid? LocationId { get; set; }
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public string Actor { get; set; } = "";
    public List<SupplyFile> Files { get; set; } = [];
}
public sealed class SupplyFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string BlobName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
public sealed class SupplyPolicy
{
    public decimal ApprovalAbove { get; set; } = 1000;
    public bool NonPreferredApproval { get; set; } = true;
    public bool RushApproval { get; set; } = true;
    public bool PriceOverrideApproval { get; set; } = true;
    public Dictionary<string,decimal> SpendingLimits { get; set; } = [];
    public Dictionary<string,string> AiActions { get; set; } = [];
    public Dictionary<string,SupplyTradeProfile> Trades { get; set; } = new()
    {
        ["concrete"] = new(){DefaultStrategy="direct",PreferredUnits=["cubic-yard","ton"],Fields=["mix","strength","slump"],DeliveryRequired=true},
        ["doors-locks"] = new(){Fields=["manufacturer","model","finish","handing","keying","dimensions","compatibility"]},
        ["framing"] = new(){Fields=["grade","dimensions","treatment"]},
        ["land-clearing"] = new(){PreferredUnits=["gallon","load","ton","hour"],Fields=["specification"]},
        ["general"] = new()
    };
}
public sealed class SupplyTradeProfile
{
    public string DefaultStrategy { get; set; } = "stock";
    public string Gate { get; set; } = "blocker";
    public string[] PreferredUnits { get; set; } = ["each"];
    public string[] Fields { get; set; } = [];
    public bool DeliveryRequired { get; set; }
}
public sealed record SupplyAudit(Guid Id,string Action,string Actor,DateTime AtUtc,string Reason,string Target="",string Fingerprint="");
public sealed record SupplyBalance(string ItemId,Guid LocationId,decimal OnHand,decimal Reserved,decimal Available);
public sealed record SupplyReadiness(Guid DemandId,string Status,decimal Required,decimal Reserved,decimal Received,decimal Consumed,decimal Shortage,string Gate,string[] Reasons);
public sealed class SupplyCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public Guid? JobId { get; set; }
    public string ItemId { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Damaged { get; set; }
    public string Unit { get; set; } = "each";
    public string Reason { get; set; } = "";
    public string State { get; set; } = "";
    public string Reference { get; set; } = "";
    public DateTime? ExpectedAtUtc { get; set; }
    public CatalogItem? Item { get; set; }
    public InventoryLocation? Location { get; set; }
    public VendorSupplyProfile? Vendor { get; set; }
    public VendorItem? Offer { get; set; }
    public MaterialDemand? Demand { get; set; }
    public PurchaseOrder? Order { get; set; }
    public SupplyPolicy? Policy { get; set; }
}

public sealed class SupplyDispatch
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "sending";
    public string Recipient { get; set; } = "";
    public string Body { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public DateTime? ProviderAcceptedAtUtc { get; set; }
}
