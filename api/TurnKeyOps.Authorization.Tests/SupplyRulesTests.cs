using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace MedInsights.Authorization.Tests;
public sealed class SupplyRulesTests
{
    private static readonly DateTime Now=new(2026,10,6,12,0,0,DateTimeKind.Utc);
    private static (SupplyState s,MaterialDemand d,InventoryLocation warehouse,InventoryLocation truck) Fixture()
    {
        var item=new CatalogItem{Id="lockset",Name="Lockset",Unit="each",Trades=["doors-locks"]};var warehouse=new InventoryLocation{Name="Warehouse"};var truck=new InventoryLocation{Name="Truck",Type="truck"};
        var d=new MaterialDemand{Strategy="stock",ItemId=item.Id,Quantity=8,Unit="each",JobId=Guid.NewGuid(),RequirementId=Guid.NewGuid(),Description="Hardware"};
        var s=new SupplyState{Catalog=[item],Locations=[warehouse,truck],Demands=[d]};
        SupplyRules.Move(s,new(){Action="adjust",State="add",ItemId=item.Id,LocationId=warehouse.Id,Quantity=12,Unit="each",Reason="Verified count"},"user",Now);
        return(s,d,warehouse,truck);
    }
    [Fact]public void ReservationExplainsAvailableStockAndCannotOverallocate()
    {
        var(s,d,w,_)=Fixture();SupplyRules.Reserve(s,new(){TargetId=d.Id,LocationId=w.Id,Quantity=8},"buyer",Now);
        Assert.Equal(new SupplyBalance("lockset",w.Id,12,8,4),SupplyRules.Balance(s,"lockset",w.Id,Now));
        Assert.Throws<ArgumentException>(()=>SupplyRules.Reserve(s,new(){TargetId=d.Id,LocationId=w.Id,Quantity=1},"buyer",Now));
        SupplyRules.Allocation(s,new(){Action="release",TargetId=s.Reservations.Single().Id},"buyer",Now);Assert.Equal(12,SupplyRules.Balance(s,"lockset",w.Id,Now).Available);
    }
    [Fact]public void TransferIssueConsumptionAndReturnPreservePhysicalQuantities()
    {
        var(s,d,w,t)=Fixture();SupplyRules.Move(s,new(){Action="transfer",ItemId="lockset",LocationId=w.Id,ToLocationId=t.Id,Quantity=8,Reason="Load truck"},"user",Now);
        Assert.Equal(12,SupplyRules.Balance(s,"lockset",w.Id,Now).OnHand+SupplyRules.Balance(s,"lockset",t.Id,Now).OnHand);
        SupplyRules.Reserve(s,new(){TargetId=d.Id,LocationId=t.Id,Quantity=8},"user",Now);SupplyRules.Allocation(s,new(){Action="issue",TargetId=s.Reservations.Single().Id},"user",Now);
        Assert.Equal(0,SupplyRules.Balance(s,"lockset",t.Id,Now).OnHand);Assert.Equal(0,SupplyRules.Readiness(s,d,Now).Shortage);
        SupplyRules.Allocation(s,new(){Action="consume",TargetId=d.Id,Quantity=6},"user",Now);SupplyRules.Allocation(s,new(){Action="return",TargetId=d.Id,LocationId=t.Id,Quantity=2},"user",Now);
        Assert.Equal(2,SupplyRules.Balance(s,"lockset",t.Id,Now).Available);Assert.Equal(2,SupplyRules.Readiness(s,d,Now).Shortage);
        Assert.Throws<ArgumentException>(()=>SupplyRules.Allocation(s,new(){Action="return",TargetId=d.Id,LocationId=t.Id,Quantity=1},"user",Now));
        Assert.All(s.Movements,m=>Assert.False(string.IsNullOrWhiteSpace(m.Actor)));
    }
    [Fact]public void StockCorrectionsCannotRemoveReservedStock()
    {
        var(s,d,w,_)=Fixture();SupplyRules.Reserve(s,new(){TargetId=d.Id,LocationId=w.Id,Quantity=8},"user",Now);
        Assert.Throws<ArgumentException>(()=>SupplyRules.Move(s,new(){Action="adjust",State="remove",ItemId="lockset",LocationId=w.Id,Quantity=5,Reason="Count"},"user",Now));
    }
    [Fact]public void ConversionsAreExplicitAndItemSpecific()
    {
        var item=new CatalogItem{Unit="each",UnitConversions=new(){["box"]=12}};Assert.Equal(24,SupplyRules.Convert(item,2,"box"));
        Assert.Throws<ArgumentException>(()=>SupplyRules.Convert(item,2,"pallet"));Assert.Throws<ArgumentException>(()=>SupplyRules.Convert(item,-1,"each"));
    }
    [Theory][InlineData(false)][InlineData(true)]public void PartialReceivingAndDamagedReplacementSatisfyOnlyAcceptedQuantity(bool direct)
    {
        var(s,d,w,_)=Fixture();var vendor=new VendorSupplyProfile{ContactId=Guid.NewGuid(),Preferred=true};s.Vendors.Add(vendor);
        if(direct){s.Catalog[0].Stocked=false;d.Strategy="direct";d.Trade="concrete";}
        var order=new PurchaseOrder{VendorId=vendor.ContactId,LocationId=direct?null:w.Id,Lines=[new(){DemandId=d.Id,Quantity=8,UnitCost=10}]};
        SupplyRules.DraftOrder(s,order,"buyer",Now);Assert.Contains("Manual or changed supplier price",order.ApprovalReasons);
        Assert.Throws<MedInsights.Lib.ForbiddenAccessException>(()=>SupplyRules.OrderTransition(s,new(){TargetId=order.Id,State="APPROVED"},false,Now));
        SupplyRules.OrderTransition(s,new(){TargetId=order.Id,State="APPROVED"},true,Now);
        SupplyRules.OrderTransition(s,new(){TargetId=order.Id,State="SENT",Reference="Vendor email 1"},true,Now);
        SupplyRules.Receive(s,new(){TargetId=order.Lines[0].Id,Quantity=3,Damaged=1,Reference="Slip 1"},"warehouse",Now);
        Assert.Equal("PARTIALLY_RECEIVED",order.Status);Assert.Equal(5,SupplyRules.Readiness(s,d,Now).Shortage);
        SupplyRules.Receive(s,new(){TargetId=order.Lines[0].Id,Quantity=5,Reference="Replacement and remainder"},"warehouse",Now);
        Assert.Equal("RECEIVED",order.Status);Assert.Equal(0,SupplyRules.Readiness(s,d,Now).Shortage);
        Assert.Equal(direct?12:20,SupplyRules.Balance(s,"lockset",w.Id,Now).OnHand);Assert.Equal(direct?0:8,SupplyRules.Balance(s,"lockset",w.Id,Now).Reserved);
        Assert.Throws<ArgumentException>(()=>SupplyRules.Receive(s,new(){TargetId=order.Lines[0].Id,Quantity=1,Reference="Duplicate"},"user",Now));
    }
    [Fact]public void OrderCannotJumpApprovalOrDuplicateExistingSupply()
    {
        var(s,d,w,_)=Fixture();var vendor=new VendorSupplyProfile{ContactId=Guid.NewGuid()};s.Vendors.Add(vendor);var order=new PurchaseOrder{VendorId=vendor.ContactId,LocationId=w.Id,Lines=[new(){DemandId=d.Id,Quantity=8,UnitCost=10}]};SupplyRules.DraftOrder(s,order,"buyer",Now);
        Assert.Throws<ArgumentException>(()=>SupplyRules.OrderTransition(s,new(){TargetId=order.Id,State="SENT",Reference="x"},true,Now));
        Assert.Throws<ArgumentException>(()=>SupplyRules.DraftOrder(s,new(){VendorId=vendor.ContactId,LocationId=w.Id,Lines=[new(){DemandId=d.Id,Quantity=1}]},"buyer",Now));
    }
    [Fact]public void SameCoreSupportsDirectServiceAndStockedHardware()
    {
        var(s,d,w,_)=Fixture();var service=new CatalogItem{Id="disposal",Name="Disposal",Kind="SERVICE",Stocked=false,Unit="load",Trades=["land-clearing"]};s.Catalog.Add(service);
        var serviceDemand=new MaterialDemand{ItemId=service.Id,Quantity=2,Unit="load",Trade="land-clearing",Strategy="service",JobId=d.JobId};s.Demands.Add(serviceDemand);
        Assert.Throws<ArgumentException>(()=>SupplyRules.Reserve(s,new(){TargetId=serviceDemand.Id,LocationId=w.Id,Quantity=1,Unit="load"},"user",Now));Assert.Equal(2,s.Catalog.Count);Assert.Equal(2,s.Demands.Count);
    }
}
