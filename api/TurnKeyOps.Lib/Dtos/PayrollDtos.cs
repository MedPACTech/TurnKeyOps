namespace TurnKeyOps.Lib.Dtos;

// Product enablement/integrations are tenant configuration; user grants never enable a product.
public sealed class PayrollConfiguration
{
    public bool Enabled { get; set; }
    public bool JobsEnabled { get; set; }
    public bool FinanceEnabled { get; set; }
    public bool SchedulingEnabled { get; set; }
    // Product availability and integration preferences remain separate from every user grant.
    public bool IntegrateJobs { get; set; } = true;
    public bool IntegrateFinance { get; set; } = true;
    public bool IntegrateScheduling { get; set; } = true;
    [System.Text.Json.Serialization.JsonIgnore] public bool UsesJobs => JobsEnabled && IntegrateJobs;
    [System.Text.Json.Serialization.JsonIgnore] public bool UsesFinance => FinanceEnabled && IntegrateFinance;
    [System.Text.Json.Serialization.JsonIgnore] public bool UsesScheduling => SchedulingEnabled && IntegrateScheduling;
    public bool RequireJob { get; set; }
    public bool RequireCostCode { get; set; } = true;
    public bool CrewTimeEnabled { get; set; } = true;
    public string Timezone { get; set; } = "America/New_York";
    public string Provider { get; set; } = "generic-csv";
    public string Frequency { get; set; } = "WEEKLY";
    public string LocationPolicy { get; set; } = "OFF";
    public decimal? MealBreakRequiredAfterHours { get; set; }
    public decimal MealBreakMinimumMinutes { get; set; } = 30;
    public decimal? WeeklyOvertime { get; set; }
    public decimal? DailyOvertime { get; set; }
    public decimal? DailyDoubleTime { get; set; }
    public bool SeventhDayOvertime { get; set; }
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public decimal DoubleTimeMultiplier { get; set; } = 2m;
    public string[] CostCodes { get; set; } = ["GENERAL", "ADMIN", "SHOP", "TRAINING", "TRAVEL"];
    public string[] Classifications { get; set; } = ["Laborer", "Foreman", "Apprentice", "Journeyman"];
    public string[] EarningsTypes { get; set; } = ["REGULAR","OVERTIME","DOUBLE_TIME","PTO","HOLIDAY","SICK","TRAINING","TRAVEL","OTHER"];
    public string[] LeaveTypes { get; set; } = ["PTO","SICK","VACATION","UNPAID"];
    public Dictionary<string,string> AiActions { get; set; } = new(){["payroll.readiness"]="read",["payroll.prepare"]="approval"};
    public Dictionary<string,string> EarningsMapping { get; set; } = [];
}
public sealed class PayrollState
{
    public string Version { get; set; } = "";
    public PayrollConfiguration Configuration { get; set; } = new();
    public List<PayrollTimeEntry> Time { get; set; } = [];
    public List<PayrollProfile> Profiles { get; set; } = [];
    public List<PayrollCrew> Crews { get; set; } = [];
    public List<PayrollPeriod> Periods { get; set; } = [];
    public List<PayrollBatch> Batches { get; set; } = [];
    public List<PayrollActual> Actuals { get; set; } = [];
    public List<PayrollLeave> Leave { get; set; } = [];
    public List<PayrollWageRequirement> WageRequirements { get; set; } = [];
    public List<PayrollEvent> Events { get; set; } = [];
    public List<PayrollAudit> Audit { get; set; } = [];
}
public sealed record PayrollBreak(DateTimeOffset Start,DateTimeOffset? End,string Kind,bool Paid);
public sealed record PayrollLocation(decimal Latitude,decimal Longitude,decimal? Accuracy,string Evidence);
public sealed record PayrollTimeEntry(Guid Id,Guid EmployeeId,DateOnly WorkDate,string Timezone,DateTimeOffset? ClockIn,DateTimeOffset? ClockOut,decimal Hours,Guid? JobId,string CostCode,string Classification,string EarningType,string Source,string Status,string Notes="",Guid? CrewId=null,Guid? SiteId=null,Guid? ScheduleEventId=null,string Trade="",string Jurisdiction="",string Device="",string SourceKey="",PayrollBreak[]? Breaks=null,Guid? CorrectsId=null,string? SubmittedBy=null,DateTimeOffset? SubmittedAt=null,string? ApprovedBy=null,DateTimeOffset? ApprovedAt=null,PayrollLocation? Location=null);
public sealed record PayrollProfile(Guid Id,Guid EmployeeId,DateOnly EffectiveDate,string PayType,decimal? PayRate,decimal? SalaryPerPeriod,bool OvertimeEligible,string Classification,string ProviderEmployeeId,string Department,decimal? LaborCostRate=null,string EmploymentStatus="ACTIVE",DateOnly? TerminationDate=null);
public sealed record PayrollCrew(Guid Id,string Name,Guid LeadId,Guid[] Members,DateOnly EffectiveFrom,DateOnly? EffectiveTo=null,string Trade="",string[]? Qualifications=null);
public sealed record PayrollPeriod(Guid Id,DateOnly Start,DateOnly End,DateOnly PayDate,DateTimeOffset Cutoff,string Status="OPEN");
public sealed record PayrollEarning(Guid TimeId,Guid EmployeeId,DateOnly Date,Guid? JobId,string CostCode,string Classification,string Type,decimal Hours,decimal? Rate,decimal? Gross,string ProviderEmployeeId,string ProviderEarningCode,string Trade="",string Jurisdiction="",decimal? LaborCostRate=null);
public sealed record PayrollBatch(Guid Id,Guid PeriodId,int ExportVersion,string Provider,PayrollEarning[] Lines,string CalculationHash,string Status,string PreparedBy,DateTimeOffset PreparedAt,string? Artifact=null,string? ArtifactHash=null,string[]? Issues=null);
public sealed record PayrollActual(Guid Id,Guid BatchId,string ProviderPayrollId,string SourceKey,Guid EmployeeId,string EarningType,decimal Hours,decimal? Gross,decimal? EmployeeTaxes=null,decimal? EmployerTaxes=null,decimal? Deductions=null,decimal? NetPay=null,decimal? Benefits=null,decimal? Fees=null,decimal? Burden=null,Guid? CorrectsId=null);
public sealed record PayrollLeave(Guid Id,Guid EmployeeId,DateOnly Date,decimal Hours,string Type,string Status="REQUESTED",string Reason="",string? ApprovedBy=null);
public sealed record PayrollWageRequirement(Guid Id,Guid JobId,string Classification,string Jurisdiction,DateOnly EffectiveFrom,DateOnly? EffectiveTo,decimal RequiredBase,decimal RequiredFringe,string AuthorityReference);
// Durable outbox: no synchronous Finance handler is required for a payroll command to commit.
public sealed record PayrollEvent(Guid Id,string Type,Guid SourceId,string Payload,string Hash,DateTimeOffset At);
public sealed record PayrollAudit(Guid CommandId,string Action,string Fingerprint,string Actor,DateTimeOffset At,string Reason);
public sealed class PayrollCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExpectedVersion { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public PayrollConfiguration? Configuration { get; set; }
    public PayrollTimeEntry? Time { get; set; }
    public PayrollTimeEntry[] Entries { get; set; } = [];
    public PayrollProfile? Profile { get; set; }
    public PayrollCrew? Crew { get; set; }
    public PayrollPeriod? Period { get; set; }
    public PayrollLeave? Leave { get; set; }
    public PayrollWageRequirement? WageRequirement { get; set; }
    public PayrollActual[] Actuals { get; set; } = [];
    public string BreakKind { get; set; } = "UNPAID";
    public bool Paid { get; set; }
}
