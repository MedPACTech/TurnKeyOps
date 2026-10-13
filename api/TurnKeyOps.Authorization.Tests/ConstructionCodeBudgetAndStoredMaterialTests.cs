using System.Text.Json;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Authorization.Tests;
public sealed class ConstructionCodeBudgetAndStoredMaterialTests
{
    private sealed class Harness
    {
        public FinanceState State=FinanceTests.State();public readonly Guid Job=Guid.NewGuid();public readonly Guid Customer=Guid.NewGuid();public SupplyState Supply=new();public ConstructionFinanceService Service {get;}
        public Harness()
        {
            State.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});State.Periods.Add(new(){Id="2026-11",Start=new(2026,11,1),End=new(2026,11,30)});
            State.Accounts.AddRange([new("1110","Retainage","ASSET","DEBIT"),new("1120","Contract asset","ASSET","DEBIT"),new("2200","Contract liability","LIABILITY","CREDIT"),new("2210","Billing","LIABILITY","CREDIT")]);State.Construction.Accounts=new(){{"retainage","1110"},{"contractAsset","1120"},{"contractLiability","2200"},{"billing","2210"}};
            State.CostCodes.AddRange([new("MAT-A","Material A","MATERIAL"),new("MAT-B","Material B","MATERIAL"),new("LAB-A","Labor A","LABOR")]);State.Budgets.Add(new(Job,"accepted",2000,0,new(){["MATERIAL"]=1000,["LABOR"]=500},true,"Reviewed"));State.Construction.Contracts.Add(new(Job,Customer,2000,0,[new("one","Contract",2000)]));
            var tenant=Guid.NewGuid();var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(State));store.Setup(s=>s.SaveAsync(tenant,It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Callback<Guid,FinanceState,string,CancellationToken>((_,value,version,_)=>{Assert.Equal(State.Version,version);FinanceIntegrity.ValidateTransition(State,value);value.Version=Guid.NewGuid().ToString();State=JobConfigurationService.Clone(value);}).Returns(Task.CompletedTask);
            var authority=new Mock<IFinanceAuthority>();authority.Setup(a=>a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(tenant);user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());var jobs=new Mock<IJobService>();jobs.Setup(j=>j.GetAsync(Job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=Job,CustomerId=Customer});var supply=new Mock<ISupplyStore>();supply.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(Supply));Service=new(store.Object,authority.Object,user.Object,supply.Object,jobs.Object);
        }
        public async Task Run(ConstructionFinanceCommand c){c.ExpectedVersion=State.Version;await Service.CommandAsync(c);}
        public ConstructionCodeBudgetRevision Budget()=>new(Guid.NewGuid(),Job,new(2026,9,1),[new("MATERIAL","MAT-A",600),new("MATERIAL","MAT-B",400),new("LABOR","LAB-A",500)]);
        public async Task ReviewBudget(){var id=Guid.NewGuid();await Run(new(){Id=id,Action="draft-code-budget",JobId=Job,Date=new(2026,9,1),Reason="Reviewed allocation",CodeBudget=Budget()});await Run(new(){Action="review-code-budget",Target=id});}
        public async Task Bill(int month,decimal work,decimal stored,decimal installed)
        {var id=Guid.NewGuid();await Run(new(){Action="application",JobId=Job,Application=new(id,Job,month.ToString(),new(2026,month,1),new DateOnly(2026,month,1).AddMonths(1).AddDays(-1),[new("one",work,stored,installed)],Documents:stored>0?["receipts/materials.pdf"]:null)});await Run(new(){Action="submit-application",Target=id});await Run(new(){Action="approve-application",Target=id});await Run(new(){Action="invoice-application",Target=id});}
    }
    [Fact]public async Task StoredMaterialInstallationTransfersValueWithoutDoubleBillingOrRetainage()
    {
        var h=new Harness();await h.Bill(9,100,500,0);await h.Bill(10,100,0,200);await h.Bill(11,0,0,300);
        Assert.Equal(700,h.State.Construction.Applications.Sum(a=>a.CurrentBilling));Assert.Equal(70,h.State.Construction.Applications.Sum(a=>a.Retainage));Assert.Equal(630,h.State.OpenItems.Sum(i=>i.Total));Assert.Equal(70,FinanceLedger.AccountBalance(h.State,"1110",DateOnly.MaxValue));Assert.Equal(2,h.State.Journals.Count(j=>j.SourceType=="construction-billing"));
        var transfer=h.State.Construction.Applications.Single(a=>a.Number=="11");Assert.True(transfer.IsPosted);Assert.Null(transfer.JournalId);Assert.Equal(0,transfer.CurrentBilling);Assert.Equal(700,h.State.Construction.Applications.SelectMany(a=>a.Lines).Sum(l=>l.CompletedWorkThisPeriod));Assert.Equal(0,h.State.Construction.Applications.SelectMany(a=>a.Lines).Sum(l=>l.NewStoredMaterials-l.InstalledFromStored));
        var workspace=JsonSerializer.SerializeToElement(await h.Service.WorkspaceAsync(new(2026,11,30)),JobConfigurationService.Json);var sov=workspace.GetProperty("scheduleOfValues")[0];Assert.Equal(700,sov.GetProperty("completedWorkToDate").GetDecimal());Assert.Equal(0,sov.GetProperty("storedMaterialsToDate").GetDecimal());Assert.Equal(700,sov.GetProperty("totalBilled").GetDecimal());
        var clone=JobConfigurationService.Clone(h.State.Construction);clone.Applications[2]=transfer with{Lines=[new("one",1,0,300)]};Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateTransition(h.State.Construction,clone));
    }
    [Fact]public async Task StoredMaterialGuardrailsRequireEvidenceAvailableStoredAndContractCapacity()
    {
        var h=new Harness();ConstructionPayApplication App(decimal work,decimal stored,decimal install,string[]? docs=null)=>new(Guid.NewGuid(),h.Job,"test",new(2026,9,1),new(2026,9,30),[new("one",work,stored,install)],Documents:docs);
        Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateApplication(h.State,App(0,100,0)));Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateApplication(h.State,App(0,100,100,["receipt"])));Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateApplication(h.State,App(1500,501,0,["receipt"])));
        await h.Bill(9,100,500,0);var excessive=App(0,0,501) with{PeriodStart=new(2026,10,1),PeriodEnd=new(2026,10,31)};Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateApplication(h.State,excessive));
    }
    [Fact]public async Task ReviewedCodeBudgetsReconcileAndRemainImmutableWithStaleAllocationsExplicit()
    {
        var h=new Harness();Assert.Throws<ArgumentException>(()=>ConstructionCodeBudgets.Validate(h.State,h.Budget() with{Lines=[new("MATERIAL","MAT-A",1001),new("LABOR","LAB-A",500)]}));Assert.Throws<ArgumentException>(()=>ConstructionCodeBudgets.Validate(h.State,h.Budget() with{Lines=[new("MATERIAL","LAB-A",1000),new("LABOR","LAB-A",500)]}));
        await h.ReviewBudget();var review=Assert.Single(h.State.Construction.CodeBudgets);Assert.Equal("REVIEWED",review.Status);Assert.Equal(1500,review.Lines.Sum(l=>l.Amount));var clone=JobConfigurationService.Clone(h.State.Construction);clone.CodeBudgets[0]=review with{Lines=[new("MATERIAL","MAT-A",1500)]};Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateTransition(h.State.Construction,clone));
        h.State.Costs.Add(new(Guid.NewGuid(),h.Job,"MATERIAL","MAT-A",100,new(2026,9,5),"bill","1"));var report=ConstructionCodeBudgets.Report(h.State,h.Supply,h.Job,new(2026,9,30));var line=report.Single(r=>r.CostCode=="MAT-A");Assert.Equal(600,line.Budget);Assert.Equal(500,line.ActualVariance);h.State.Budgets[0]=h.State.Budgets[0] with{Categories=new(){["MATERIAL"]=1100,["LABOR"]=500}};line=ConstructionCodeBudgets.Report(h.State,h.Supply,h.Job,new(2026,9,30)).Single(r=>r.CostCode=="MAT-A");Assert.False(line.AllocationCurrent);Assert.Null(line.Budget);Assert.Contains("changed",line.AllocationNote);
    }
    [Fact]public async Task CodeReportsReconcileActualCommitmentForecastAndAvoidInventedAllocations()
    {
        var h=new Harness();await h.ReviewBudget();var demand=Guid.NewGuid();var order=Guid.NewGuid();var orderLine=Guid.NewGuid();h.Supply.Catalog.Add(new(){Id="material",Kind="MATERIAL"});h.Supply.Demands.Add(new(){Id=demand,JobId=h.Job});h.Supply.Orders.Add(new(){Id=order,Number="PO-1",Status="APPROVED",Lines=[new(){Id=orderLine,DemandId=demand,ItemId="material",Quantity=10,UnitCost=20}]});
        await h.Run(new(){Action="code-commitment",JobId=h.Job,Date=new(2026,9,1),Reason="Purchase coding",CommitmentCoding=new(Guid.NewGuid(),h.Job,order,orderLine,"MAT-B")});h.State.Costs.Add(new(Guid.NewGuid(),h.Job,"MATERIAL","MAT-A",100,new(2026,9,5),"bill","actual"));
        await h.Run(new(){Action="forecast",JobId=h.Job,Date=new(2026,9,1),Reason="Reviewed final forecast",Forecast=new(Guid.NewGuid(),h.Job,new(2026,9,1),300,true,"",Costs:[new("MATERIAL","MAT-A",100),new("MATERIAL","MAT-B",100),new("LABOR","LAB-A",100)])});
        var report=ConstructionCodeBudgets.Report(h.State,h.Supply,h.Job,new(2026,9,30));Assert.Equal(100,report.Sum(r=>r.Actual));Assert.Equal(200,report.Sum(r=>r.RemainingCommitment));Assert.Equal(300,report.Sum(r=>r.ForecastRemaining));Assert.Equal(500,report.Sum(r=>r.EstimatedFinalCost));Assert.Equal(500,ConstructionFinanceEngine.Calculate(h.State,h.Supply,h.State.Construction.Contracts[0],new(2026,9,30)).EstimatedFinalCost);
        await h.Run(new(){Action="forecast",JobId=h.Job,Date=new(2026,9,2),Reason="Category allocation unknown",Forecast=new(Guid.NewGuid(),h.Job,new(2026,9,2),300,true,"",Costs:[new("MATERIAL",null,300)])});report=ConstructionCodeBudgets.Report(h.State,h.Supply,h.Job,new(2026,9,30));Assert.Null(report.Single(r=>r.CostCode=="MAT-A").ForecastRemaining);Assert.Null(report.Single(r=>r.CostCode=="MAT-B").EstimatedFinalCost);Assert.Equal(300,report.Single(r=>r.Category=="MATERIAL"&&r.CostCode==null).ForecastRemaining);Assert.Equal(400,ConstructionFinanceEngine.Calculate(h.State,h.Supply,h.State.Construction.Contracts[0],new(2026,9,30)).EstimatedFinalCost);
    }
}
