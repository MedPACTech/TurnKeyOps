namespace TurnKeyOps.Lib.Dtos;

public sealed class FinanceOperationsState
{
    public string ApRetainageAccount { get; set; } = "";
    public List<FinanceApRetainageEvent> ApRetainageEvents { get; set; } = [];
    public FinanceReversalPolicy ReversalPolicy { get; set; } = new(false,"",default);
    public List<FinanceReversalAttempt> ReversalAttempts { get; set; } = [];
    public FinanceBankConnection? BankConnection { get; set; }
    public List<FinanceLaborRate> Rates { get; set; } = [];
    public List<FinanceTimeEntry> Time { get; set; } = [];
    public List<FinanceAccrual> AccrualDrafts { get; set; } = [];
    public List<FinanceAccrual> Accruals { get; set; } = [];
    public List<FinanceRecurringJournal> Recurring { get; set; } = [];
    public List<FinanceCloseTask> CloseTasks { get; set; } = [];
    public List<FinanceBankSplit> BankSplits { get; set; } = [];
    public List<FinanceFixedAsset> Assets { get; set; } = [];
    public List<FinanceVendorCompliance> Vendors { get; set; } = [];
}
public sealed record FinanceApRetainageEvent(Guid Id,Guid BillId,DateOnly Date,string Kind,decimal GrossAmount,decimal RetainedAmount,decimal ApAmount,Guid JournalId,FinanceAllocation[] Allocations,string SourceKey);
public sealed record FinanceReversalPolicy(bool Enabled,string ApprovedBy,DateTime ApprovedAtUtc);
public sealed record FinanceReversalAttempt(Guid Id,Guid AccrualId,DateOnly AttemptDate,string Status,string Reason);
public sealed record FinanceBankConnection(string ProviderId,string AccountReference,string CashAccountId,bool Enabled,string ApprovedBy="",DateTime ApprovedAtUtc=default);
public sealed record FinanceLaborRate(Guid Id, Guid EmployeeId, DateOnly EffectiveDate, decimal HourlyRate, decimal BurdenRate, string Actor);
public sealed record FinanceTimeEntry(Guid Id, string SourceKey, Guid EmployeeId, Guid JobId, DateOnly Date, decimal Hours, string CostCode, string Status, Guid? RateId = null, decimal? RateSnapshot = null, Guid? JournalId = null, string? ApprovedBy = null, string EarningsClass = "REGULAR", decimal? BaseRateSnapshot = null, decimal? BurdenRateSnapshot = null);
public sealed record FinanceAccrual(Guid Id, DateOnly Date, string Description, FinanceLine[] Lines, DateOnly ReversalDate, Guid JournalId, Guid? ReversalJournalId = null);
public sealed record FinanceRecurringJournal(Guid Id, string Description, FinanceLine[] Lines, DateOnly Start, DateOnly End);
public sealed record FinanceCloseTask(string PeriodId, string Key, string Description, bool Required, bool Complete, string Actor);
public sealed record FinanceBankSplit(Guid BankTransactionId, Guid JournalId, decimal Amount, string Actor);
public sealed record FinanceFixedAsset(Guid Id, string Description, string Class, DateOnly AcquisitionDate, decimal Cost, Guid? VendorId, Guid? JobId, DateOnly? InServiceDate, string Location, string? EquipmentId, string Status, Guid JournalId);
// Sensitive tax identifiers belong in an external secrets vault; only a non-secret reference is stored here.
public sealed record FinanceVendorCompliance(Guid VendorId, string W9Status, DateOnly? InsuranceExpiry, DateOnly? LicenseExpiry, bool Eligible1099, string Classification, string[] AttachmentIds);
public sealed class FinanceOperationsCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public Guid TargetId { get; set; }
    public string SourceKey { get; set; } = "";
    public DateOnly Date { get; set; }
    public FinanceLaborRate? Rate { get; set; }
    public FinanceTimeEntry? Time { get; set; }
    public FinanceAccrual? Accrual { get; set; }
    public FinanceRecurringJournal? Recurring { get; set; }
    public FinanceCloseTask? CloseTask { get; set; }
    public FinanceBankTransaction[] BankTransactions { get; set; } = [];
    public FinanceBankSplit[] Splits { get; set; } = [];
    public FinanceFixedAsset? Asset { get; set; }
    public FinanceVendorCompliance? Vendor { get; set; }
    public bool Enabled { get; set; }
    public FinanceBankConnection? BankConnection { get; set; }
    public string? BankCsv { get; set; }
    public string CashAccountId { get; set; } = "";
    public Guid ActivityEntryId { get; set; }
    public string OffsetAccount { get; set; } = "";
}
