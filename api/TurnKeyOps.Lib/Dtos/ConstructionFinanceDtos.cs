namespace TurnKeyOps.Lib.Dtos;
public sealed class ConstructionFinanceState
{
    public List<ConstructionCodeBudgetRevision> CodeBudgets { get; set; } = [];
    public List<ConstructionCommitmentCoding> CommitmentCodings { get; set; } = [];
    public List<ConstructionContractRevision> ContractRevisions { get; set; } = [];
    public List<ConstructionCustomerApproval> CustomerApprovals { get; set; } = [];
    public List<ConstructionMigrationBaseline> OpeningBalances { get; set; } = [];
    public List<ConstructionContract> Contracts { get; set; } = [];
    public List<ConstructionPayApplication> Applications { get; set; } = [];
    public List<ConstructionForecast> Forecasts { get; set; } = [];
    public List<ConstructionWipSnapshot> WipSnapshots { get; set; } = [];
    public List<ConstructionRetainageRelease> RetainageReleases { get; set; } = [];
    public Dictionary<string,string> Accounts { get; set; } = [];
}
public sealed record ConstructionSovLine(string Id,string Description,decimal OriginalValue,decimal ApprovedChanges=0,string? CostCode=null)
{ public decimal CurrentValue=>OriginalValue+ApprovedChanges; }
public sealed record ConstructionContract(Guid JobId,Guid CustomerId,decimal OriginalValue,decimal ApprovedChanges,ConstructionSovLine[] Lines,decimal RetainagePercent=10,string RecognitionMethod="COST_TO_COST",bool ProgressBillingEnabled=true,bool Completed=false)
{ public decimal CurrentValue=>OriginalValue+ApprovedChanges; }
public sealed record ConstructionApplicationLine(string SovLineId,decimal CurrentBilling,decimal NewStoredMaterials=0,decimal InstalledFromStored=0,decimal? RetainageWithheld=null)
{
    public decimal BillableAmount=>CurrentBilling+NewStoredMaterials;
    public decimal CompletedWorkThisPeriod=>CurrentBilling+InstalledFromStored;
}
public sealed record ConstructionPayApplication(Guid Id,Guid JobId,string Number,DateOnly PeriodStart,DateOnly PeriodEnd,ConstructionApplicationLine[] Lines,string Status="DRAFT",decimal PriorBilled=0,decimal CurrentBilling=0,decimal Retainage=0,decimal TotalDue=0,Guid? JournalId=null,Guid? InvoiceId=null,string[]? Documents=null)
{ public bool IsPosted=>Status=="INVOICED"||JournalId!=null; }
public sealed record ConstructionForecast(Guid Id,Guid JobId,DateOnly Date,decimal RemainingForecast,bool Complete,string Reason,string Actor="",DateTime AtUtc=default,string BasisHash="",ConstructionForecastCost[]? Costs=null);
public sealed record ConstructionWipResult(Guid JobId,decimal ContractValue,decimal ActualCost,decimal RemainingCommitment,decimal? RemainingForecast,decimal? EstimatedFinalCost,decimal? PercentComplete,decimal? EarnedRevenue,decimal Billed,decimal RetainageHeld,decimal? Underbilling,decimal? Overbilling,decimal? ProjectedProfit,decimal? ProjectedMargin,string[] Exceptions,decimal CashCollected=0,decimal RecognizedToDate=0,decimal? Backlog=null);
public sealed record ConstructionWipSnapshot(Guid Id,string PeriodId,ConstructionWipResult[] Jobs,string Status,string Actor,DateTime AtUtc,Guid? JournalId=null,string SourcePayloadJson="",string SourceHash="");
public sealed record ConstructionRetainageRelease(Guid Id,Guid JobId,DateOnly Date,decimal Amount,Guid JournalId,Guid OpenItemId,string Reason);
public sealed class ConstructionFinanceCommand
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string ExpectedVersion {get;set;}="";
    public string Action {get;set;}="";
    public Guid JobId {get;set;}
    public Guid Target {get;set;}
    public string PeriodId {get;set;}="";
    public string Reason {get;set;}="";
    public DateOnly Date {get;set;}
    public decimal Amount {get;set;}
    public ConstructionContract? Contract {get;set;}
    public ConstructionPayApplication? Application {get;set;}
    public ConstructionForecast? Forecast {get;set;}
    public Dictionary<string,string>? Accounts {get;set;}
    public ConstructionCodeBudgetRevision? CodeBudget {get;set;}
    public ConstructionCommitmentCoding? CommitmentCoding {get;set;}
}

public sealed record ConstructionMigrationBaseline(Guid JobId,DateOnly Date,decimal Billed,decimal Retainage,decimal EarnedRevenue,string SourceId,ConstructionApplicationLine[] Lines);

public sealed record ConstructionCustomerApproval(Guid Id,Guid ApplicationId,Guid Actor,DateTime AtUtc,string Name);
public sealed record ConstructionPortalApproval(Guid Id,string ExpectedVersion,string Name);

public sealed record ConstructionContractRevision(Guid Id,ConstructionContract Contract,string Actor,DateTime AtUtc,string Reason,Guid[] ApprovedChangeIds);

public sealed record ConstructionForecastCost(string Category,string? CostCode,decimal RemainingForecast);

public sealed record ConstructionCodeBudgetLine(string Category,string CostCode,decimal Amount);
public sealed record ConstructionCodeBudgetRevision(Guid Id,Guid JobId,DateOnly Date,ConstructionCodeBudgetLine[] Lines,string Status="DRAFT",string Reason="",string Actor="",DateTime AtUtc=default,string CategoryBasisHash="",Dictionary<string,decimal?>? CategorySnapshot=null);
public sealed record ConstructionCommitmentCoding(Guid Id,Guid JobId,Guid OrderId,Guid OrderLineId,string CostCode,string Actor="",DateTime AtUtc=default,DateOnly Date=default,string? ImportedCommitmentId=null);
public sealed record ConstructionCostSource(string Kind,string SourceId,decimal Amount,Guid? JournalId=null);
public sealed record ConstructionCodeReport(Guid JobId,string Category,string? CostCode,decimal? Budget,decimal RemainingCommitment,decimal Actual,decimal? ForecastRemaining,decimal? EstimatedFinalCost,decimal? ActualVariance,decimal? ForecastVariance,bool AllocationCurrent,bool Unclassified,ConstructionCostSource[] Sources,string AllocationNote="");
