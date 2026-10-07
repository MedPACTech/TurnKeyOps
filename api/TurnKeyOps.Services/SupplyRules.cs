using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public static class SupplyRules
{
    public static string PolicyHash(SupplyPolicy p)=>System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(p,JobConfigurationService.Json)));
    public static readonly string[] Units=["each","box","foot","linear-foot","square-foot","cubic-yard","ton","gallon","hour","day","load","pallet","bundle"];
    public static void Positive(decimal quantity){if(quantity<=0||quantity>100000000)throw new ArgumentException("Quantity must be positive and within the supported limit.");}
    public static CatalogItem Item(SupplyState s,string id)=>s.Catalog.SingleOrDefault(i=>i.Id==id&&i.Active)??throw new ArgumentException("Choose an active catalog item.");
    public static MaterialDemand Demand(SupplyState s,Guid id)=>s.Demands.SingleOrDefault(d=>d.Id==id&&!d.Cancelled)??throw new ArgumentException("Choose an active Job demand.");
    public static InventoryLocation Location(SupplyState s,Guid? id)=>s.Locations.SingleOrDefault(l=>l.Id==id&&l.Active)??throw new ArgumentException("Choose an active inventory location.");
    public static decimal Convert(CatalogItem item,decimal quantity,string unit)
    {
        Positive(quantity);if(unit==item.Unit)return quantity;
        if(!item.UnitConversions.TryGetValue(unit,out var factor)||factor<=0)throw new ArgumentException("No explicit conversion to the catalog unit is configured.");
        var result=quantity*factor;Positive(result);return result;
    }
    public static SupplyBalance Balance(SupplyState s,string item,Guid location,DateTime now)
    {
        var stock=s.Movements.Where(m=>m.ItemId==item&&m.Type is not ("RESERVATION" or "RESERVATION_RELEASE" or "DAMAGE"));
        var onHand=stock.Where(m=>m.ToLocationId==location).Sum(m=>m.Quantity)-stock.Where(m=>m.FromLocationId==location).Sum(m=>m.Quantity);
        var reserved=s.Reservations.Where(r=>r.ItemId==item&&r.LocationId==location&&r.Status=="active"&&(r.ExpiresAtUtc is null||r.ExpiresAtUtc>now)).Sum(r=>r.Quantity);
        return new(item,location,onHand,reserved,onHand-reserved);
    }
    public static decimal Received(SupplyState s,Guid line)=>s.Receipts.Where(r=>r.LineId==line).Sum(r=>r.Accepted);
    public static SupplyReadiness Readiness(SupplyState s,MaterialDemand d,DateTime now)
    {
        var reserved=s.Reservations.Where(r=>r.DemandId==d.Id&&r.Status=="active"&&(r.ExpiresAtUtc is null||r.ExpiresAtUtc>now)).Sum(r=>r.Quantity);
        var issued=s.Movements.Where(m=>m.DemandId==d.Id&&m.Type=="ISSUE_TO_JOB").Sum(m=>m.Quantity);
        var returns=s.Movements.Where(m=>m.DemandId==d.Id&&m.Type=="RETURN_FROM_JOB").Sum(m=>m.Quantity);
        var directLines=s.Orders.SelectMany(o=>o.Lines).Where(l=>l.DemandId==d.Id&&l.DirectToJob).Select(l=>l.Id).ToHashSet();
        var direct=s.Receipts.Where(r=>directLines.Contains(r.LineId)).Sum(r=>r.Accepted);
        var consumed=s.Movements.Where(m=>m.DemandId==d.Id&&m.Type=="CONSUMPTION").Sum(m=>m.Quantity);
        var allocated=issued+direct-returns;
        var shortage=Math.Max(0,d.Quantity-reserved-allocated);
        var orders=s.Orders.Where(o=>o.Status is not ("CANCELLED" or "CLOSED")&&o.Lines.Any(l=>l.DemandId==d.Id)).ToList();
        var reasons=new List<string>();
        if(shortage>0){reasons.Add($"Missing {shortage} {d.Unit}");if(orders.Any(o=>o.Status is "SENT" or "APPROVED" or "PENDING_APPROVAL" or "DRAFT"))reasons.Add("Purchase order is not acknowledged");
            if(orders.Any(o=>o.ExpectedAtUtc<now||d.RequiredAtUtc.HasValue&&o.ExpectedAtUtc>d.RequiredAtUtc))reasons.Add("Delivery is late for the required date");
            if(orders.Any(o=>o.Status=="PARTIALLY_RECEIVED"))reasons.Add("Only part of the order has arrived");}
        var status=d.Cancelled?"CANCELLED":consumed>=d.Quantity?"CONSUMED":shortage==0?(reserved>0?"RESERVED":"DELIVERED_TO_JOB"):orders.Any()?"ORDERED":"PURCHASE_REQUIRED";
        return new(d.Id,status,d.Quantity,reserved,allocated,consumed,shortage,d.Gate,reasons.ToArray());
    }
    public static void Move(SupplyState s,SupplyCommand c,string actor,DateTime now)
    {
        var item=Item(s,c.ItemId);var quantity=Convert(item,c.Quantity,c.Unit);
        if(!item.Stocked||item.JobSpecific)throw new ArgumentException("Use a Job receipt for non-stock or special-order items.");
        var type=c.Action switch{"transfer"=>"TRANSFER","adjust"=>"ADJUSTMENT","scrap"=>"SCRAP",_=>throw new ArgumentException("Invalid stock movement.")};
        if(string.IsNullOrWhiteSpace(c.Reason))throw new ArgumentException("A movement reason is required.");
        var from=c.Action=="adjust"?null:c.LocationId;var to=c.Action=="scrap"?null:c.ToLocationId;
        if(c.Action=="adjust"){if(c.State=="remove"){from=c.LocationId;to=null;}else if(c.State=="add"){to=c.LocationId;}else throw new ArgumentException("Choose add or remove.");}
        if(from.HasValue){Location(s,from);if(Balance(s,item.Id,from.Value,now).Available<quantity)throw new ArgumentException("Insufficient unreserved stock.");}
        if(to.HasValue)Location(s,to);
        if(from==to||from is null&&to is null)throw new ArgumentException("Choose distinct source and destination.");
        s.Movements.Add(new(){ItemId=item.Id,Quantity=quantity,Unit=item.Unit,Type=type,FromLocationId=from,ToLocationId=to,Reason=c.Reason,Actor=actor,AtUtc=now,SourceId=c.Id});
    }
    public static void Reserve(SupplyState s,SupplyCommand c,string actor,DateTime now)
    {
        var d=Demand(s,c.TargetId);var item=Item(s,d.ItemId??"");var location=Location(s,c.LocationId);var quantity=Convert(item,c.Quantity,c.Unit);
        if(!item.Stocked||item.JobSpecific||d.Strategy!="stock")throw new ArgumentException("This demand requires direct sourcing.");
        if(Balance(s,item.Id,location.Id,now).Available<quantity)throw new ArgumentException("Stock is already reserved or unavailable.");
        var ordered=s.Orders.Where(o=>o.Status is not ("CANCELLED" or "CLOSED")).SelectMany(o=>o.Lines).Where(l=>l.DemandId==d.Id).Sum(l=>l.Quantity-Received(s,l.Id));
        if(Readiness(s,d,now).Shortage-ordered<quantity)throw new ArgumentException("Reservation exceeds unmet demand.");
        var r=new StockReservation{DemandId=d.Id,ItemId=item.Id,LocationId=location.Id,Quantity=quantity,RequiredAtUtc=d.RequiredAtUtc};s.Reservations.Add(r);
        s.Movements.Add(new(){ItemId=item.Id,Quantity=quantity,Unit=item.Unit,Type="RESERVATION",FromLocationId=location.Id,DemandId=d.Id,JobId=d.JobId,SourceId=r.Id,Actor=actor,AtUtc=now,Reason=c.Reason});
    }
    public static void Allocation(SupplyState s,SupplyCommand c,string actor,DateTime now)
    {
        if(c.Action is "release" or "issue"){
            var r=s.Reservations.SingleOrDefault(r=>r.Id==c.TargetId&&r.Status=="active")??throw new ArgumentException("Active reservation not found.");
            var d=Demand(s,r.DemandId);var item=Item(s,r.ItemId);
            if(c.Action=="issue"&&r.ExpiresAtUtc<=now)throw new ArgumentException("Reservation expired.");
            r.Status=c.Action=="release"?"released":"issued";
            s.Movements.Add(new(){ItemId=item.Id,Quantity=r.Quantity,Unit=item.Unit,Type=c.Action=="release"?"RESERVATION_RELEASE":"ISSUE_TO_JOB",FromLocationId=r.LocationId,DemandId=d.Id,JobId=d.JobId,SourceId=r.Id,Actor=actor,AtUtc=now,Reason=c.Reason});
        }else{
            var d=Demand(s,c.TargetId);Positive(c.Quantity);if(c.Unit!=d.Unit)throw new ArgumentException("Use the demand's unit.");
            var readiness=Readiness(s,d,now);if(c.Quantity>readiness.Received-readiness.Consumed)throw new ArgumentException("Quantity exceeds unconsumed Job allocation.");
            Guid? to=null;
            if(c.Action=="return"){var item=Item(s,d.ItemId??"");if(!item.Stocked||item.JobSpecific)throw new ArgumentException("Special-order/service returns require vendor resolution.");to=Location(s,c.LocationId).Id;}
            s.Movements.Add(new(){ItemId=d.ItemId??"",Quantity=c.Quantity,Unit=d.Unit,Type=c.Action=="return"?"RETURN_FROM_JOB":"CONSUMPTION",ToLocationId=to,DemandId=d.Id,JobId=d.JobId,Actor=actor,AtUtc=now,Reason=c.Reason,SourceId=c.Id});
        }
    }
    public static void DraftOrder(SupplyState s,PurchaseOrder order,string actor,DateTime now)
    {
        var vendor=s.Vendors.SingleOrDefault(v=>v.ContactId==order.VendorId&&v.Active)??throw new ArgumentException("Choose an active vendor contact.");
        if(order.Lines.Count is 0 or >100||order.Tax<0||order.Shipping<0||order.Tax>1000000000000||order.Shipping>1000000000000)throw new ArgumentException("Provide order lines and nonnegative charges.");
        order.Files=[];order.Dispatch=null;order.ApprovalPolicyHash="";order.Id=Guid.NewGuid();order.Number=$"PO-{s.Orders.Count+1:000000}";order.Status="DRAFT";order.Buyer=actor;order.CreatedAtUtc=now;order.ApprovedBy="";order.ApprovedAtUtc=null;order.SentAtUtc=null;order.AcknowledgedAtUtc=null;order.ApprovalReasons=[];
        if(s.Policy.NonPreferredApproval&&!vendor.Preferred)order.ApprovalReasons.Add("Non-preferred vendor");
        if(order.Lines.GroupBy(l=>l.DemandId).Any(g=>g.Count()>1))throw new ArgumentException("Use one line per demand.");
        foreach(var l in order.Lines){
            var d=Demand(s,l.DemandId);Positive(l.Quantity);if(l.UnitCost<0||l.UnitCost>100000000)throw new ArgumentException("Invalid unit cost.");
            l.Id=Guid.NewGuid();l.ItemId=d.ItemId;l.Description=d.Description;
            if(l.Unit!=d.Unit)throw new ArgumentException("Order unit must equal demand unit; explicitly convert supplier packs first.");
            var outstanding=s.Orders.Where(o=>o.Status is not ("CANCELLED" or "CLOSED")).SelectMany(o=>o.Lines).Where(x=>x.DemandId==d.Id).Sum(x=>x.Quantity-Received(s,x.Id));
            if(l.Quantity>Readiness(s,d,now).Shortage-outstanding)throw new ArgumentException("An existing allocation or order already covers this demand.");
            var item=d.ItemId is null?null:Item(s,d.ItemId);
            if(d.Strategy!="stock"||item is null||!item.Stocked||item.JobSpecific)l.DirectToJob=true;
            if(!l.DirectToJob)Location(s,order.LocationId);
            var offer=s.VendorItems.SingleOrDefault(v=>v.ItemId==d.ItemId&&v.VendorId==order.VendorId);
            if(s.Policy.PriceOverrideApproval&&(offer?.Cost is null||offer.Unit!=l.Unit||offer.Cost!=l.UnitCost))order.ApprovalReasons.Add("Manual or changed supplier price");
            if(l.RequestId.HasValue){var request=s.Requests.SingleOrDefault(r=>r.Id==l.RequestId&&r.DemandId==d.Id&&r.Status=="APPROVED")??throw new ArgumentException("Approve the purchase request first.");if(l.Quantity>request.Quantity)throw new ArgumentException("Order exceeds purchase request.");request.Status="ORDERED";if(s.Policy.RushApproval&&request.Rush)order.ApprovalReasons.Add("Rush order");}
        }
        if(order.Total>s.Policy.ApprovalAbove)order.ApprovalReasons.Add("Order exceeds approval threshold");
        if(s.Policy.SpendingLimits.TryGetValue(actor,out var limit)&&order.Total>limit)order.ApprovalReasons.Add("Buyer spending limit exceeded");
        s.Orders.Add(order);
    }
    public static void OrderTransition(SupplyState s,SupplyCommand c,bool owner,DateTime now)
    {
        var o=s.Orders.SingleOrDefault(o=>o.Id==c.TargetId)??throw new ArgumentException("Order not found.");
        var allowed=o.Status switch{"DRAFT"=>new[]{"PENDING_APPROVAL","APPROVED","CANCELLED"},"PENDING_APPROVAL"=>["APPROVED","CANCELLED"],"APPROVED"=>["PENDING_APPROVAL","SENT","CANCELLED"],"SENT"=>["ACKNOWLEDGED","CANCELLED"],"ACKNOWLEDGED"=>["CANCELLED"],"RECEIVED"=>["CLOSED"],_=>Array.Empty<string>()};
        if(!allowed.Contains(c.State))throw new ArgumentException("Invalid purchase order transition.");
        if(o.Dispatch?.Status is "sending" or "unconfirmed" && c.State!="SENT")throw new ArgumentException("Review the uncertain vendor send before changing this order.");
        if(c.State=="APPROVED"){
            o.ApprovalReasons=[];
            var vendor=s.Vendors.SingleOrDefault(v=>v.ContactId==o.VendorId&&v.Active)??throw new ArgumentException("Active vendor required.");
            if(s.Policy.NonPreferredApproval&&!vendor.Preferred)o.ApprovalReasons.Add("Non-preferred vendor");
            if(o.Total>s.Policy.ApprovalAbove)o.ApprovalReasons.Add("Order exceeds approval threshold");
            if(s.Policy.SpendingLimits.TryGetValue(o.Buyer,out var spending)&&o.Total>spending)o.ApprovalReasons.Add("Buyer spending limit exceeded");
            foreach(var line in o.Lines){var offer=s.VendorItems.SingleOrDefault(v=>v.ItemId==line.ItemId&&v.VendorId==o.VendorId);
                if(s.Policy.PriceOverrideApproval&&(offer?.Cost is null||offer.Unit!=line.Unit||offer.Cost!=line.UnitCost))o.ApprovalReasons.Add("Manual or changed supplier price");
                if(s.Policy.RushApproval&&s.Requests.Any(r=>r.Id==line.RequestId&&r.Rush))o.ApprovalReasons.Add("Rush order");}
        }
        if(c.State=="APPROVED"&&o.ApprovalReasons.Count>0&&!owner)throw new MedInsights.Lib.ForbiddenAccessException("An owner must approve these purchasing exceptions.");
        if(c.State=="CANCELLED"&&s.Receipts.Any(r=>r.OrderId==o.Id))throw new ArgumentException("Resolve received goods before cancelling.");
        if(c.State is "SENT" or "ACKNOWLEDGED"&&string.IsNullOrWhiteSpace(c.Reference))throw new ArgumentException("Record the vendor communication reference.");
        if(c.State=="SENT"){if(o.Dispatch?.Status is "sending" or "unconfirmed"){if(!owner)throw new MedInsights.Lib.ForbiddenAccessException("Owner verification is required for an uncertain vendor send.");o.Dispatch.Status="externally-verified";}if(o.ApprovalPolicyHash!=PolicyHash(s.Policy))throw new ArgumentException("Purchasing rules changed. Request approval again.");o.SentAtUtc=now;}
        if(c.State=="APPROVED")o.ApprovalPolicyHash=PolicyHash(s.Policy);
        if(c.State=="ACKNOWLEDGED"){if(c.ExpectedAtUtc is null)throw new ArgumentException("Record the confirmed delivery date.");o.ExpectedAtUtc=c.ExpectedAtUtc;o.AcknowledgedAtUtc=now;}
        if(c.State is "SENT" or "ACKNOWLEDGED")o.VendorReference=c.Reference;
        if(c.State=="CANCELLED")foreach(var l in o.Lines)if(l.RequestId.HasValue){var r=s.Requests.Single(r=>r.Id==l.RequestId);r.Status="APPROVED";}
        o.Status=c.State;
    }
    public static void Receive(SupplyState s,SupplyCommand c,string actor,DateTime now)
    {
        var order=s.Orders.SingleOrDefault(o=>o.Lines.Any(l=>l.Id==c.TargetId))??throw new ArgumentException("Order line not found.");
        if(order.Status is not ("SENT" or "ACKNOWLEDGED" or "PARTIALLY_RECEIVED"))throw new ArgumentException("Only sent orders can be received.");
        var line=order.Lines.Single(l=>l.Id==c.TargetId);var d=Demand(s,line.DemandId);
        if(c.Quantity<0||c.Damaged<0||c.Quantity+c.Damaged<=0||c.Quantity+c.Damaged>line.Quantity-Received(s,line.Id))throw new ArgumentException("Receipt exceeds remaining quantity or has no received items.");
        if(c.Unit!=line.Unit)throw new ArgumentException("Receipt unit must match the purchase order.");
        if(string.IsNullOrWhiteSpace(c.Reference))throw new ArgumentException("Record a packing slip or service confirmation reference.");
        Guid? location=null;
        if(!line.DirectToJob){location=Location(s,c.LocationId??order.LocationId).Id;if(location!=order.LocationId)throw new ArgumentException("Receive at the ordered location or explicitly transfer afterward.");}
        var receipt=new SupplyReceipt{Id=c.Id,OrderId=order.Id,LineId=line.Id,Accepted=c.Quantity,Damaged=c.Damaged,LocationId=location,Reference=c.Reference,Notes=c.Reason,Actor=actor,AtUtc=now};s.Receipts.Add(receipt);
        if(c.Quantity>0){s.Movements.Add(new(){ItemId=line.ItemId??"",Quantity=c.Quantity,Unit=line.Unit,Type="RECEIPT",ToLocationId=location,JobId=d.JobId,DemandId=d.Id,SourceId=receipt.Id,Actor=actor,AtUtc=now,Reason=c.Reference});
            if(!line.DirectToJob){s.Reservations.Add(new(){ItemId=line.ItemId!,DemandId=d.Id,LocationId=location!.Value,Quantity=c.Quantity,RequiredAtUtc=d.RequiredAtUtc});s.Movements.Add(new(){ItemId=line.ItemId!,Quantity=c.Quantity,Unit=line.Unit,Type="RESERVATION",FromLocationId=location,JobId=d.JobId,DemandId=d.Id,SourceId=receipt.Id,Actor=actor,AtUtc=now,Reason="Received for Job"});}}
        if(c.Damaged>0)s.Movements.Add(new(){ItemId=line.ItemId??"",Quantity=c.Damaged,Unit=line.Unit,Type="DAMAGE",JobId=d.JobId,DemandId=d.Id,SourceId=receipt.Id,Actor=actor,AtUtc=now,Reason=c.Reason});
        order.Status=order.Lines.All(l=>Received(s,l.Id)>=l.Quantity)?"RECEIVED":"PARTIALLY_RECEIVED";
    }
}
