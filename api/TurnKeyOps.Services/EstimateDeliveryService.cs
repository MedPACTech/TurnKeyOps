using IBeam.Communications.Abstractions;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services;
public sealed class EstimateDeliveryService(IEmailService email, ISmsService sms, ITenantCommunicationProfileResolver profiles)
{
    public async Task SendAsync(Guid tenantId, QuoteEstimateDto packet, string origin, string channel, string note,CancellationToken ct)
    {
        if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/")throw new ArgumentException("Configure the tenant's HTTPS public origin before delivering proposals.");
        if(packet.Delivery is null)throw new ArgumentException("Issue the revision before delivery.");
        var profile=profiles.Resolve(tenantId);
        var body=$"{note}\n\nReview proposal revision {packet.RevisionNumber} for {packet.SiteName}:\n{origin.TrimEnd('/')}{packet.Delivery.ReviewUrl}";
        if(channel=="email")
        {
            if(string.IsNullOrWhiteSpace(packet.Delivery.Email))throw new ArgumentException("Customer email is missing.");
            var message=new EmailMessage{FromAddress=profile.EmailFromAddress,FromName=profile.EmailFromName,Subject=$"Proposal · {packet.SiteName} · revision {packet.RevisionNumber}",TextBody=body};message.To.Add(packet.Delivery.Email);await email.SendAsync(message,ct:ct);
        }
        else if(channel=="sms")
        {
            if(string.IsNullOrWhiteSpace(packet.Delivery.Phone))throw new ArgumentException("Customer phone is missing.");
            var message=new SmsMessage{FromPhoneNumber=profile.SmsFromPhoneNumber,Body=body};message.To.Add(packet.Delivery.Phone);await sms.SendAsync(message,ct:ct);
        }
        else throw new ArgumentException("Choose email or SMS.");
    }
}
