namespace TurnKeyOps.Lib.Dtos;

// Export files are normalized explicitly; no credentials or live ERP connection are inferred.
public sealed class FinanceMigrationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public string ExpectedSupplyVersion { get; set; } = "";
    public string SourceSystem { get; set; } = "Acumatica";
    public string ExportId { get; set; } = "";
    public string SourceFileSha256 { get; set; } = "";
    public DateOnly CutoverDate { get; set; }
    public string Strategy { get; set; } = "OPEN_ITEMS_AND_BALANCES";
    public string Reason { get; set; } = "";
    public string ApprovedFingerprint { get; set; } = "";
    public FinanceAccount[] NewAccounts { get; set; } = [];
    public FinanceMigrationAccountMap[] Accounts { get; set; } = [];
    public FinanceMigrationEntityMap[] Entities { get; set; } = [];
    public FinanceMigrationBalance[] TrialBalance { get; set; } = [];
    public FinanceMigrationOpenItem[] OpenItems { get; set; } = [];
    public FinanceMigrationCost[] JobCosts { get; set; } = [];
    public FinanceMigrationCommitment[] Commitments { get; set; } = [];
    public FinanceMigrationInventory[] Inventory { get; set; } = [];
    public FinanceJobBudget[] ConstructionBudgets { get; set; } = [];
    public ConstructionForecast[] ConstructionForecasts { get; set; } = [];
    public ConstructionContract[] ConstructionContracts { get; set; } = [];
    public ConstructionMigrationBaseline[] ConstructionHistory { get; set; } = [];
    public FinanceMigrationControls Controls { get; set; } = new();
}
public sealed record FinanceMigrationAccountMap(string ExternalAccount, string Account);
public sealed record FinanceMigrationEntityMap(string Kind, string ExternalId, Guid Id);
public sealed record FinanceMigrationBalance(string ExternalAccount, decimal Debit, decimal Credit);
public sealed record FinanceMigrationOpenItem(string ExternalId, string Kind, string ExternalPartyId, string Number, DateOnly Date, DateOnly DueDate, decimal Balance, string? ExternalJobId = null, Guid? ExistingInvoiceId = null);
public sealed record FinanceMigrationCost(string ExternalId, string ExternalJobId, string Category, string? CostCode, decimal Amount);
public sealed record FinanceMigrationCommitment(string ExternalId, string ExternalJobId, string ExternalVendorId, string Description, string Category, decimal RemainingAmount, Guid? SupplyOrderId = null, Guid? SupplyOrderLineId = null);
public sealed record FinanceMigrationInventory(string ExternalId, string ItemId, Guid LocationId, decimal Quantity, decimal Value);
public sealed class FinanceMigrationControls
{
    public decimal? ConstructionBudget { get; set; }
    public decimal? ContractValue { get; set; }
    public decimal? ConstructionBilled { get; set; }
    public decimal? Retainage { get; set; }
    public decimal? EarnedRevenue { get; set; }
    public decimal Ar { get; set; }
    public decimal Ap { get; set; }
    public decimal JobCosts { get; set; }
    public decimal Commitments { get; set; }
    public decimal Inventory { get; set; }
    // All configured active cash accounts must be confirmed, including zero balances.
    public Dictionary<string, decimal> Cash { get; set; } = [];
}
public sealed record FinanceMigrationCheck(string Name, decimal Imported, decimal Source, decimal Difference);
public sealed record FinanceMigrationPreview(string Fingerprint, bool CanImport, string[] Errors, string[] Warnings, FinanceMigrationCheck[] Checks, int JournalLines, int OpenItems, int JobCosts, int Commitments, int Inventory);
public sealed record FinanceMigrationBatch(Guid Id, string SourceSystem, string ExportId, string SourceFileSha256, DateOnly CutoverDate, string Strategy, string Fingerprint, Guid JournalId, string Actor, DateTime AtUtc, string Reason, string SourcePayloadJson, FinanceMigrationCheck[] Checks, FinanceMigrationAccountMap[] Accounts, FinanceMigrationEntityMap[] Entities);
public sealed record FinanceImportedCommitment(string Id, Guid BatchId, Guid JobId, Guid VendorId, string ExternalId, string Description, string Category, decimal RemainingAmount, DateOnly Date, Guid? SupplyOrderId, Guid? SupplyOrderLineId, decimal? UnitCost = null);
// Inventory import is valuation evidence under vendor-bill costing, never another expense or mutable stock count.
public sealed record FinanceImportedInventory(string Id, Guid BatchId, string ExternalId, string ItemId, Guid LocationId, decimal Quantity, decimal Value, DateOnly Date);

public sealed class FinanceOpeningCreditApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid CreditItemId { get; set; }
    public Guid TargetItemId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class FinanceOpeningCreditReversal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid ApplicationId { get; set; }
    public string Reason { get; set; } = "";
}
