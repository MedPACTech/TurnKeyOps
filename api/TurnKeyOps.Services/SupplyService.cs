using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class SupplyService(ISupplyStore store,ISupplyAuthority authority,IUserContext user,
    IJobRepository jobs,IJobWorkflowPayloadStore payloads,IManagedProfileStore contacts,ICalendarEventRepository calendar)
{
    private string Partition=>RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
    private string Actor=>user.UserId.ToString("D");
    private static readonly string[] PurchasingActions=["vendor","offer","request","approve-request","draft-order","order-status","policy"];
    public async Task<object> WorkspaceAsync(string module,CancellationToken ct=default)
    {
        await authority.RequireAsync(module,false,ct);var s=await store.ReadAsync(user.TenantId,ct);var now=DateTime.UtcNow;
        var costs=await authority.CanAsync("purchasing",false,ct);var owner=await authority.OwnerAsync(ct);
        // Return deliberate projections: vendor prices, ledger reasons and blob paths never leak through inventory.read.
        var inventoryRead=await authority.CanAsync("inventory",false,ct);
        var catalog=s.Catalog.Select(i=>new{i.Id,i.Name,i.Description,i.Kind,i.Category,i.Sku,i.Unit,i.Active,i.Stocked,i.JobSpecific,i.Trades,i.Metadata,i.UnitConversions,defaultCost=costs?i.DefaultCost:null,defaultSellPrice=costs?i.DefaultSellPrice:null});
        var demand=s.Demands.Select(d=>new{d.Id,d.JobId,d.JobName,d.RequirementId,d.Description,d.ItemId,d.Quantity,d.Unit,d.Trade,d.Strategy,d.RequiredAtUtc,d.Gate,d.OverrideReason,readiness=SupplyRules.Readiness(s,d,now)}).ToList();
        var balances=s.Catalog.Where(i=>i.Stocked).SelectMany(i=>s.Locations.Select(l=>SupplyRules.Balance(s,i.Id,l.Id,now))).ToList();
        var jobChoices=new List<object>();
        if(await authority.CanAsync("jobs",false,ct))foreach(var j in await jobs.ListAsync(Partition,ct)){
            if(j.IsDeleted||j.PartitionKey!=Partition)continue;var p=await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct);
            jobChoices.Add(new{j.Id,j.Name,requirements=p.Execution?.Requirements.Where(r=>r.Kind=="material"&&r.Status!="Cancelled").Select(r=>new{r.Id,r.Description,r.Quantity,r.Unit,r.CatalogItemId,r.RequiredAtUtc})});}
        var vendorChoices=new List<object>();
        if(costs)await foreach(var p in contacts.ListAsync(Partition,ct))if(p.PartitionKey==Partition&&!p.IsDeleted&&p.IsActive&&p.ProfileTypes.Contains("vendor"))vendorChoices.Add(new{p.Id,name=p.CompanyName??$"{p.FirstName} {p.LastName}"});
        var orders=s.Orders.Select(o=>new{o.Id,o.Number,o.Status,o.VendorId,o.LocationId,o.ExpectedAtUtc,o.VendorReference,o.DeliveryInstructions,o.ApprovalReasons,dispatchStatus=o.Dispatch?.Status,files=costs?o.Files.Select(f=>new{f.Id,f.Name,f.ContentType}):null,total=costs?(decimal?)o.Total:null,lines=o.Lines.Select(l=>new{l.Id,l.DemandId,l.ItemId,l.Description,l.Quantity,l.Unit,l.DirectToJob,unitCost=costs?(decimal?)l.UnitCost:null,received=SupplyRules.Received(s,l.Id)})}).ToList();
        var suggestions=s.Demands.Where(d=>!d.Cancelled).Select(d=>new{demandId=d.Id,options=s.VendorItems.Where(v=>v.ItemId==d.ItemId&&s.Vendors.Any(x=>x.ContactId==v.VendorId&&x.Active)).OrderByDescending(v=>v.Preferred||s.Vendors.Any(x=>x.ContactId==v.VendorId&&x.Preferred)).ThenBy(v=>v.LeadDays).Select(v=>new{v.VendorId,v.LeadDays,v.Preferred,cost=costs?v.Cost:null,canMeetDate=d.RequiredAtUtc is null? (bool?)null:now.AddDays(v.LeadDays)<=d.RequiredAtUtc,explanation="Preferred suppliers first, then recorded lead time. Availability must be confirmed by the vendor."})});
        return new{version=s.Version,canWrite=await authority.CanAsync(module,true,ct),canInventoryWrite=await authority.CanAsync("inventory",true,ct),canPurchase=costs,canPurchaseWrite=await authority.CanAsync("purchasing",true,ct),canConfigure=owner,catalog,locations=s.Locations,demands=demand,balances=inventoryRead?balances:[],reservations=inventoryRead?s.Reservations:[],
            movements=inventoryRead?s.Movements.Select(m=>new{m.Id,m.ItemId,m.Type,m.Quantity,m.Unit,m.FromLocationId,m.ToLocationId,m.JobId,m.Reason,m.AtUtc,m.Actor}):null,
            inTransit=s.Orders.Where(o=>o.Status is "SENT" or "ACKNOWLEDGED" or "PARTIALLY_RECEIVED").SelectMany(o=>o.Lines.Select(l=>new{orderId=o.Id,l.ItemId,l.DemandId,quantity=l.Quantity-SupplyRules.Received(s,l.Id),l.Unit,o.ExpectedAtUtc})),
            vendors=costs?s.Vendors.Select(v=>new{v.ContactId,v.Name,v.Preferred,v.OrderingEmail,v.OrderingPhone,v.Terms,v.LeadDays,v.Delivers,v.Active}):null,vendorChoices,jobChoices,orders,requests=costs?s.Requests:null,
            receipts=s.Receipts.Select(r=>new{r.Id,r.OrderId,r.LineId,r.Accepted,r.Damaged,r.AtUtc,r.LocationId,r.Reference,files=r.Files.Select(f=>new{f.Id,f.Name,f.ContentType})}),suggestions,
            policy=owner?s.Policy:null,tradeProfiles=s.Policy.Trades,units=SupplyRules.Units,metrics=new{shortages=demand.Count(d=>d.readiness.Shortage>0),lateOrders=s.Orders.Count(o=>o.ExpectedAtUtc<now&&o.Status is not ("RECEIVED" or "CLOSED" or "CANCELLED")),receivedByUnit=s.Receipts.GroupBy(r=>s.Orders.Single(o=>o.Id==r.OrderId).Lines.Single(l=>l.Id==r.LineId).Unit).Select(g=>new{unit=g.Key,accepted=g.Sum(r=>r.Accepted),damaged=g.Sum(r=>r.Damaged)}),orderValue=costs?(decimal?)s.Orders.Where(o=>o.Status!="CANCELLED").Sum(o=>o.Total):null}};
    }
    public async Task<object> CommandAsync(string module,SupplyCommand c,CancellationToken ct=default)
    {
        var required=PurchasingActions.Contains(c.Action)?"purchasing":"inventory";
        await authority.RequireAsync(required,true,ct);await authority.RequireAsync(module,false,ct);
        if(c.Id==Guid.Empty||c.Reason.Length>4000||c.Reference.Length>500)throw new ArgumentException("Invalid command identity or text length.");
        var s=await store.ReadAsync(user.TenantId,ct);
        var commandCopy=JobConfigurationService.Clone(c);commandCopy.ExpectedVersion="";
        var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(commandCopy,JobConfigurationService.Json)));
        var replay=s.Audit.SingleOrDefault(a=>a.Id==c.Id);
        if(replay is not null){if(replay.Fingerprint!=fingerprint)throw new ArgumentException("This command identity was already used for a different change.");return await WorkspaceAsync(module,ct);}
        if(c.ExpectedVersion!=s.Version)throw new InvalidOperationException("Supply changed. Refresh before saving.");
        var now=DateTime.UtcNow;var owner=await authority.OwnerAsync(ct);
        switch(c.Action){
            case "catalog":
                var item=c.Item??throw new ArgumentException("Catalog item required.");ValidateItem(s,item);
                if(!await authority.CanAsync("purchasing",true,ct)&&(item.DefaultCost.HasValue||item.DefaultSellPrice.HasValue))throw new MedInsights.Lib.ForbiddenAccessException("Purchasing write is required for prices.");
                var existing=s.Catalog.SingleOrDefault(i=>i.Id==item.Id);
                if(existing is not null){if(s.Movements.Any(m=>m.ItemId==item.Id)||s.Demands.Any(d=>d.ItemId==item.Id))throw new ArgumentException("Used catalog items retain their unit and history; create a new item revision.");s.Catalog.Remove(existing);}
                if(item.SourceSystem!="manual"&&string.IsNullOrWhiteSpace(item.ExternalId))throw new ArgumentException("Imported items require their source identity.");
                if(s.Catalog.Any(i=>i.SourceSystem==item.SourceSystem&&i.ExternalId==item.ExternalId&&!string.IsNullOrEmpty(item.ExternalId)))throw new ArgumentException("This source item is already imported.");
                s.Catalog.Add(item);break;
            case "location":
                var location=c.Location??throw new ArgumentException("Location required.");
                if(string.IsNullOrWhiteSpace(location.Name)||location.Type is not ("warehouse" or "shop" or "yard" or "truck" or "trailer" or "staging" or "job-site"))throw new ArgumentException("Choose a location name and type.");
                if(location.JobId.HasValue)await Job(location.JobId.Value,ct);
                if(location.ParentId.HasValue)SupplyRules.Location(s,location.ParentId);
                if(s.Locations.Any(l=>l.Id==location.Id))throw new ArgumentException("Location identity already exists.");s.Locations.Add(location);break;
            case "vendor":
                var vendor=c.Vendor??throw new ArgumentException("Vendor profile required.");var contact=await contacts.GetByKeysAsync(Partition,RepositoryKeyHelper.ToRowKey(vendor.ContactId),ct);
                if(contact is null||contact.IsDeleted||!contact.IsActive||contact.PartitionKey!=Partition||!contact.ProfileTypes.Contains("vendor"))throw new ArgumentException("Choose this tenant's active vendor contact.");
                if(vendor.LeadDays<0)throw new ArgumentException("Lead time cannot be negative.");vendor.Name=contact.CompanyName??$"{contact.FirstName} {contact.LastName}";vendor.OrderingEmail=string.IsNullOrWhiteSpace(vendor.OrderingEmail)?contact.ContactEmail??"":vendor.OrderingEmail;
                s.Vendors.RemoveAll(v=>v.ContactId==vendor.ContactId);s.Vendors.Add(vendor);break;
            case "offer":
                var offer=c.Offer??throw new ArgumentException("Vendor offer required.");var ci=SupplyRules.Item(s,offer.ItemId);
                if(!s.Vendors.Any(v=>v.ContactId==offer.VendorId&&v.Active)||offer.Cost<0||offer.LeadDays<0||offer.PackSize<=0)throw new ArgumentException("Invalid supplier offer.");
                if(offer.Unit!=ci.Unit&&!ci.UnitConversions.ContainsKey(offer.Unit))throw new ArgumentException("Configure an exact item conversion first.");
                offer.UpdatedAtUtc=now;s.VendorItems.RemoveAll(v=>v.ItemId==offer.ItemId&&v.VendorId==offer.VendorId);s.VendorItems.Add(offer);break;
            case "demand":
                var d=c.Demand??throw new ArgumentException("Demand mapping required.");var (job,p)=await Job(d.JobId,ct);
                await authority.RequireAsync("jobs",false,ct);
                if(job.Status is TurnKeyOps.Lib.Enums.JobStatus.Completed or TurnKeyOps.Lib.Enums.JobStatus.Closed or TurnKeyOps.Lib.Enums.JobStatus.Cancelled)throw new ArgumentException("Reopen the Job before creating new demand.");
                var requirement=p.Execution?.Requirements.SingleOrDefault(r=>r.Id==d.RequirementId&&r.Kind=="material"&&r.Status!="Cancelled")??throw new ArgumentException("Choose an existing material requirement. Labor and bundled scope are not stock demand.");
                if(s.Demands.Any(x=>x.JobId==d.JobId&&x.RequirementId==d.RequirementId))throw new ArgumentException("This requirement is already mapped.");
                d.Id=Guid.NewGuid();d.JobName=job.Name;d.Description=requirement.Description;d.Quantity=requirement.Quantity;d.Unit=requirement.Unit;d.Source=requirement.Source??"Job requirement";d.Trade=p.Execution!.TradeProfile;d.RequiredAtUtc=requirement.RequiredAtUtc??job.ScheduledStart;d.Cancelled=false;d.OverrideReason="";
                var profile=s.Policy.Trades.GetValueOrDefault(d.Trade)??new();d.Gate=profile.Gate;if(string.IsNullOrWhiteSpace(d.Strategy))d.Strategy=profile.DefaultStrategy;
                if(d.ItemId is not null){var mapped=SupplyRules.Item(s,d.ItemId);d.Quantity=SupplyRules.Convert(mapped,d.Quantity,d.Unit);d.Unit=mapped.Unit;if(!mapped.Stocked||mapped.JobSpecific)d.Strategy="direct";}
                else d.Strategy="direct";
                if(d.Strategy is not ("stock" or "direct" or "rental" or "service" or "manual"))throw new ArgumentException("Choose a sourcing strategy.");
                if(d.RequiredEventId.HasValue&&!((await calendar.ListAsync(Partition,ct)).Any(e=>e.Id==d.RequiredEventId&&e.JobId==d.JobId&&!e.IsDeleted)))throw new ArgumentException("Choose this Job's shared Calendar event.");
                s.Demands.Add(d);break;
            case "link-delivery":
                var linked=SupplyRules.Demand(s,c.TargetId);await authority.RequireAsync("calendar",false,ct);
                if(!Guid.TryParse(c.Reference,out var eventId)||(await calendar.ListAsync(Partition,ct)).All(e=>e.Id!=eventId||e.JobId!=linked.JobId||e.IsDeleted||e.JobEventType!="delivery"||e.EventStatus=="cancelled"))throw new ArgumentException("Choose this Job's active delivery event.");linked.RequiredEventId=eventId;break;
            case "reserve":SupplyRules.Reserve(s,c,Actor,now);break;
            case "release":case "issue":case "consume":case "return":SupplyRules.Allocation(s,c,Actor,now);break;
            case "transfer":case "adjust":case "scrap":SupplyRules.Move(s,c,Actor,now);break;
            case "clear-override":
                if(!owner)throw new MedInsights.Lib.ForbiddenAccessException("Owner permission is required.");SupplyRules.Demand(s,c.TargetId).OverrideReason="";break;
            case "override":
                if(!owner||string.IsNullOrWhiteSpace(c.Reason))throw new MedInsights.Lib.ForbiddenAccessException("Owner permission and a reason are required for readiness override.");SupplyRules.Demand(s,c.TargetId).OverrideReason=c.Reason;break;
            case "request":
                var demand=SupplyRules.Demand(s,c.TargetId);SupplyRules.Positive(c.Quantity);
                if(c.Quantity>SupplyRules.Readiness(s,demand,now).Shortage||s.Requests.Any(r=>r.DemandId==demand.Id&&r.Status is "REQUESTED" or "APPROVED"))throw new ArgumentException("Request exceeds shortage or another request is pending.");
                s.Requests.Add(new(){DemandId=demand.Id,Quantity=c.Quantity,Reason=c.Reason,Requester=Actor,AtUtc=now,RequiredAtUtc=demand.RequiredAtUtc,Rush=c.State=="rush"});break;
            case "approve-request":
                if(!owner)throw new MedInsights.Lib.ForbiddenAccessException("An owner approves purchase requests.");
                var request=s.Requests.SingleOrDefault(r=>r.Id==c.TargetId&&r.Status=="REQUESTED")??throw new ArgumentException("Pending request not found.");request.Status="APPROVED";break;
            case "draft-order":SupplyRules.DraftOrder(s,c.Order??throw new ArgumentException("Order required."),Actor,now);break;
            case "order-status":SupplyRules.OrderTransition(s,c,owner,now);if(c.State=="APPROVED"){var order=s.Orders.Single(o=>o.Id==c.TargetId);order.ApprovedBy=Actor;order.ApprovedAtUtc=now;}break;
            case "receive":SupplyRules.Receive(s,c,Actor,now);break;
            case "policy":
                if(!owner)throw new MedInsights.Lib.ForbiddenAccessException("Only an owner can configure supply policy.");var policy=c.Policy??throw new ArgumentException("Policy required.");
                if(policy.ApprovalAbove<0||policy.SpendingLimits.Any(x=>x.Value<0)||policy.Trades.Values.Any(p=>p.Gate is not ("blocker" or "warning")||p.DefaultStrategy is not ("stock" or "direct" or "service" or "rental" or "manual"))||policy.AiActions.Any(a=>!a.Key.StartsWith("supply.")||a.Value is not ("disabled" or "read" or "approval" or "auto" or "draft" or "recommend")))throw new ArgumentException("Invalid supply policy.");s.Policy=policy;break;
            default:throw new ArgumentException("Unknown supply action.");
        }
        s.Audit.Add(new(c.Id,c.Action,Actor,now,c.Reason,c.Item?.Id??c.Location?.Id.ToString()??c.Order?.Id.ToString()??c.Demand?.Id.ToString()??c.TargetId.ToString(),fingerprint));await store.SaveAsync(user.TenantId,s,c.ExpectedVersion,ct);return await WorkspaceAsync(module,ct);
    }
    private async Task<(TurnKeyOps.Lib.Entities.Job,JobWorkflowPayloadDto)> Job(Guid id,CancellationToken ct)
    {var j=await jobs.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(id),ct);if(j is null||j.IsDeleted||j.PartitionKey!=Partition)throw new ArgumentException("Job does not belong to this tenant.");return(j,await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct));}
    private static void ValidateItem(SupplyState s,CatalogItem i)
    {
        if(string.IsNullOrWhiteSpace(i.Id)||i.Id.Length>150||string.IsNullOrWhiteSpace(i.Name)||i.Name.Length>300||!SupplyRules.Units.Contains(i.Unit)||i.Kind is not ("MATERIAL" or "PRODUCT" or "CONSUMABLE" or "EQUIPMENT_PART" or "SERVICE" or "SUBCONTRACT" or "FEE" or "RENTAL" or "OTHER")||i.DefaultCost<0||i.DefaultSellPrice<0)throw new ArgumentException("Provide a valid catalog name, category and unit.");
        if(i.Kind is "SERVICE" or "SUBCONTRACT" or "FEE" or "RENTAL")i.Stocked=false;
        var fields=i.Trades.SelectMany(t=>(s.Policy.Trades.GetValueOrDefault(t)??throw new ArgumentException("Unknown trade profile.")).Fields).ToHashSet();
        if(i.Metadata.Any(m=>!fields.Contains(m.Key)||m.Value.Length>2000)||i.UnitConversions.Any(c=>string.IsNullOrWhiteSpace(c.Key)||c.Key.Length>40||c.Key==i.Unit||c.Value<=0||c.Value>100000000))throw new ArgumentException("Use configured trade metadata and positive explicit unit conversions.");
        if(i.PreferredVendorId.HasValue&&!s.Vendors.Any(v=>v.ContactId==i.PreferredVendorId&&v.Active))throw new ArgumentException("Unknown preferred vendor.");
    }
}
