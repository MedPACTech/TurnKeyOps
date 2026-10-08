namespace TurnKeyOps.Lib.Dtos;

// Tenant is the storage boundary, never a caller-supplied command field.
public sealed class FinanceState
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public List<FinanceAccount> Accounts { get; set; } = [];
    public List<FinancePeriod> Periods { get; set; } = [];
    public List<FinanceJournalDraft> JournalDrafts { get; set; } = [];
    public List<FinanceJournal> Journals { get; set; } = [];
    public List<FinanceOpenItem> OpenItems { get; set; } = [];
    public List<FinanceSettlement> Settlements { get; set; } = [];
    public List<FinanceBill> Bills { get; set; } = [];
    public List<FinanceCostCode> CostCodes { get; set; } = [];
    public List<FinanceJobCost> Costs { get; set; } = [];
    public List<FinanceBudgetReview> BudgetReviews { get; set; } = [];
    public List<FinanceJobChangeValue> JobChanges { get; set; } = [];
    public List<FinanceJobBudget> Budgets { get; set; } = [];
    public List<FinanceCashAccount> CashAccounts { get; set; } = [];
    public List<FinanceBankTransaction> BankTransactions { get; set; } = [];
    public List<FinanceReconciliation> Reconciliations { get; set; } = [];
    public List<FinanceAudit> Audit { get; set; } = [];
    public FinancePolicy Policy { get; set; } = new();
}
public sealed record FinanceAccount(string Code, string Name, string Type, string NormalBalance, bool SystemControlled = false, bool Active = true, string? ParentCode = null, string? ExternalId = null);
public sealed class FinancePeriod
{
    public string Id { get; set; } = "";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public string Status { get; set; } = "OPEN";
}
public sealed class FinancePolicy
{
    public string Currency { get; set; } = "USD";
    public string MaterialActualSource { get; set; } = "vendor-bill";
    public string Matching { get; set; } = "warning";
    public string RevenueRecognition { get; set; } = "invoice";
    public Dictionary<string,string> Accounts { get; set; } = new() { ["cash"]="1000", ["ar"]="1100", ["inventory"]="1200", ["ap"]="2000", ["tax"]="2100", ["equity"]="3000", ["revenue"]="4000", ["cost"]="5000" };
}
public sealed record FinanceLine(string Account, decimal Debit, decimal Credit, Guid? JobId = null, string? CostCode = null, Guid? CustomerId = null, Guid? VendorId = null, string? Trade = null, string? Site = null, string Description = "", string? Category = null);
public sealed record FinanceJournal(Guid Id, DateOnly Date, string PeriodId, string SourceType, string SourceId, string Memo, string Status, string CreatedBy, string PostedBy, DateTime PostedAtUtc, FinanceLine[] Lines, string Fingerprint, Guid? ReversesId = null);
public sealed record FinanceOpenItem(Guid Id, string Kind, Guid PartyId, string Number, DateOnly Date, DateOnly DueDate, decimal Total, Guid JournalId, Guid? JobId = null, string? ExternalId = null);
public sealed record FinanceAllocation(Guid OpenItemId, decimal Amount);
public sealed record FinanceSettlement(Guid Id, string Kind, Guid PartyId, DateOnly Date, decimal Amount, string CashAccount, string Method, string Reference, string SourceKey, Guid JournalId, FinanceAllocation[] Allocations, string Origin = "finance");
public sealed class FinanceBill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateOnly DueDate { get; set; }
    public string Terms { get; set; } = "";
    public Guid? OrderId { get; set; }
    public Guid[] ReceiptIds { get; set; } = [];
    public string Status { get; set; } = "DRAFT";
    public List<FinanceBillLine> Lines { get; set; } = [];
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total => Lines.Sum(x => x.Amount) + Tax + Shipping;
    public string Notes { get; set; } = "";
    public string? ExternalId { get; set; }
    public string[] MatchExceptions { get; set; } = [];
    public Guid? JournalId { get; set; }
}
public sealed record FinanceBillLine(string Description, decimal Quantity, decimal UnitCost, string Account, string Category = "MATERIAL", Guid? JobId = null, string? CostCode = null, Guid? OrderLineId = null)
{
    public decimal Amount => Math.Round(Quantity * UnitCost, 2, MidpointRounding.AwayFromZero);
}
public sealed record FinanceCostCode(string Code, string Name, string Category, string? Trade = null, bool Active = true);
public sealed record FinanceJobCost(Guid Id, Guid JobId, string Category, string? CostCode, decimal Amount, DateOnly Date, string SourceType, string SourceId, Guid? JournalId = null, decimal? Hours = null, decimal? Rate = null);
public sealed record FinanceJobBudget(Guid JobId, string SourceId, decimal Sold, decimal ApprovedChanges, Dictionary<string,decimal?> Categories, bool ActualsComplete, string CompletenessNote, bool TaxBasisKnown = true);
public sealed record FinanceCashAccount(string Id, string Name, string LedgerAccount, string Institution, string MaskedReference, string Currency = "USD", bool Active = true);
public sealed class FinanceBankTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CashAccountId { get; set; } = "";
    public string ImportId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public Guid? JournalId { get; set; }
    public string ConfirmedBy { get; set; } = "";
    public string? ReconciliationId { get; set; }
}
public sealed record FinanceReconciliation(string Id, string CashAccountId, DateOnly Start, DateOnly End, decimal OpeningBalance, decimal ClosingBalance, string Actor, DateTime AtUtc);
public sealed record FinanceAudit(Guid CommandId, string Action, string Target, string Actor, DateTime AtUtc, string Reason, string Fingerprint);
public sealed class FinanceCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Amount { get; set; }
    public decimal OpeningBalance { get; set; }
    public string Reference { get; set; } = "";
    public string Method { get; set; } = "manual";
    public Guid PartyId { get; set; }
    public Guid? JobId { get; set; }
    public string Category { get; set; } = "OTHER_DIRECT_COST";
    public string? CostCode { get; set; }
    public string Account { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public decimal? Hours { get; set; }
    public decimal? Rate { get; set; }
    public FinanceLine[] Lines { get; set; } = [];
    public FinanceAllocation[] Allocations { get; set; } = [];
    public FinanceAccount? ChartAccount { get; set; }
    public FinanceBill? Bill { get; set; }
    public FinanceCostCode? Code { get; set; }
    public FinanceCashAccount? CashAccount { get; set; }
    public FinanceBankTransaction[] BankTransactions { get; set; } = [];
    public FinancePolicy? Policy { get; set; }
    public Dictionary<string,decimal?> Budget { get; set; } = [];
    public bool Complete { get; set; }
    public decimal ArControl { get; set; }
    public decimal ApControl { get; set; }
    public FinanceOpenItem[] ImportOpenItems { get; set; } = [];
}

public sealed record FinanceJournalDraft(Guid Id, DateOnly Date, string Memo, FinanceLine[] Lines, string CreatedBy, Guid? PostedJournalId = null);

public sealed record FinanceBudgetReview(Guid Id, Guid JobId, string Actor, DateTime AtUtc, string Reason, Dictionary<string,decimal?> Categories, bool ActualsComplete);
public sealed record FinanceJobChangeValue(Guid JobId, Guid ChangeId, Guid EstimateId, int Revision, string DocumentHash, decimal NetValue);
