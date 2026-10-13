using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceInsightsTests
{
    private static readonly DateOnly Day=new(2026,9,15);
    private static FinanceState State(){var s=FinanceTests.State();FinanceLedger.Post(s,Day,"manual","cash","Cash",[new("1000",1000,0),new("3000",0,1000)],"owner");return s;}
    private static FinanceOpenItem Item(FinanceState s,string kind,decimal amount,int due){var i=new FinanceOpenItem(Guid.NewGuid(),kind,Guid.NewGuid(),kind+"-1",Day,Day.AddDays(due),amount,Guid.NewGuid());s.OpenItems.Add(i);return i;}
    [Fact]public void ForecastSeparatesUncertainReceiptsFromObligationsAndNamesUnknowns()
    {
        var s=State();Item(s,"AR",500,-3);Item(s,"AP",200,5);Item(s,"AP",400,31);
        s.Bills.Add(new(){Number="Unapproved",Status="PENDING_APPROVAL",Lines=[new("Service",1,90,"5000")]});
        var f=FinanceInsights.Forecast(s,Day);
        Assert.Equal(1000,f.OpeningCash);Assert.Equal(800,f.ObligationsScenario);Assert.Equal(1300,f.DueDateScenario);Assert.Equal(90,f.PendingBillsExcluded);
        Assert.Equal(Day.AddDays(1),f.Sources.Single(s=>s.Kind=="AR").Date);Assert.Contains(f.Unknowns,s=>s.Contains("payroll"));Assert.Equal(30,f.Days.Length);
    }
    [Fact]public void ForecastUsesGrossReceivableAndPostedTaxCashRatherThanInferringTaxPayments()
    {
        var s=State();Item(s,"AR",110,5);
        var taxPayment=FinanceLedger.Post(s,Day.AddDays(2),"manual","tax-payment","Recorded tax payment",[new("2000",10,0),new("1000",0,10)],"owner");
        var f=FinanceInsights.Forecast(s,Day);
        Assert.Equal(110,f.ReceiptsAssumed);Assert.Equal(-10,f.RecordedFutureMovement);Assert.Equal(990,f.ObligationsScenario);Assert.Equal(1100,f.DueDateScenario);
        Assert.Equal(taxPayment.Id,f.Sources.Single(x=>x.Kind=="posted-cash").JournalId);Assert.Contains(f.Unknowns,x=>x.Contains("tax",StringComparison.OrdinalIgnoreCase));
    }
    [Fact]public void FuturePostedPaymentsAreNotForecastTwice()
    {
        var s=State();var i=Item(s,"AP",200,5);var j=FinanceLedger.Post(s,Day.AddDays(3),"vendor-payment","future","Future payment",[new("2000",200,0),new("1000",0,200)],"owner");
        s.Settlements.Add(new(Guid.NewGuid(),"vendor-payment",i.PartyId,Day.AddDays(3),200,"operating","check","ref","future",j.Id,[new(i.Id,200)]));
        var f=FinanceInsights.Forecast(s,Day);Assert.Equal(800,f.ObligationsScenario);Assert.Equal(0,f.PaymentsAssumed);Assert.Single(f.Sources);Assert.Equal(j.Id,f.Sources[0].JournalId);
    }
    [Fact]public void ForecastExcludesUnrecordedFutureInvoicesAndNetsCashTransfers()
    {
        var s=State();s.Accounts.Add(new("1010","Savings","ASSET","DEBIT"));s.CashAccounts.Add(new("savings","Savings","1010","",""));
        FinanceLedger.Post(s,Day.AddDays(1),"manual","transfer","Transfer",[new("1010",200,0),new("1000",0,200)],"owner");
        s.OpenItems.Add(new(Guid.NewGuid(),"AR",Guid.NewGuid(),"future",Day.AddDays(1),Day.AddDays(4),100,Guid.NewGuid()));
        var f=FinanceInsights.Forecast(s,Day);Assert.Equal(1000,f.DueDateScenario);Assert.Empty(f.Sources);
    }
    [Fact]public void PartialFutureAllocationLeavesOnlyRemainderInDueDateScenario()
    {
        var s=State();var i=Item(s,"AR",500,5);var j=FinanceLedger.Post(s,Day.AddDays(2),"payment","partial","Receipt",[new("1000",200,0),new("1100",0,200)],"owner");
        s.Settlements.Add(new(Guid.NewGuid(),"payment",i.PartyId,Day.AddDays(2),200,"operating","check","ref","partial",j.Id,[new(i.Id,200)]));
        var f=FinanceInsights.Forecast(s,Day);Assert.Equal(1500,f.DueDateScenario);Assert.Equal(300,f.ReceiptsAssumed);Assert.Equal(200,f.RecordedFutureMovement);
    }
    [Fact]public void JobReviewNeverInventsMarginForMissingCostsAndHasSources()
    {
        var s=State();var job=Guid.NewGuid();s.Budgets.Add(new(job,"estimate",1000,0,new(){["LABOR"]=null},false,"Missing labor"));
        var r=FinanceInsights.Review(s,new(),"finance.job-health",Day);var finding=Assert.Single(r.Findings);
        Assert.Equal("unknown",finding.Severity);Assert.Contains("profitability is unknown",finding.Message);Assert.Contains($"job:{job}",finding.SourceIds);Assert.DoesNotContain("gross profit",finding.Message);
    }
    [Fact]public void BillTaxAndShippingPreventReviewedJobFromClaimingCompleteProfitability()
    {
        var s=State();var job=Guid.NewGuid();s.Budgets.Add(new(job,"estimate",1000,0,new(){["MATERIAL"]=100},true,"Owner reviewed"));
        s.Bills.Add(new(){Id=Guid.NewGuid(),Date=Day,JournalId=Guid.NewGuid(),Tax=10,Shipping=5,Lines=[new("Material",1,100,"5000","MATERIAL",job)]});
        var r=FinanceInsights.Review(s,new(),"finance.job-health",Day);
        Assert.Contains(r.Findings,f=>f.Severity=="unknown"&&f.Message.Contains("tax or shipping remains unallocated"));
        Assert.DoesNotContain(r.Findings,f=>f.Message.Contains("gross profit"));
    }
    [Fact]public void CollectionsReviewNamesOnlyOverdueOutstandingSources()
    {
        var s=State();var overdue=Item(s,"AR",250,-5);Item(s,"AR",100,2);
        var r=FinanceInsights.Review(s,new(),"finance.ar",Day);Assert.Single(r.Findings);Assert.Contains("250.00 USD",r.Findings[0].Message);Assert.Equal([$"open-item:{overdue.Id}"],r.Findings[0].SourceIds);
        Assert.Throws<ArgumentException>(()=>FinanceInsights.Review(s,new(),"finance.post",Day));
    }
    [Fact]public void CommitmentSourcesRelieveOnlyPostedLinkedBillsAndIgnoreCancelledOrders()
    {
        var s=State();var job=Guid.NewGuid();var demand=Guid.NewGuid();var order=new PurchaseOrder{Number="PO-1",Status="APPROVED",Lines=[new(){DemandId=demand,Quantity=10,UnitCost=25,Description="Door hardware"}]};
        var supply=new SupplyState{Demands=[new(){Id=demand,JobId=job}],Orders=[order,new(){Number="CANCELLED",Status="CANCELLED",Lines=[new(){DemandId=demand,Quantity=20,UnitCost=25}]}]};
        s.Bills.Add(new(){OrderId=order.Id,Date=Day,Status="APPROVED",JournalId=Guid.NewGuid(),Lines=[new("Hardware",4,25,"5000",OrderLineId:order.Lines[0].Id)]});
        s.Bills.Add(new(){OrderId=order.Id,Date=Day,Status="PENDING_APPROVAL",Lines=[new("Unapproved",2,25,"5000",OrderLineId:order.Lines[0].Id)]});
        var source=Assert.Single(FinanceInsights.Commitments(s,supply,job,Day));Assert.Equal(6,source.UnbilledQuantity);Assert.Equal(150,source.Amount);Assert.Equal(order.Id,source.OrderId);Assert.Equal(order.Lines[0].Id,source.OrderLineId);
        Assert.Equal(250,Assert.Single(FinanceInsights.Commitments(s,supply,job,Day.AddDays(-1))).Amount);
    }
    [Fact]public void BobReviewFitsStoredResponseBudgetAndDisclosesOmittedSources()
    {
        var s=State();for(var n=0;n<20;n++)Item(s,"AR",10,-2);
        var r=FinanceInsights.Review(s,new(),"finance.ar",Day);
        Assert.Equal(5,r.Findings.Length);Assert.Contains("Showing the first 5",r.Summary);
        var minimized=new BobContextMinimizer().Minimize(r);Assert.False(minimized.TryGetProperty("truncated",out _));
        Assert.Null(FinanceInsights.Review(s,new(),"finance.cash",Day).Forecast);
    }

    [Fact]public void HistoricalMigratedCostsNeverBecomeCurrentPeriodMarginWithoutMatchedRevenue()
    {
        var s=State();var job=Guid.NewGuid();s.Budgets.Add(new(job,"estimate",1000,0,new(){["LABOR"]=200},true,"Reviewed"));s.Costs.Add(new(Guid.NewGuid(),job,"LABOR",null,200,Day,"migration-job-cost","external-cost"));
        var review=FinanceInsights.Review(s,new(),"finance.job-health",Day);Assert.Contains(review.Findings,f=>f.Severity=="unknown"&&f.Message.Contains("historical revenue matching"));
    }

    [Fact]public void MappedImportedCommitmentUsesRemainingValueInsteadOfOriginalPoQuantity()
    {
        var s=State();var job=Guid.NewGuid();var demand=Guid.NewGuid();var order=new PurchaseOrder{Number="PO-1",Status="APPROVED",Lines=[new(){DemandId=demand,Quantity=100,UnitCost=10}]};var supply=new SupplyState{Demands=[new(){Id=demand,JobId=job}],Orders=[order]};
        s.ImportedCommitments.Add(new("external-po",Guid.NewGuid(),job,Guid.NewGuid(),"PO-1","Remaining source balance","MATERIAL",200,Day,order.Id,order.Lines[0].Id,UnitCost:10));
        var source=Assert.Single(FinanceInsights.Commitments(s,supply,job,Day));Assert.Equal(200,source.Amount);Assert.Null(source.UnbilledQuantity);Assert.True(source.ImportedBaseline);
        Assert.Equal(source.Amount,FinanceMigrationCommitments.Remaining(s,supply,job,Day));
        s.Bills.Add(new(){OrderId=order.Id,Date=Day,JournalId=Guid.NewGuid(),Status="APPROVED",Lines=[new("Price variance",2,40,"5000",OrderLineId:order.Lines[0].Id)]});
        Assert.Equal(180,Assert.Single(FinanceInsights.Commitments(s,supply,job,Day)).Amount);Assert.Equal(180,FinanceMigrationCommitments.Remaining(s,supply,job,Day));
        order.Lines[0].UnitCost=99;Assert.Equal(180,Assert.Single(FinanceInsights.Commitments(s,supply,job,Day)).Amount);Assert.Equal(10,Assert.Single(FinanceInsights.Commitments(s,supply,job,Day)).UnitCost);
    }

    [Fact]public void SignedOpeningCreditsOffsetOnlySamePartySameLedgerAndNeverInventCashRefunds()
    {
        var s=State();var ar=Item(s,"AR",150,5);s.OpenItems.Add(new(Guid.NewGuid(),"AR",ar.PartyId,"AR-credit",Day,Day,-50,Guid.NewGuid()));
        var ap=Item(s,"AP",200,5);s.OpenItems.Add(new(Guid.NewGuid(),"AP",ap.PartyId,"AP-credit",Day,Day,-40,Guid.NewGuid()));s.OpenItems.Add(new(Guid.NewGuid(),"AR",Guid.NewGuid(),"Other-party-credit",Day,Day,-100,Guid.NewGuid()));
        var f=FinanceInsights.Forecast(s,Day);Assert.Equal(100,f.ReceiptsAssumed);Assert.Equal(160,f.PaymentsAssumed);Assert.Equal(940,f.DueDateScenario);Assert.Equal(840,f.ObligationsScenario);Assert.Equal(3,f.Sources.Count(s=>s.Kind=="unapplied-credit"));Assert.All(f.Sources.Where(s=>s.Kind=="unapplied-credit"),s=>Assert.Equal(0,s.Amount));
    }

}
