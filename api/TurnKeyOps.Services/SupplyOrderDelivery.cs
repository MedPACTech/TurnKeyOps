using System.Globalization;
using IBeam.Communications.Abstractions;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
// Freeze and claim before transport. An uncertain attempt is never retried automatically.
public sealed class SupplyOrderDelivery(ISupplyStore store,ISupplyAuthority authority,IUserContext user,IEmailService email,ITenantCommunicationProfileResolver profiles)
{
    public async Task<object> SendAsync(Guid id,string version,CancellationToken ct=default)
    {
        await authority.RequireAsync("purchasing",true,ct);var s=await store.ReadAsync(user.TenantId,ct);var order=s.Orders.SingleOrDefault(o=>o.Id==id)??throw new KeyNotFoundException("Order not found.");
        if(order.Dispatch is not null)return View(order.Dispatch);
        if(s.Version!=version)throw new InvalidOperationException("Supply changed. Review before sending.");
        if(order.Status!="APPROVED")throw new ArgumentException("Approve the order before sending.");
        if(order.ApprovalPolicyHash!=SupplyRules.PolicyHash(s.Policy))throw new ArgumentException("Purchasing rules changed. Request approval again.");
        var vendor=s.Vendors.SingleOrDefault(v=>v.ContactId==order.VendorId&&v.Active)??throw new ArgumentException("Active vendor required.");
        if(!System.Net.Mail.MailAddress.TryCreate(vendor.OrderingEmail,out var address)||address.Address!=vendor.OrderingEmail)throw new ArgumentException("Configure a valid vendor ordering email.");
        var profile=profiles.Resolve(user.TenantId);
        var dispatch=new SupplyDispatch{Id=Guid.NewGuid(),Recipient=vendor.OrderingEmail,Actor=user.UserId.ToString(),AtUtc=DateTime.UtcNow,Status="sending",Body=$"Purchase order {order.Number}\n\n"+string.Join("\n",order.Lines.Select(l=>$"{l.Quantity.ToString(CultureInfo.InvariantCulture)} {l.Unit} — {l.Description} @ {l.UnitCost.ToString("0.00",CultureInfo.InvariantCulture)}"))+$"\nTotal: {order.Total.ToString("0.00",CultureInfo.InvariantCulture)}\nExpected: {order.ExpectedAtUtc:O}\n{order.DeliveryInstructions}\nPlease confirm this order and the delivery date."};
        order.Dispatch=dispatch;s.Audit.Add(new(dispatch.Id,"po.send-claimed",dispatch.Actor,dispatch.AtUtc,$"Order {order.Number}; vendor {vendor.ContactId}"));
        await store.SaveAsync(user.TenantId,s,version,ct);
        var accepted=false;
        try{var message=new EmailMessage{FromAddress=profile.EmailFromAddress,FromName=profile.EmailFromName,Subject=$"Purchase order {order.Number}",TextBody=dispatch.Body};message.To.Add(dispatch.Recipient);await email.SendAsync(message,ct:ct);accepted=true;}
        catch{ /* Persist uncertainty; the provider might have accepted the message. */ }
        for(var attempt=0;attempt<4;attempt++){
            var latest=await store.ReadAsync(user.TenantId,CancellationToken.None);var current=latest.Orders.Single(o=>o.Id==id);
            if(current.Dispatch?.Id!=dispatch.Id)throw new InvalidOperationException("Vendor dispatch identity changed.");
            current.Dispatch.Status=accepted?"provider-accepted":"unconfirmed";current.Dispatch.ProviderAcceptedAtUtc=accepted?DateTime.UtcNow:null;
            if(accepted&&current.Status=="APPROVED"){current.Status="SENT";current.SentAtUtc=DateTime.UtcNow;current.VendorReference=$"Provider acceptance {dispatch.Id}";}
            latest.Audit.Add(new(Guid.NewGuid(),"po.send-result",user.UserId.ToString(),DateTime.UtcNow,$"{order.Number}: {current.Dispatch.Status}"));
            try{await store.SaveAsync(user.TenantId,latest,latest.Version,CancellationToken.None);return View(current.Dispatch);}catch(InvalidOperationException)when(attempt<3){}
        }
        return View(dispatch);
    }
    private static object View(SupplyDispatch d)=>new{d.Id,d.Status,message=d.Status=="provider-accepted"?"Accepted by the email provider. Vendor delivery and acknowledgment are not yet confirmed.":"Send outcome is unconfirmed. Review provider history before taking further action."};
}
