using System.Text.Json;
using MedInsights.Lib;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;

public sealed class FinanceMigrationTests
{
    [Fact] public async Task DryRunReconcilesWithoutWritingAndRequiresExactApproval()
    {
        var f = new Fixture(); var request = f.Request(); var report = await f.Service.PreviewAsync(request);
        Assert.True(report.CanImport, string.Join(" ", report.Errors)); Assert.All(report.Checks, c => Assert.Equal(0, c.Difference));
        Assert.Empty(f.State.Journals); Assert.Empty(f.State.Costs); Assert.Equal(0, f.Saves);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ImportAsync(request));
        request.ApprovedFingerprint = report.Fingerprint; request.Reason = "Different review";
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ImportAsync(request)); Assert.Equal(0, f.Saves);
    }
    [Fact] public async Task AtomicImportPreservesSourceAndCostsWithoutDoubleExpenseAndIsIdempotent()
    {
        var f = new Fixture(); var request = f.Request(); await f.Import(request);
        Assert.Single(f.State.Journals); Assert.Single(f.State.MigrationBatches); Assert.Equal(2, f.State.OpenItems.Count);
        Assert.Single(f.State.Costs); Assert.Equal("migration-job-cost", f.State.Costs[0].SourceType); Assert.Equal(0, FinanceLedger.AccountBalance(f.State, "5000", request.CutoverDate));
        Assert.Equal(80, f.State.Costs[0].Amount); Assert.Single(f.State.ImportedCommitments); Assert.Single(f.State.ImportedInventory);
        Assert.Contains("source-customer", f.State.MigrationBatches[0].SourcePayloadJson);
        Assert.Equal(0, FinanceLedger.AccountBalance(f.State, "1100", request.CutoverDate) - f.State.OpenItems.Where(i => i.Kind == "AR").Sum(i => i.Total));
        Assert.Equal(1, f.Saves); await f.Service.ImportAsync(request); Assert.Equal(1, f.Saves);
        request.JobCosts[0] = request.JobCosts[0] with { Amount = 81 };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ImportAsync(request));
    }
    [Fact] public async Task ControlDifferencesReturnReviewableTotalsAndNeverCommit()
    {
        var f = new Fixture(); var request = f.Request(); request.Controls.Ar = 99;
        var report = await f.Service.PreviewAsync(request); Assert.False(report.CanImport);
        Assert.Contains(report.Checks, c => c.Name == "AR source" && c.Difference == 1);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ImportAsync(request)); Assert.Empty(f.State.Journals);
    }
    [Theory] [InlineData("cash")] [InlineData("inventory")] [InlineData("job-cost")] [InlineData("commitment")]
    public async Task EverySecondaryControlMustReconcile(string mismatch)
    {
        var f = new Fixture(); var request = f.Request();
        switch (mismatch) { case "cash": request.Controls.Cash["operating"]++; break; case "inventory": request.Controls.Inventory++; break; case "job-cost": request.Controls.JobCosts++; break; default: request.Controls.Commitments++; break; }
        var report = await f.Service.PreviewAsync(request); Assert.False(report.CanImport); Assert.Contains(report.Checks, c => c.Difference != 0);
    }
    [Fact] public async Task SignedCustomerVendorCreditsRemainInOpeningControls()
    {
        var f = new Fixture(); var request = f.Request();
        request.OpenItems = [.. request.OpenItems, new("customer-credit", "AR", "source-customer", "CM-1", request.CutoverDate, request.CutoverDate, -20), new("vendor-credit", "AP", "source-vendor", "VC-1", request.CutoverDate, request.CutoverDate, -10)];
        request.TrialBalance = [new("source-ar",80,0), new("source-ap",0,30), new("source-cash",50,0), new("source-stock",20,0), new("source-equity",0,120)];
        request.Controls.Ar = 80; request.Controls.Ap = 30; await f.Import(request);
        Assert.Equal(80, f.State.OpenItems.Where(i => i.Kind == "AR").Sum(i => FinanceLedger.Balance(f.State,i,request.CutoverDate)));
        Assert.Equal(30, f.State.OpenItems.Where(i => i.Kind == "AP").Sum(i => FinanceLedger.Balance(f.State,i,request.CutoverDate)));
        FinanceIntegrity.ValidateTransition(new(), f.State);
    }
    [Fact] public async Task TenantEntityMappingCannotLeakOrAttachForeignCustomerJobVendor()
    {
        var f = new Fixture(); var request = f.Request();
        f.Customers.Setup(c => c.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new Customer{Id=f.Customer,PartitionKey="another-tenant"});
        Assert.False((await f.Service.PreviewAsync(request)).CanImport);
        f.Customers.Setup(c => c.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);
        Assert.False((await f.Service.PreviewAsync(request)).CanImport);
        f.Jobs.Setup(j => j.GetAsync(f.Job,It.IsAny<CancellationToken>())).ReturnsAsync((JobDto?)null);
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors, e => e.Contains("Job mapping"));
        request.Entities[1] = request.Entities[1] with { Id = Guid.NewGuid() };
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors, e => e.Contains("Vendor mapping"));
    }
    [Fact] public async Task HistoricalOverlapAndClosedPeriodAreRejected()
    {
        var f = new Fixture(); var request = f.Request(); f.State.Periods[0].Status = "CLOSED";
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors, e => e.Contains("closed"));
        f.State.Periods[0].Status = "OPEN"; FinanceLedger.Post(f.State,request.CutoverDate,"manual","live","Already live",[new("1000",1,0),new("3000",0,1)],"owner");
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors, e => e.Contains("overlap"));
    }
    [Theory] [InlineData("item")] [InlineData("location")] [InlineData("quantity")]
    public async Task InventoryMustMapAndReconcileOperationalSupplyBeforeFinance(string bad)
    {
        var f = new Fixture(); var request = f.Request(); request.Inventory[0] = bad switch { "item" => request.Inventory[0] with { ItemId="foreign" }, "location" => request.Inventory[0] with { LocationId=Guid.NewGuid() }, _ => request.Inventory[0] with { Quantity=9 } };
        Assert.False((await f.Service.PreviewAsync(request)).CanImport); Assert.Empty(f.State.Journals);
    }
    [Fact] public async Task StaleFinanceOrSupplyAndUnauthorizedOwnerCannotCommit()
    {
        var f = new Fixture(); var request = f.Request(); request.ExpectedVersion = "stale"; Assert.False((await f.Service.PreviewAsync(request)).CanImport);
        request.ExpectedVersion = f.State.Version; request.ExpectedSupplyVersion = "stale"; Assert.False((await f.Service.PreviewAsync(request)).CanImport);
        request.ExpectedSupplyVersion = f.Supply.Version; request.ApprovedFingerprint = (await f.Service.PreviewAsync(request)).Fingerprint;
        f.Authority.Setup(a => a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => f.Service.ImportAsync(request)); Assert.Equal(0,f.Saves);
        f.Authority.Setup(a => a.RequireAsync(false,It.IsAny<CancellationToken>())).ThrowsAsync(new ForbiddenAccessException("Denied"));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => f.Service.PreviewAsync(request));
    }
    [Fact] public async Task DuplicateIdsAndUnmappedAccountsNeverImport()
    {
        var f = new Fixture(); var request = f.Request(); request.OpenItems = [..request.OpenItems,request.OpenItems[0]];
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors,e=>e.Contains("Duplicate"));
        request = f.Request(); request.Accounts = request.Accounts.Where(a=>a.ExternalAccount!="source-ar").ToArray();
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors,e=>e.Contains("Unmapped"));
    }
    [Fact] public async Task SourceTrialBalanceMustBalanceAndMetadataMustBePreserved()
    {
        var f = new Fixture(); var request=f.Request(); request.TrialBalance[0]=request.TrialBalance[0] with{Debit=101};
        Assert.Contains((await f.Service.PreviewAsync(request)).Errors,e=>e.Contains("must balance"));
        request=f.Request();request.SourceFileSha256="unknown";Assert.False((await f.Service.PreviewAsync(request)).CanImport);
        request=f.Request();request.Strategy="FULL_HISTORY";Assert.False((await f.Service.PreviewAsync(request)).CanImport);
    }
    [Fact] public async Task CanonicalInvoiceIdentityIsValidatedAndPreservedForFutureEvents()
    {
        var f=new Fixture();var request=f.Request();var invoiceId=Guid.NewGuid();request.OpenItems[0]=request.OpenItems[0] with{ExistingInvoiceId=invoiceId,ExternalJobId="source-job"};
        f.Invoices.Setup(i=>i.GetAsync(invoiceId)).ReturnsAsync(new InvoiceDto{Id=invoiceId,CustomerId=f.Customer,JobId=f.Job,IssueDate=request.CutoverDate.ToDateTime(TimeOnly.MinValue),Total=150,Status=InvoiceStatus.Sent});
        await f.Import(request);Assert.Equal(invoiceId,f.State.OpenItems.Single(i=>i.Kind=="AR").Id);Assert.Equal(100,f.State.OpenItems.Single(i=>i.Kind=="AR").Total);
    }
    [Fact] public async Task ImportedHistoryCannotBeEditedOrDeleted()
    {
        var f=new Fixture();await f.Import(f.Request());var copy=Clone(f.State);copy.ImportedInventory.Clear();Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(f.State,copy));
        copy=Clone(f.State);copy.ImportedCommitments[0]=copy.ImportedCommitments[0] with{RemainingAmount=31};Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(f.State,copy));
        copy=Clone(f.State);copy.MigrationBatches.Clear();Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(f.State,copy));
    }
    [Fact] public async Task MappedPoCommitmentRequiresExactVendorJobLineAndRemainingCap()
    {
        var f=new Fixture();var request=f.Request();var order=Guid.NewGuid();var line=Guid.NewGuid();var demand=Guid.NewGuid();
        f.Supply.Demands.Add(new(){Id=demand,JobId=f.Job});f.Supply.Orders.Add(new(){Id=order,VendorId=f.Vendor,Status="APPROVED",Lines=[new(){Id=line,DemandId=demand,Quantity=10,UnitCost=5}]});
        request.Commitments[0]=request.Commitments[0] with{SupplyOrderId=order,SupplyOrderLineId=line};Assert.True((await f.Service.PreviewAsync(request)).CanImport);
        request.Commitments[0]=request.Commitments[0] with{RemainingAmount=51};Assert.False((await f.Service.PreviewAsync(request)).CanImport);
    }
    [Fact] public async Task ImportedCommitmentsReplaceMappedPoAndRelieveOnlyExplicitBills()
    {
        var f=new Fixture();var request=f.Request();var order=Guid.NewGuid();var line=Guid.NewGuid();var demand=Guid.NewGuid();
        f.Supply.Demands.Add(new(){Id=demand,JobId=f.Job});f.Supply.Orders.Add(new(){Id=order,VendorId=f.Vendor,Status="APPROVED",Lines=[new(){Id=line,DemandId=demand,Quantity=10,UnitCost=5}]});
        request.Commitments[0]=request.Commitments[0] with{SupplyOrderId=order,SupplyOrderLineId=line};await f.Import(request);
        Assert.Equal(30,FinanceMigrationCommitments.Remaining(f.State,f.Supply,f.Job,request.CutoverDate));
        f.State.Bills.Add(new(){Id=Guid.NewGuid(),VendorId=f.Vendor,OrderId=order,Status="POSTED",Date=request.CutoverDate,Lines=[new("new material",2,20,"5000",JobId:f.Job,OrderLineId:line)]});
        Assert.Equal(20,FinanceMigrationCommitments.Remaining(f.State,f.Supply,f.Job,request.CutoverDate));
        Assert.Equal(5,f.State.ImportedCommitments.Single().UnitCost);
        var overbilled=new FinanceBill{VendorId=f.Vendor,OrderId=order,Date=request.CutoverDate,Lines=[new("Excess source quantity",5,1,"5000",JobId:f.Job,OrderLineId:line)]};
        Assert.Throws<ArgumentException>(()=>FinanceMigrationCommitments.ValidateBill(f.State,overbilled));
        overbilled.Lines=[overbilled.Lines[0] with{Quantity=1,JobId=Guid.NewGuid()}];Assert.Throws<ArgumentException>(()=>FinanceMigrationCommitments.ValidateBill(f.State,overbilled));
        f.Supply.Orders.Single().Lines.Single().UnitCost=100;
        Assert.Equal(20,FinanceMigrationCommitments.Remaining(f.State,f.Supply,f.Job,request.CutoverDate));
    }
    [Fact] public async Task UnmappedCommitmentBillMustExplicitlyMatchJobVendorAndRemainingAmount()
    {
        var f=new Fixture();var request=f.Request();await f.Import(request);var commitment=f.State.ImportedCommitments.Single();
        var bill=new FinanceBill{VendorId=f.Vendor,Date=request.CutoverDate,Lines=[new("Imported order",1,20,"5000",JobId:f.Job,ImportedCommitmentId:commitment.Id)]};
        FinanceMigrationCommitments.ValidateBill(f.State,bill);bill.Status="POSTED";f.State.Bills.Add(bill);
        Assert.Equal(10,FinanceMigrationCommitments.Remaining(f.State,f.Supply,f.Job,request.CutoverDate));
        var another=new FinanceBill{VendorId=f.Vendor,Date=request.CutoverDate,Lines=[new("Imported order",1,11,"5000",JobId:f.Job,ImportedCommitmentId:commitment.Id)]};
        Assert.Throws<ArgumentException>(()=>FinanceMigrationCommitments.ValidateBill(f.State,another));
        another.Lines=[another.Lines[0] with{UnitCost=1,JobId=Guid.NewGuid()}];Assert.Throws<ArgumentException>(()=>FinanceMigrationCommitments.ValidateBill(f.State,another));
    }
    [Fact] public async Task InvoiceEventsAfterCutoverUseImportedBalanceWithoutRepostingHistory()
    {
        var f=new Fixture();var request=f.Request();var invoiceId=Guid.NewGuid();request.OpenItems[0]=request.OpenItems[0] with{ExistingInvoiceId=invoiceId};
        var invoice=new InvoiceDto{Id=invoiceId,CustomerId=f.Customer,IssueDate=new DateTime(2026,9,1),DueDate=new DateTime(2026,9,30),Subtotal=150,Total=150,Status=InvoiceStatus.Sent,InvoiceNumber="AR-1",
            Payments=[new(){Id=Guid.NewGuid(),Amount=50,OccurredAtUtc=new DateTime(2026,9,20),IdempotencyKey="historical"},new(){Id=Guid.NewGuid(),Amount=30,OccurredAtUtc=new DateTime(2026,10,1),IdempotencyKey="new-event"}]};
        f.Invoices.Setup(i=>i.GetAsync(invoiceId)).ReturnsAsync(invoice);await f.Import(request);
        f.State.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});
        FinanceInvoicePosting.Apply(f.State,invoice,"owner");FinanceInvoicePosting.Apply(f.State,invoice,"owner");
        Assert.Equal(2,f.State.Journals.Count);Assert.DoesNotContain(f.State.Journals,j=>j.SourceType=="invoice");Assert.Single(f.State.Settlements);
        Assert.Equal(70,FinanceLedger.Balance(f.State,f.State.OpenItems.Single(i=>i.Id==invoiceId),new(2026,10,1)));FinanceIntegrity.ValidateTransition(new(),f.State);
    }
    [Fact] public async Task ImportHoldsAndReleasesTheSourceFenceOnSuccessAndApprovalFailure()
    {
        var coordinator=new Mock<IFinanceSourceCoordinator>();var fence=new Mock<IAsyncDisposable>();
        coordinator.Setup(c=>c.AcquireAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(fence.Object);
        var f=new Fixture(coordinator.Object);var request=f.Request();await f.Import(request);
        coordinator.Verify(c=>c.AcquireAsync(f.Tenant,It.IsAny<CancellationToken>()),Times.Once);fence.Verify(d=>d.DisposeAsync(),Times.Once);
        var other=new Fixture(coordinator.Object);await Assert.ThrowsAsync<ArgumentException>(()=>other.Service.ImportAsync(other.Request()));
        fence.Verify(d=>d.DisposeAsync(),Times.Exactly(2));Assert.Equal(0,other.Saves);
    }
    [Fact] public async Task ExplicitZeroBalanceCanonicalArMappingPreservesPaidSourceEvidenceAndLaterRefund()
    {
        var f=new Fixture();var request=f.Request();var invoiceId=Guid.NewGuid();
        request.OpenItems[0]=request.OpenItems[0] with{Balance=0,ExistingInvoiceId=invoiceId};request.Controls.Ar=0;
        request.TrialBalance=[new("source-ap",0,40),new("source-cash",50,0),new("source-stock",20,0),new("source-equity",0,30)];
        var invoice=new InvoiceDto{Id=invoiceId,CustomerId=f.Customer,IssueDate=new DateTime(2026,9,1),DueDate=new DateTime(2026,9,30),Subtotal=150,Total=150,Status=InvoiceStatus.Paid,InvoiceNumber="AR-1"};
        f.Invoices.Setup(i=>i.GetAsync(invoiceId)).ReturnsAsync(invoice);await f.Import(request);Assert.Equal(0,f.State.OpenItems.Single(i=>i.Id==invoiceId).Total);
        f.State.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});
        invoice.Payments=[new(){Id=Guid.NewGuid(),Kind="refund",Amount=10,OccurredAtUtc=new DateTime(2026,10,1),IdempotencyKey="new-refund"}];
        FinanceInvoicePosting.Apply(f.State,invoice,"owner");Assert.Equal(10,FinanceLedger.Balance(f.State,f.State.OpenItems.Single(i=>i.Id==invoiceId),new(2026,10,1)));FinanceIntegrity.ValidateTransition(new(),f.State);
    }
    [Theory] [InlineData("AR")] [InlineData("AP")]
    public async Task ImportedCreditsApplyToSamePartyWithoutInventingCashExpenseOrTax(string kind)
    {
        var f=new Fixture();var request=f.Request();var party=kind=="AR"?"source-customer":"source-vendor";
        request.OpenItems=[..request.OpenItems,new("source-credit",kind,party,"Credit",request.CutoverDate,request.CutoverDate,-10)];
        request.Controls.Ar=kind=="AR"?90:100;request.Controls.Ap=kind=="AP"?30:40;
        request.TrialBalance=[new("source-ar",request.Controls.Ar,0),new("source-ap",0,request.Controls.Ap),new("source-cash",50,0),new("source-stock",20,0),new("source-equity",0,request.Controls.Ar+70-request.Controls.Ap)];
        await f.Import(request);var original=Clone(f.State);var credit=f.State.OpenItems.Single(i=>i.Total<0);var target=f.State.OpenItems.Single(i=>i.Kind==kind&&i.Total>0);
        var command=new FinanceOpeningCreditApplication{ExpectedVersion=f.State.Version,CreditItemId=credit.Id,TargetItemId=target.Id,Amount=10,Date=request.CutoverDate,Reason="Apply reviewed source credit"};
        await f.Service.ApplyOpeningCreditAsync(command);await f.Service.ApplyOpeningCreditAsync(command);
        Assert.Equal(2,f.Saves);Assert.Equal(0,FinanceLedger.Balance(f.State,credit,DateOnly.MaxValue));Assert.Equal(target.Total-10,FinanceLedger.Balance(f.State,target,DateOnly.MaxValue));
        foreach(var account in f.State.Accounts)Assert.Equal(FinanceLedger.AccountBalance(original,account.Code,DateOnly.MaxValue),FinanceLedger.AccountBalance(f.State,account.Code,DateOnly.MaxValue));
        Assert.Equal(original.Costs.Count,f.State.Costs.Count);Assert.Single(f.State.Settlements);Assert.Contains(f.State.Settlements.Single().Allocations,a=>a.OpenItemId==credit.Id&&a.Amount==-10);
        FinanceIntegrity.ValidateTransition(original,f.State);command.Amount=9;await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyOpeningCreditAsync(command));
    }
    [Fact] public async Task OpeningCreditApplicationRejectsCrossPartyOverapplicationStaleVersionAndClosedPeriod()
    {
        var f=new Fixture();var request=f.Request();request.OpenItems=[..request.OpenItems,new("source-credit","AR","source-customer","Credit",request.CutoverDate,request.CutoverDate,-10)];
        request.Controls.Ar=90;request.TrialBalance=[new("source-ar",90,0),new("source-ap",0,40),new("source-cash",50,0),new("source-stock",20,0),new("source-equity",0,120)];await f.Import(request);
        var credit=f.State.OpenItems.Single(i=>i.Total<0);var target=f.State.OpenItems.Single(i=>i.Kind=="AR"&&i.Total>0);
        var command=new FinanceOpeningCreditApplication{ExpectedVersion=f.State.Version,CreditItemId=credit.Id,TargetItemId=target.Id,Amount=11,Date=request.CutoverDate,Reason="Reviewed credit"};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyOpeningCreditAsync(command));command.Amount=10;command.TargetItemId=f.State.OpenItems.Single(i=>i.Kind=="AP").Id;
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyOpeningCreditAsync(command));command.TargetItemId=target.Id;command.ExpectedVersion="stale";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ApplyOpeningCreditAsync(command));command.ExpectedVersion=f.State.Version;f.State.Periods[0].Status="CLOSED";
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyOpeningCreditAsync(command));Assert.Equal(1,f.Saves);
        f.Authority.Setup(a=>a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Service.ApplyOpeningCreditAsync(command));
    }
    [Theory] [InlineData("AR")] [InlineData("AP")]
    public async Task ImportedCreditUnapplyAppendsReversalAndRestoresBothBalances(string kind)
    {
        var f=new Fixture();var request=f.Request();request.OpenItems=[..request.OpenItems,new("credit",kind,kind=="AR"?"source-customer":"source-vendor","Credit",request.CutoverDate,request.CutoverDate,-10)];
        request.Controls.Ar=kind=="AR"?90:100;request.Controls.Ap=kind=="AP"?30:40;
        request.TrialBalance=[new("source-ar",request.Controls.Ar,0),new("source-ap",0,request.Controls.Ap),new("source-cash",50,0),new("source-stock",20,0),new("source-equity",0,request.Controls.Ar+70-request.Controls.Ap)];await f.Import(request);
        var credit=f.State.OpenItems.Single(i=>i.Total<0);var target=f.State.OpenItems.Single(i=>i.Kind==kind&&i.Total>0);
        var application=new FinanceOpeningCreditApplication{ExpectedVersion=f.State.Version,CreditItemId=credit.Id,TargetItemId=target.Id,Amount=10,Date=request.CutoverDate,Reason="Source credit application"};await f.Service.ApplyOpeningCreditAsync(application);
        var original=Clone(f.State);var command=new FinanceOpeningCreditReversal{ExpectedVersion=f.State.Version,ApplicationId=application.Id,Date=request.CutoverDate,Reason="Correct wrong document application"};
        f.State.Periods[0].Status="CLOSED";await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ReverseOpeningCreditAsync(command));f.State.Periods[0].Status="OPEN";
        await f.Service.ReverseOpeningCreditAsync(command);await f.Service.ReverseOpeningCreditAsync(command);Assert.Equal(3,f.Saves);
        Assert.Equal(-10,FinanceLedger.Balance(f.State,credit,DateOnly.MaxValue));Assert.Equal(target.Total,FinanceLedger.Balance(f.State,target,DateOnly.MaxValue));
        Assert.Equal(original.Journals[1].Id,f.State.Journals.Last().ReversesId);FinanceIntegrity.ValidateTransition(original,f.State);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ReverseOpeningCreditAsync(new(){ExpectedVersion=f.State.Version,ApplicationId=application.Id,Date=request.CutoverDate,Reason="Duplicate reversal"}));
        Assert.Equal(3,f.State.Journals.Count);Assert.Equal(2,f.State.Settlements.Count);
    }
    [Fact] public async Task ImportedVendorOpenItemsCannotBeRepostedAsNewBills()
    {
        var f=new Fixture();await f.Import(f.Request());
        var duplicate=new FinanceBill{VendorId=f.Vendor,Number=" ap-1 ",Date=new(2026,10,1),DueDate=new(2026,10,31),Lines=[new("Historical bill entered again",1,40,"5000")]};
        Assert.Throws<ArgumentException>(()=>FinanceMigrationCommitments.ValidateBill(f.State,duplicate));
        duplicate.Number="NEW-2";FinanceMigrationCommitments.ValidateBill(f.State,duplicate);
        duplicate.Number="AP-1";duplicate.VendorId=Guid.NewGuid();FinanceMigrationCommitments.ValidateBill(f.State,duplicate);
        duplicate.VendorId=f.Vendor;duplicate.Id=f.State.OpenItems.Single(i=>i.Kind=="AP").Id;FinanceMigrationCommitments.ValidateBill(f.State,duplicate);
        Assert.Single(f.State.Journals);Assert.Single(f.State.OpenItems.Where(i=>i.Kind=="AP"));
    }
    private static FinanceState Clone(FinanceState s)=>JsonSerializer.Deserialize<FinanceState>(JsonSerializer.Serialize(s,JobConfigurationService.Json),JobConfigurationService.Json)!;
    [Fact] public async Task ConstructionOpeningReconcilesPerJobAndReplaysWithoutNewJournals()
    {
        var f=new Fixture();var r=f.Request();
        f.State.Construction.Accounts=new(){["retainage"]="1300",["contractAsset"]="1400",["contractLiability"]="2300",["billing"]="2400"};
        f.State.Accounts.AddRange([new("1300","Retainage","ASSET","DEBIT"),new("1400","Contract asset","ASSET","DEBIT"),new("2300","Contract liability","LIABILITY","CREDIT"),new("2400","Billing","LIABILITY","CREDIT")]);
        f.Jobs.Setup(j=>j.GetAsync(f.Job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=f.Job,CustomerId=f.Customer});
        r.ConstructionContracts=[new(f.Job,f.Customer,1000,0,[new("1","Scope",1000)])];
        r.ConstructionHistory=[new(f.Job,r.CutoverDate,300,30,400,"ERP-job-1",[new("1",300)])];
        r.ConstructionBudgets=[new(f.Job,"ERP-budget-1",1000,0,new(){["MATERIAL"]=600},true,"Reviewed")];
        r.ConstructionForecasts=[new(Guid.NewGuid(),f.Job,r.CutoverDate,80,true,"Reviewed remaining forecast")];
        r.Controls.ContractValue=1000;r.Controls.ConstructionBilled=300;r.Controls.Retainage=30;r.Controls.EarnedRevenue=400;r.Controls.ConstructionBudget=600;
        r.Accounts=[..r.Accounts,new("source-retainage","1300"),new("source-contract-asset","1400")];
        r.TrialBalance=r.TrialBalance.Select(b=>b.ExternalAccount=="source-equity"?b with{Credit=b.Credit+130}:b).Concat([new("source-retainage",30,0),new("source-contract-asset",100,0)]).ToArray();
        var preview=await f.Service.PreviewAsync(r);Assert.True(preview.CanImport,string.Join(" ",preview.Errors));
        f.Jobs.Setup(j=>j.GetAsync(f.Job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=f.Job,CustomerId=Guid.NewGuid()});
        Assert.Contains((await f.Service.PreviewAsync(r)).Errors,e=>e.Contains("customer does not match"));
        f.Jobs.Setup(j=>j.GetAsync(f.Job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=f.Job,CustomerId=f.Customer});await f.Import(r);
        Assert.Single(f.State.Journals);Assert.Equal(100,f.State.Journals.Single().Lines.Where(l=>l.JobId==f.Job&&l.Account=="1400").Sum(l=>l.Debit-l.Credit));
        Assert.Single(f.State.Construction.OpeningBalances);Assert.Single(f.State.Budgets);await f.Service.ImportAsync(r);Assert.Equal(1,f.Saves);
        var result=ConstructionFinanceEngine.Calculate(f.State,f.Supply,f.State.Construction.Contracts.Single(),r.CutoverDate);Assert.Equal(300,result.Billed);Assert.Equal(30,result.RetainageHeld);
        var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(f.Tenant);user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());
        var supply=new Mock<ISupplyStore>();supply.Setup(p=>p.ReadAsync(f.Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(f.Supply);
        var construction=new ConstructionFinanceService(f.Store.Object,f.Authority.Object,user.Object,supply.Object,f.Jobs.Object);
        await construction.CommandAsync(new(){ExpectedVersion=f.State.Version,Action="release-retainage",JobId=f.Job,Date=r.CutoverDate,Amount=30,Reason="Release imported retainage"});
        Assert.Equal(0,FinanceLedger.AccountBalance(f.State,"1300",r.CutoverDate));
        await construction.CommandAsync(new(){ExpectedVersion=f.State.Version,Action="calculate-wip",PeriodId="2026-09",Reason="Calculate first WIP after import"});
        var snapshot=f.State.Construction.WipSnapshots.Single();Assert.Equal(500,snapshot.Jobs.Single().EarnedRevenue);
        await construction.CommandAsync(new(){ExpectedVersion=f.State.Version,Action="approve-wip",Target=snapshot.Id,Reason="Review first WIP"});
        await construction.CommandAsync(new(){ExpectedVersion=f.State.Version,Action="post-wip",Target=snapshot.Id,Reason="Post first WIP"});
        var wipJournal=f.State.Journals.Single(j=>j.SourceType=="construction-wip");Assert.Equal(wipJournal.Lines.Sum(l=>l.Debit),wipJournal.Lines.Sum(l=>l.Credit));
        Assert.Equal(100,-FinanceLedger.AccountBalance(f.State,"4000",r.CutoverDate));Assert.Equal(200,FinanceLedger.AccountBalance(f.State,"1400",r.CutoverDate));
        FinanceIntegrity.ValidateTransition(new(),f.State);
    }
    [Fact] public async Task ConstructionMigrationRejectsCanonicalCustomerMismatch()
    {
        var f=new Fixture();var r=f.Request();r.ConstructionContracts=[new(f.Job,f.Customer,1000,0,[new("1","Scope",1000)])];
        // Invalid/missing source history also prevents import without touching persistent state.
        var report=await f.Service.PreviewAsync(r);Assert.False(report.CanImport);Assert.Equal(0,f.Saves);
    }
    private sealed class Fixture
    {
        public readonly Guid Tenant=Guid.NewGuid(), Customer=Guid.NewGuid(), Vendor=Guid.NewGuid(), Job=Guid.NewGuid(), Location=Guid.NewGuid();
        public FinanceState State=FinanceTests.State(); public SupplyState Supply=new(){Version="supply-v1"}; public int Saves;
        public Mock<IFinanceAuthority> Authority=new();public Mock<ICustomerRepository> Customers=new(); public Mock<IJobService> Jobs=new();public Mock<IInvoiceService> Invoices=new();public FinanceMigrationService Service; public Mock<IFinanceStore> Store = new();
        public Fixture(IFinanceSourceCoordinator? coordinator=null)
        {
            State.Version="finance-v1";Supply.Vendors.Add(new(){ContactId=Vendor});Supply.Catalog.Add(new(){Id="item"});Supply.Locations.Add(new(){Id=Location});Supply.Movements.Add(new(){ItemId="item",ToLocationId=Location,Quantity=2});
            var store=Store;store.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>Clone(State));
            store.Setup(s=>s.SaveAsync(Tenant,It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((Guid tenant,FinanceState next,string expected,CancellationToken ct)=>{Assert.Equal(State.Version,expected);FinanceIntegrity.ValidateTransition(State,next);next.Version="finance-v"+(++Saves+1);State=next;return Task.CompletedTask;});
            var supply=new Mock<ISupplyStore>();supply.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>Supply);
            var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(Tenant);user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());
            Authority.Setup(a=>a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Customers.Setup(c=>c.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string partition,string row,CancellationToken ct)=>new Customer{Id=Guid.Parse(row),PartitionKey=partition});
            Jobs.Setup(j=>j.GetAsync(Job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=Job});
            Service=new(store.Object,Authority.Object,user.Object,supply.Object,Customers.Object,Jobs.Object,Invoices.Object,coordinator);
        }
        public FinanceMigrationRequest Request()=>new(){ExpectedVersion=State.Version,ExpectedSupplyVersion=Supply.Version,SourceSystem="Acumatica",ExportId="cutover-2026-09",SourceFileSha256=new string('A',64),CutoverDate=new(2026,9,30),Reason="Reviewed trial balance and source subsidiary schedules",
            Accounts=[new("source-ar","1100"),new("source-ap","2000"),new("source-cash","1000"),new("source-stock","1200"),new("source-equity","3000")],
            Entities=[new("customer","source-customer",Customer),new("vendor","source-vendor",Vendor),new("job","source-job",Job)],
            TrialBalance=[new("source-ar",100,0),new("source-ap",0,40),new("source-cash",50,0),new("source-stock",20,0),new("source-equity",0,130)],
            OpenItems=[new("invoice-old","AR","source-customer","AR-1",new(2026,9,1),new(2026,9,30),100),new("bill-old","AP","source-vendor","AP-1",new(2026,9,1),new(2026,9,30),40)],
            JobCosts=[new("cost-old","source-job","MATERIAL","MATERIAL",80)],Commitments=[new("po-old","source-job","source-vendor","Remaining order","MATERIAL",30)],Inventory=[new("stock-old","item",Location,2,20)],
            Controls=new(){Ar=100,Ap=40,JobCosts=80,Commitments=30,Inventory=20,Cash=new(){["operating"]=50}}};
        public async Task Import(FinanceMigrationRequest request){request.ApprovedFingerprint=(await Service.PreviewAsync(request)).Fingerprint;await Service.ImportAsync(request);}
    }
}
