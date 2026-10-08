using System.Text.Json;
using Moq;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;

public sealed class FinanceTests
{
    private static readonly DateOnly Day=new(2026,9,15);
    public static FinanceState State(){var s=new FinanceState();FinanceLedger.Initialize(s);s.Periods.Add(new(){Id="2026-09",Start=new(2026,9,1),End=new(2026,9,30)});return s;}
    private static FinanceLine[] Lines(decimal amount=100)=>[new("1000",amount,0),new("3000",0,amount)];
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,JobConfigurationService.Json);
    [Fact]public void PostedBillRelievesCommitmentWithoutAddingActualAgain()
    {
        var s=State();var job=Guid.NewGuid();var demand=Guid.NewGuid();var order=Guid.NewGuid();var line=Guid.NewGuid();
        var supply=new SupplyState();supply.Demands.Add(new(){Id=demand,JobId=job});
        supply.Orders.Add(new(){Id=order,Status="APPROVED",Lines=[new(){Id=line,DemandId=demand,Quantity=10,UnitCost=5}]});
        var budget=new FinanceJobBudget(job,"estimate",100,0,new(){["MATERIAL"]=50},false,"");
        s.Bills.Add(new(){OrderId=order,Date=Day,Status="POSTED",Lines=[new("Material",4,5,"5000",OrderLineId:line)]});
        Assert.Equal(30,Json(FinanceService.JobHealth(s,supply,budget,Day)).GetProperty("committed").GetDecimal());
        Assert.Equal(50,Json(FinanceService.JobHealth(s,supply,budget,Day.AddDays(-1))).GetProperty("committed").GetDecimal());
        Assert.Equal(0,Json(FinanceService.JobHealth(s,supply,budget,Day)).GetProperty("actual").GetDecimal());
    }
    [Fact]public void RandomBalancedJournalsAndStatementsAlwaysBalance()
    {
        var s=State();var random=new Random(493);
        for(var n=0;n<500;n++){var amount=random.Next(1,1000000)/100m;FinanceLedger.Post(s,Day,"manual",n.ToString(),"Opening cash",Lines(amount),"owner");}
        FinanceIntegrity.ValidateTransition(new(),s);
        var report=Json(FinanceLedger.Reports(s,Day,Day));Assert.Equal(0,report.GetProperty("trialDifference").GetDecimal());Assert.Equal(0,report.GetProperty("balanceSheet").GetProperty("difference").GetDecimal());
    }
    [Theory][InlineData(100,99)][InlineData(-1,-1)][InlineData(0,0)][InlineData(0.001,0.001)]
    public void InvalidPostingRejected(decimal debit,decimal credit)=>Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(State(),Day,"manual","bad","Bad",[new("1000",debit,0),new("3000",0,credit)],"owner"));
    [Fact]public void MixedSidesAndUnknownAccountsRejected()
    {
        Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(State(),Day,"manual","bad","Bad",[new("1000",100,50),new("3000",0,50)],"owner"));
        Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(State(),Day,"manual","bad","Bad",[new("missing",100,0),new("3000",0,100)],"owner"));
    }
    [Fact]public void SourceReplayIsNoOpEvenAfterCloseButChangedContentFails()
    {
        var s=State();var entry=FinanceLedger.Post(s,Day,"manual","source","Opening",Lines(),"owner");s.Periods[0].Status="CLOSED";
        Assert.Equal(entry.Id,FinanceLedger.Post(s,Day,"manual","source","Opening",Lines(),"owner").Id);Assert.Single(s.Journals);
        Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(s,Day,"manual","source","Opening",Lines(200),"owner"));
        Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(s,Day,"manual","new","Opening",Lines(),"owner"));
    }
    [Fact]public void PostedHistoryCannotBeEditedOrDeleted()
    {
        var s=State();FinanceLedger.Post(s,Day,"manual","source","Opening",Lines(),"owner");var clone=JobConfigurationService.Clone(s);
        clone.Journals[0]=clone.Journals[0] with{Memo="Silent edit"};Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(s,clone));
        clone=JobConfigurationService.Clone(s);clone.Journals.Clear();Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(s,clone));
    }
    [Fact]public void MultiInvoicePartialAllocationAndRefundReconcile()
    {
        var s=State();var party=Guid.NewGuid();var first=AddAr(s,party,60,"a");var second=AddAr(s,party,40,"b");
        FinanceLedger.Settle(s,new(){Id=Guid.NewGuid(),Date=Day,Amount=50,PartyId=party,Account="operating",SourceKey="receipt",Reference="check",Allocations=[new(first.Id,30),new(second.Id,20)]},"owner","payment");
        Assert.Equal(30,FinanceLedger.Balance(s,first,Day));Assert.Equal(20,FinanceLedger.Balance(s,second,Day));
        FinanceLedger.Settle(s,new(){Id=Guid.NewGuid(),Date=Day,Amount=10,PartyId=party,Account="operating",SourceKey="refund",Reference="refund",Allocations=[new(first.Id,10)]},"owner","refund");
        FinanceIntegrity.ValidateTransition(new(),s);var report=Json(FinanceLedger.Reports(s,Day,Day));Assert.Equal(60,report.GetProperty("ar").GetDecimal());Assert.Equal(0,report.GetProperty("arDifference").GetDecimal());
    }
    [Fact]public void AllocationRejectsOtherPartyOverpaymentAndExcessRefund()
    {
        var s=State();var party=Guid.NewGuid();var item=AddAr(s,party,100,"a");
        FinanceCommand Command(decimal amount,Guid person)=>new(){Id=Guid.NewGuid(),Date=Day,Amount=amount,PartyId=person,Account="operating",SourceKey="receipt",Reference="check",Allocations=[new(item.Id,amount)]};
        Assert.Throws<ArgumentException>(()=>FinanceLedger.Settle(s,Command(1,Guid.NewGuid()),"owner","payment"));Assert.Throws<ArgumentException>(()=>FinanceLedger.Settle(s,Command(101,party),"owner","payment"));Assert.Throws<ArgumentException>(()=>FinanceLedger.Settle(s,Command(1,party),"owner","refund"));
    }
    private static FinanceOpenItem AddAr(FinanceState s,Guid party,decimal total,string source)
    {
        var journal=FinanceLedger.Post(s,Day,"invoice",source,source,[new("1100",total,0,CustomerId:party),new("4000",0,total)],"owner");var item=new FinanceOpenItem(Guid.NewGuid(),"AR",party,source,Day,Day,total,journal.Id);s.OpenItems.Add(item);return item;
    }
    [Fact]public void ThreeWayMatchDetectsPriceQuantityAndMissingReceipt()
    {
        var vendor=Guid.NewGuid();var order=new PurchaseOrder{VendorId=vendor,Lines=[new(){Quantity=2,UnitCost=10}]};var supply=new SupplyState{Orders=[order]};
        var bill=new FinanceBill{VendorId=vendor,Number="A",OrderId=order.Id,Lines=[new("Material",3,11,"5000",OrderLineId:order.Lines[0].Id)]};
        var exceptions=FinanceLedger.Match(bill,supply,[]);Assert.Contains("Unit cost variance.",exceptions);Assert.Contains("Billed quantity exceeds PO quantity.",exceptions);Assert.Contains("Missing or insufficient receipt.",exceptions);Assert.Contains("Bill exceeds PO total.",exceptions);
        Assert.Contains("Duplicate vendor invoice number.",FinanceLedger.Match(bill,supply,[new(){VendorId=vendor,Number=" a "}]));
    }
    [Fact]public void ReconciledCashIntervalCannotReceiveNewActivity()
    {
        var s=State();s.Reconciliations.Add(new("r","operating",Day,Day,0,0,"owner",DateTime.UtcNow));Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(s,Day,"manual","new","New",Lines(),"owner"));
    }
    [Fact]public void StaffAndOperationalGrantsDoNotAutomaticallyGrantFinance()
    {
        var staff=new TenantMembership{MembershipStatus="Active",Role="staff"};Assert.DoesNotContain("finance.read",UserModulePermissions.Resolve(staff,null));
        Assert.False(UserModulePermissions.Allows(["jobs.read","jobs.write","invoices.read","billing.read"],"finance",false));
        var profile=new UserProfile{IsActive=true,ModulePermissions=["jobs.read"]};Assert.DoesNotContain("finance.read",UserModulePermissions.Resolve(staff,profile));
    }
    [Fact]public async Task InvoiceSyncPaymentReplayAndAsOfReportsReconcile()
    {
        var f=new Fixture();var customer=Guid.NewGuid();var invoice=new InvoiceDto{Id=Guid.NewGuid(),CustomerId=customer,InvoiceNumber="INV-1",Status=InvoiceStatus.Sent,IssueDate=Day.ToDateTime(TimeOnly.MinValue),DueDate=Day.AddDays(30).ToDateTime(TimeOnly.MinValue),Subtotal=100,TaxAmount=10,Total=110,Payments=[new(){Id=Guid.NewGuid(),Amount=40,IdempotencyKey="stripe:event",OccurredAtUtc=Day.ToDateTime(TimeOnly.MinValue)}]};
        f.Invoices.Setup(i=>i.GetPagedAsync(100,null)).ReturnsAsync((new[]{invoice}.AsEnumerable(),(string?)null));
        await f.Do(new(){Action="sync-invoices"});await f.Do(new(){Action="sync-invoices"});Assert.Equal(2,f.State.Journals.Count);Assert.Single(f.State.Settlements);Assert.Equal(70,FinanceLedger.Balance(f.State,f.State.OpenItems.Single(),Day));
        var report=Json(FinanceLedger.Reports(f.State,Day,Day));Assert.Equal(0,report.GetProperty("arDifference").GetDecimal());Assert.Equal(100,report.GetProperty("profit").GetDecimal());
    }
    [Fact]public async Task ApBillAndPartialPaymentCreateOneCostAndReconcile()
    {
        var f=new Fixture();var vendor=Guid.NewGuid();var job=Guid.NewGuid();f.Supply.Vendors.Add(new(){ContactId=vendor});f.Jobs.Setup(j=>j.GetAsync(job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=job});
        var bill=new FinanceBill{VendorId=vendor,Number="V-1",Date=Day,DueDate=Day.AddDays(30),Lines=[new("Material",1,100,"5000",JobId:job)]};await f.Do(new(){Action="bill",Bill=bill});await f.Do(new(){Action="approve-bill",Target=bill.Id.ToString()});
        var payment=new FinanceCommand{Action="vendor-payment",Date=Day,Amount=30,PartyId=vendor,Account="operating",SourceKey="check1",Reference="check1",Allocations=[new(bill.Id,30)]};await f.Do(payment);await f.Do(payment);
        Assert.Single(f.State.Costs);Assert.Equal(70,FinanceLedger.Balance(f.State,f.State.OpenItems.Single(),Day));Assert.Equal(2,f.State.Journals.Count);Assert.Equal(0,Json(FinanceLedger.Reports(f.State,Day,Day)).GetProperty("apDifference").GetDecimal());
    }
    [Fact]public async Task StaleCommandsAndUnauthorizedControlsDoNotCommit()
    {
        var f=new Fixture();await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.CommandAsync(new(){Action="period",ExpectedVersion="stale"}));
        f.Authority.Setup(a=>a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Do(new(){Action="close",Target="2026-09",Reason="Ready"}));Assert.Equal("OPEN",f.State.Periods[0].Status);
        f.Authority.Setup(a=>a.RequireAsync(It.IsAny<bool>(),It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Denied"));await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.WorkspaceAsync());
    }
    [Fact]public async Task CloseReopenAuditAndReversalPreserveOriginal()
    {
        var f=new Fixture();await f.Do(new(){Action="post-journal",Date=Day,Reason="Opening",Lines=Lines()});var original=f.State.Journals.Single();await f.Do(new(){Action="close",Target="2026-09",Reason="Reviewed"});
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Do(new(){Action="post-journal",Date=Day,Reason="Late",Lines=Lines()}));await f.Do(new(){Action="reopen",Target="2026-09",Reason="Authorized correction"});await f.Do(new(){Action="reverse",Date=Day,Target=original.Id.ToString(),Reason="Correction"});
        Assert.Equal(FinanceLedger.Hash(original),FinanceLedger.Hash(f.State.Journals.First()));Assert.Equal(original.Id,f.State.Journals.Last().ReversesId);Assert.Contains(f.State.Audit,a=>a.Action=="reopen");
    }
    [Fact]public async Task OpeningImportRequiresBalancedLedgerAndSourceControls()
    {
        var f=new Fixture();var item=new FinanceOpenItem(Guid.NewGuid(),"AR",Guid.NewGuid(),"OLD-1",Day,Day,100,Guid.Empty,ExternalId:"acumatica:1");
        var command=new FinanceCommand{Action="opening-import",Date=Day,SourceKey="migration1",Reason="Reconciled source",ArControl=99,ImportOpenItems=[item],Lines=[new("1100",100,0),new("3000",0,100)]};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Do(command));Assert.Empty(f.State.Journals);command.ArControl=100;await f.Do(command);Assert.Single(f.State.OpenItems);Assert.Equal(0,Json(FinanceLedger.Reports(f.State,Day,Day)).GetProperty("arDifference").GetDecimal());
    }
    [Fact]public async Task DuplicateBankImportRequiresHumanMatchAndReconciliation()
    {
        var f=new Fixture();await f.Do(new(){Action="post-journal",Date=Day,Reason="Cash",Lines=Lines()});var journal=f.State.Journals.Single();
        FinanceCommand Import()=>new(){Action="bank-import",BankTransactions=[new(){CashAccountId="operating",ImportId="statement",ExternalId="row1",Date=Day,Amount=100,Reference="deposit"}]};
        await f.Do(Import());await f.Do(Import());Assert.Single(f.State.BankTransactions);Assert.Null(f.State.BankTransactions[0].JournalId);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Do(new(){Action="reconcile",Date=Day,EndDate=Day,Amount=100,Account="operating",Reference="statement"}));
        await f.Do(new(){Action="bank-match",Target=f.State.BankTransactions[0].Id.ToString(),Reference=journal.Id.ToString(),Reason="Verified statement"});
        await f.Do(new(){Action="reconcile",Date=Day,EndDate=Day,Amount=100,Account="operating",Reference="statement"});Assert.Single(f.State.Reconciliations);
    }
    [Fact]public async Task AcceptedEstimateBudgetUsesCostAndKeepsMissingCostUnknown()
    {
        var f=new Fixture();var id=Guid.NewGuid();f.Jobs.Setup(j=>j.GetAsync(id,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=id,AcceptedEstimate=new(){EstimateId=Guid.NewGuid(),Revision=3,DocumentHash="immutable",SelectedOptions=[new(){Total=1100,Tax=100,Lines=[new(){Kind="MATERIAL",Total=700,Cost=400},new(){Kind="LABOR",Total=300,Cost=null}]}]}});
        await f.Do(new(){Action="seed-budget",JobId=id});var b=f.State.Budgets.Single();Assert.Equal(1000,b.Sold);Assert.Equal(400,b.Categories["MATERIAL"]);Assert.Null(b.Categories["LABOR"]);Assert.False(b.ActualsComplete);Assert.Empty(f.State.Journals);
    }
    [Fact]public async Task ManualJournalCannotBypassArControlOrDraftApproval()
    {
        var f=new Fixture();await Assert.ThrowsAsync<ArgumentException>(()=>f.Do(new(){Action="post-journal",Date=Day,Reason="Bypass",Lines=[new("1100",100,0),new("4000",0,100)]}));
        await f.Do(new(){Action="draft-journal",Date=Day,Reason="Unbalanced draft",Lines=[new("1000",100,0),new("3000",0,99)]});Assert.Empty(f.State.Journals);Assert.Single(f.State.JournalDrafts);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Do(new(){Action="post-draft",Target=f.State.JournalDrafts[0].Id.ToString()}));Assert.Empty(f.State.Journals);
    }
    [Fact]public async Task ReportAndBobCannotBypassFinanceAuthority()
    {
        var f=new Fixture();f.Authority.Setup(a=>a.RequireAsync(false,It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Denied"));
        var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(Guid.NewGuid());user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());
        var provider=new BobFinanceActionProvider(f.Service,user.Object,"finance.ar");Assert.Equal("finance.read",provider.PermissionKey);Assert.Equal(BobActionRisk.Read,provider.Risk);
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>provider.ExecuteAsync(new(user.Object.TenantId,user.Object.UserId,Guid.NewGuid(),""),Json(new{})));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>provider.ExecuteAsync(new(Guid.NewGuid(),user.Object.UserId,Guid.NewGuid(),""),Json(new{})));
        var draft=new BobFinanceActionProvider(f.Service,user.Object,"finance.draft-journal");Assert.Equal(BobActionRisk.Financial,draft.Risk);Assert.True(BobOperationsService.RequiresConfirmation(draft.Risk));
    }
    [Fact]public void CreditsPreserveTaxAndJobCostDimensionsAndReconcileControls()
    {
        var s=State();var party=Guid.NewGuid();var invoiceId=Guid.NewGuid();var j=FinanceLedger.Post(s,Day,"invoice",invoiceId.ToString(),"Invoice",[new("1100",110,0),new("4000",0,100),new("2100",0,10)],"owner");s.OpenItems.Add(new(invoiceId,"AR",party,"Invoice",Day,Day,110,j.Id));
        var credit=FinanceLedger.Settle(s,new(){Id=Guid.NewGuid(),Date=Day,Amount=55,PartyId=party,SourceKey="credit1",Reference="Credit",Allocations=[new(invoiceId,55)]},"owner","credit");Assert.Equal(50,credit.Lines.Single(l=>l.Account=="4000").Debit);Assert.Equal(5,credit.Lines.Single(l=>l.Account=="2100").Debit);
        var vendor=Guid.NewGuid();var job=Guid.NewGuid();var billId=Guid.NewGuid();j=FinanceLedger.Post(s,Day,"vendor-bill",billId.ToString(),"Bill",[new("5000",100,0,job,Category:"MATERIAL"),new("2000",0,100)],"owner");s.OpenItems.Add(new(billId,"AP",vendor,"Bill",Day,Day,100,j.Id));s.Costs.Add(new(Guid.NewGuid(),job,"MATERIAL",null,100,Day,"vendor-bill",billId.ToString(),j.Id));
        FinanceLedger.Settle(s,new(){Id=Guid.NewGuid(),Date=Day,Amount=30,PartyId=vendor,SourceKey="vc1",Reference="Vendor credit",Allocations=[new(billId,30)]},"owner","vendor-credit");Assert.Equal(70,s.Costs.Sum(c=>c.Amount));FinanceIntegrity.ValidateTransition(new(),s);
    }
    private sealed class Fixture
    {
        public FinanceState State=FinanceTests.State();public SupplyState Supply=new();public Mock<IInvoiceService> Invoices=new();public Mock<IJobService> Jobs=new();public Mock<IFinanceAuthority> Authority=new();public FinanceService Service;
        public Fixture()
        {
            var store=new Mock<IFinanceStore>();var supply=new Mock<ISupplyStore>();var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(Guid.NewGuid());user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());
            Authority.Setup(a=>a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            store.Setup(s=>s.ReadAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(State));
            store.Setup(s=>s.SaveAsync(It.IsAny<Guid>(),It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((Guid t,FinanceState next,string expected,CancellationToken ct)=>{Assert.Equal(State.Version,expected);FinanceIntegrity.ValidateTransition(State,next);next.Version=Guid.NewGuid().ToString();State=next;return Task.CompletedTask;});
            supply.Setup(s=>s.ReadAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Supply);
            Jobs.Setup(j=>j.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<JobDto>());
            Invoices.Setup(i=>i.GetPagedAsync(100,null)).ReturnsAsync((Array.Empty<InvoiceDto>().AsEnumerable(),(string?)null));
            var customers=new Mock<TurnKeyOps.Repositories.Interfaces.ICustomerRepository>();customers.Setup(r=>r.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string partition,string row,CancellationToken ct)=>new TurnKeyOps.Lib.Entities.Customer{Id=Guid.TryParse(row,out var id)?id:Guid.Empty,PartitionKey=partition});
            Service=new(store.Object,Authority.Object,user.Object,Invoices.Object,Jobs.Object,supply.Object,customers.Object);
        }
        public Task<object> Do(FinanceCommand command){command.ExpectedVersion=State.Version;return Service.CommandAsync(command);}
    }
}
