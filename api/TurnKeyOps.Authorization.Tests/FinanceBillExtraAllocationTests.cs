using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;

public sealed class FinanceBillExtraAllocationTests
{
    [Fact] public void GrossAllocationUsesExistingExpenseAndInvalidatesEveryAffectedJob()
    {
        var f=new Fixture();var before=Clone(f.State);Assert.True(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,f.Day));
        FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");
        Assert.Single(f.State.Journals);Assert.Equal(FinanceLedger.Hash(before.Journals[0]),FinanceLedger.Hash(f.State.Journals[0]));
        Assert.Equal(110,f.State.Costs.Sum(c=>c.Amount));Assert.False(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,f.Day));
        Assert.All(f.State.Budgets,b=>Assert.False(b.ActualsComplete));
        Assert.Throws<ArgumentException>(()=>FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner"));
        FinanceIntegrity.ValidateTransition(before,f.State);
    }
    [Theory] [InlineData("amount")] [InlineData("account")] [InlineData("foreign-job")] [InlineData("category")] [InlineData("inactive-code")] [InlineData("credit")] [InlineData("date")]
    public void InvalidAttributionCannotChangeAnyCostOrJournal(string invalid)
    {
        var f=new Fixture();var command=f.Command();
        switch(invalid)
        {
            case "amount": command.Lines[0]=command.Lines[0] with{Debit=5};break;
            case "account": command.Lines[0]=command.Lines[0] with{Account="1000"};break;
            case "foreign-job": command.Lines[0]=command.Lines[0] with{JobId=Guid.NewGuid()};break;
            case "category": command.Lines[0]=command.Lines[0] with{Category="LABOR"};break;
            case "inactive-code": var index=f.State.CostCodes.FindIndex(c=>c.Code=="MATERIAL");f.State.CostCodes[index]=f.State.CostCodes[index] with{Active=false};break;
            case "credit": command.Lines[0]=command.Lines[0] with{Credit=1};break;
            default: command.Date=f.Day.AddDays(1);break;
        }
        var before=FinanceLedger.Hash(f.State);Assert.Throws<ArgumentException>(()=>FinanceBillExtraAllocations.Allocate(f.State,command,"owner"));Assert.Equal(before,FinanceLedger.Hash(f.State));
    }
    [Fact] public void ClosedBillPeriodAndUnknownSourceCodingRemainGated()
    {
        var f=new Fixture();f.State.Periods[0].Status="CLOSED";Assert.Throws<ArgumentException>(()=>FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner"));
        f.State.Periods[0].Status="OPEN";var source=f.State.Journals[0];source.Lines[2]=source.Lines[2] with{Description="Unverified extra coding"};
        Assert.Throws<ArgumentException>(()=>FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner"));Assert.True(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,f.Day));
        Assert.Equal(2,f.State.Costs.Count);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void CreditsBeforeOrAfterAttributionHaveExactDimensionalCostsAndNoSecondExpense(bool creditFirst)
    {
        var f=new Fixture();var creditDate=f.Day.AddDays(5);
        if(creditFirst)f.Credit(55,creditDate,"credit-1");
        FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");
        if(!creditFirst)f.Credit(55,creditDate,"credit-1");
        FinanceBillExtraAllocations.ApplyCredits(f.State);FinanceBillExtraAllocations.ApplyCredits(f.State);
        Assert.Equal(2,f.State.Journals.Count);Assert.Equal(110,f.State.Costs.Where(c=>c.Date<=f.Day).Sum(c=>c.Amount));
        Assert.Equal(55,f.State.Costs.Sum(c=>c.Amount));Assert.Equal(55,FinanceLedger.AccountBalance(f.State,"5000",creditDate));
        Assert.Equal(33,f.State.Costs.Where(c=>c.JobId==f.JobA).Sum(c=>c.Amount));Assert.Equal(22,f.State.Costs.Where(c=>c.JobId==f.JobB).Sum(c=>c.Amount));
        Assert.Equal(2,f.State.Costs.Count(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource));
        Assert.All(f.State.Costs.Where(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource),c=>Assert.Equal(creditDate,c.Date));
        Assert.False(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,creditDate));FinanceIntegrity.ValidateTransition(new(),f.State);
    }
    [Fact] public void MissingCreditCatchupGatesOnlyAtAndAfterCreditDate()
    {
        var f=new Fixture();FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");var day=f.Day.AddDays(5);f.Credit(55,day,"credit");
        f.State.Costs.RemoveAll(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource); // A pre-feature snapshot without dimensional credit catch-up.
        Assert.False(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,f.Day));Assert.True(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,day));
        FinanceBillExtraAllocations.ApplyCredits(f.State);Assert.False(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,day));Assert.Equal(55,f.State.Costs.Sum(c=>c.Amount));
    }
    [Fact] public void LateAttributionRequiresEveryAffectedCreditPeriodOpenBeforeAnyMutation()
    {
        var f=new Fixture();f.State.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});f.Credit(55,new(2026,10,5),"credit");f.State.Periods[1].Status="CLOSED";
        var before=FinanceLedger.Hash(f.State);Assert.Throws<ArgumentException>(()=>FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner"));Assert.Equal(before,FinanceLedger.Hash(f.State));
        f.State.Periods[1].Status="OPEN";FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");Assert.Equal(55,f.State.Costs.Sum(c=>c.Amount));
    }
    [Fact] public void ClosedCreditCatchupPreflightsAllBillsAndLeavesStateUnchanged()
    {
        var f=new Fixture();FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");
        f.State.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});
        f.Credit(10,f.Day.AddDays(1),"open-credit");f.Credit(10,new(2026,10,5),"closed-credit");
        f.State.Costs.RemoveAll(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource);
        f.State.Periods[1].Status="CLOSED";var before=FinanceLedger.Hash(f.State);
        Assert.Throws<ArgumentException>(()=>FinanceBillExtraAllocations.ApplyCredits(f.State));Assert.Equal(before,FinanceLedger.Hash(f.State));
        Assert.True(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,DateOnly.MaxValue));
        f.State.Periods[1].Status="OPEN";FinanceBillExtraAllocations.ApplyCredits(f.State);f.State.Periods[1].Status="CLOSED";
        FinanceBillExtraAllocations.ApplyCredits(f.State); // Already recorded dimensions remain immutable and need no reopen.
        Assert.Equal(90,f.State.Costs.Sum(c=>c.Amount));
    }
    [Fact] public void CreditRoundingMatchesPostedSourceOffsetsAndIsIndependentOfRetiredCostCodes()
    {
        var f=new Fixture();FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");
        f.Credit(1.01m,f.Day.AddDays(1),"partial-1");f.Credit(0.09m,f.Day.AddDays(2),"partial-2");
        f.State.Costs.RemoveAll(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource);
        var index=f.State.CostCodes.FindIndex(c=>c.Code=="MATERIAL");f.State.CostCodes[index]=f.State.CostCodes[index] with{Active=false};
        FinanceBillExtraAllocations.ApplyCredits(f.State);
        // Source 60/40/10 split: 1.01 -> .55/.37/.09; .09 -> .05/.03/.01; extras credits .10 total.
        Assert.Equal(-0.10m,f.State.Costs.Where(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource).Sum(c=>c.Amount));
        Assert.Equal(108.90m,f.State.Costs.Sum(c=>c.Amount));Assert.Equal(108.90m,FinanceLedger.AccountBalance(f.State,"5000",DateOnly.MaxValue));
        Assert.False(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,DateOnly.MaxValue));
    }
    [Fact] public void ApplyingOpeningCreditDoesNotReverseExpenseAttribution()
    {
        var f=new Fixture();FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");
        var journal=FinanceLedger.Post(f.State,f.Day,"opening-credit-application","apply","Apply previous source credit",[new("2000",5,0),new("2000",0,5)],"owner");
        f.State.Settlements.Add(new(Guid.NewGuid(),"vendor-credit",f.Bill.VendorId,f.Day,5,"","opening-credit","Existing source credit","apply",journal.Id,[new(f.Bill.Id,5)],"opening-credit-application"));
        FinanceBillExtraAllocations.ApplyCredits(f.State);Assert.DoesNotContain(f.State.Costs,c=>c.SourceType==FinanceBillExtraAllocations.CreditSource);Assert.Equal(110,f.State.Costs.Sum(c=>c.Amount));
    }
    [Theory][InlineData(true)][InlineData(false)]public void RetainedGrossCreditsRelieveTaxFreightBeforeOrAfterAttribution(bool creditFirst)
    {
        var f=new Fixture(retained:true);var credit=new FinanceCommand{Id=Guid.NewGuid(),Target=f.Bill.Id.ToString(),Date=f.Day.AddDays(1),Amount=110,SourceKey="gross-retained-credit",Reason="Reverse full gross retained bill"};
        if(creditFirst)FinanceApRetainage.Apply(f.State,credit,"owner",true);
        FinanceBillExtraAllocations.Allocate(f.State,f.Command(),"owner");
        if(!creditFirst)FinanceApRetainage.Apply(f.State,credit,"owner",true);
        FinanceBillExtraAllocations.ApplyCredits(f.State);Assert.Equal(0,f.State.Costs.Sum(c=>c.Amount));Assert.Equal(0,FinanceLedger.AccountBalance(f.State,"5000",credit.Date));
        Assert.Equal(-10,f.State.Costs.Where(c=>c.SourceType==FinanceBillExtraAllocations.CreditSource).Sum(c=>c.Amount));Assert.False(FinanceBillExtraAllocations.Outstanding(f.State,f.JobA,credit.Date));
        Assert.Equal(0,FinanceLedger.AccountBalance(f.State,"2000",credit.Date));Assert.Equal(0,FinanceLedger.AccountBalance(f.State,"2250",credit.Date));FinanceIntegrity.ValidateTransition(new(),f.State);
    }
    private static FinanceState Clone(FinanceState state)=>JsonSerializer.Deserialize<FinanceState>(JsonSerializer.Serialize(state,JobConfigurationService.Json),JobConfigurationService.Json)!;
    private sealed class Fixture
    {
        public FinanceState State=FinanceTests.State();public Guid JobA=Guid.NewGuid(),JobB=Guid.NewGuid();public DateOnly Day=new(2026,9,15);public FinanceBill Bill;
        public Fixture(bool retained=false)
        {
            if(retained){State.Accounts.Add(new("2250","Retained payable","LIABILITY","CREDIT"));State.Operations.ApRetainageAccount="2250";}
            var vendor=Guid.NewGuid();Bill=new(){VendorId=vendor,Number="V-1",Date=Day,DueDate=Day.AddDays(30),Status="APPROVED",RetainagePercent=retained?10:0,Tax=6,Shipping=4,
                Lines=[new("Material A",1,60,"5000",JobId:JobA,CostCode:"MATERIAL"),new("Material B",1,40,"5000",JobId:JobB,CostCode:"MATERIAL")]};
            var journal=FinanceLedger.Post(State,Day,"vendor-bill",Bill.Id.ToString(),"Vendor invoice",[
                new("5000",60,0,JobA,"MATERIAL",VendorId:vendor,Category:"MATERIAL"),new("5000",40,0,JobB,"MATERIAL",VendorId:vendor,Category:"MATERIAL"),
                new("5000",10,0,VendorId:vendor,Description:"Purchase tax and shipping (unallocated)"),new("2000",0,retained?99:110,VendorId:vendor),..(retained?new FinanceLine[]{new("2250",0,11,VendorId:vendor)}:[])],"owner");
            Bill.JournalId=journal.Id;State.Bills.Add(Bill);State.OpenItems.Add(new(Bill.Id,"AP",vendor,Bill.Number,Day,Bill.DueDate,retained?99:110,journal.Id));
            State.Costs.Add(new(Guid.NewGuid(),JobA,"MATERIAL","MATERIAL",60,Day,"vendor-bill",Bill.Id.ToString(),journal.Id));State.Costs.Add(new(Guid.NewGuid(),JobB,"MATERIAL","MATERIAL",40,Day,"vendor-bill",Bill.Id.ToString(),journal.Id));
            State.Budgets.Add(new(JobA,"estimate",100,0,new(){["MATERIAL"]=60},true,"Reviewed"));State.Budgets.Add(new(JobB,"estimate",100,0,new(){["MATERIAL"]=40},true,"Reviewed"));
        }
        public FinanceCommand Command()=>new(){Target=Bill.Id.ToString(),Date=Day,Reason="Reviewed supplier freight and tax allocation",Lines=[
            new("5000",6,0,JobA,"MATERIAL",Category:"MATERIAL"),new("5000",4,0,JobB,"MATERIAL",Category:"MATERIAL")]};
        public void Credit(decimal amount,DateOnly date,string key)=>FinanceLedger.Settle(State,new(){Id=Guid.NewGuid(),Date=date,Amount=amount,PartyId=Bill.VendorId,SourceKey=key,Reference=key,Allocations=[new(Bill.Id,amount)]},"owner","vendor-credit");
    }
}
