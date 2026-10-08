using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IBeam.Communications.Abstractions;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
// Existing iBeam providers and durable dispatcher. No new transport or unattended retries.
public sealed class PortalNotifications(PortalAccessService access,PortalService portal,IPlatformUserRepository users,
    JobNotificationDispatcher dispatcher,IEmailService email,ISmsService sms,ITenantCommunicationProfileResolver profiles)
{
    public async Task<object> SendAsync(Guid tenant,Guid operatorId,Guid recipientId,string kind,Guid id,string expectedVersion,string channel,string template,CancellationToken ct)
    {
        var a=await access.NotificationActorAsync(tenant,recipientId,ct);
        var detail=JsonSerializer.SerializeToElement(await portal.WorkAsync(a,kind,id,ct),JobConfigurationService.Json);
        var version=detail.TryGetProperty("version",out var v)?v.GetString():detail.TryGetProperty("documentHash",out var h)?h.GetString():null;
        if(version is null)throw new ArgumentException("Notifications currently require a project or proposal context.");
        PortalRules.ExactVersion(expectedVersion,version);
        if(!(await access.PreferencesAsync(a,ct)).Contains(channel))throw new ArgumentException("The customer has not opted into this channel.");
        if(!a.Configuration.NotificationTemplates.TryGetValue(template,out var body)||string.IsNullOrWhiteSpace(body))throw new ArgumentException("Choose a configured notification template.");
        var identity=await users.GetAsync($"USER={recipientId:N}","PROFILE",ct);
        if(identity is null||identity.IsDeleted||!identity.IsActive)throw new KeyNotFoundException();
        var recipient=channel switch {"email" when identity.EmailVerified=>identity.PrimaryEmail,"sms" when identity.PhoneVerified=>identity.PrimaryPhone,_=>null};
        if(string.IsNullOrWhiteSpace(recipient))throw new ArgumentException("This notification channel is not verified.");
        var key="PORTAL-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenant:N}|{recipientId:N}|{kind}|{id:N}|{version}|{channel}|{template}")));
        var receipt=new JobNotification{PartitionKey=RepositoryKeyHelper.ToTenantPartitionKey(tenant),RowKey=key,JobId=kind=="job"?id:Guid.Empty,ActorId=operatorId,Channel=channel,Template="portal:"+template,Recipient=recipient,Body=body};
        var profile=profiles.Resolve(tenant);
        var result=await dispatcher.DispatchAsync(receipt,async(r,token)=>{
            // Recheck opt-in and entitlement immediately before the provider call.
            var current=await access.NotificationActorAsync(tenant,recipientId,token);await portal.RequireContextAsync(current,kind,id,token);
            if(!(await access.PreferencesAsync(current,token)).Contains(channel))throw new InvalidOperationException("Preference changed.");
            if(channel=="email"){var message=new EmailMessage{FromAddress=profile.EmailFromAddress,FromName=profile.EmailFromName,Subject="Your project update",TextBody=r.Body};message.To.Add(r.Recipient);await email.SendAsync(message,ct:token);}
            else{var message=new SmsMessage{FromPhoneNumber=profile.SmsFromPhoneNumber,Body=r.Body};message.To.Add(r.Recipient);await sms.SendAsync(message,ct:token);}
        },ct);
        await access.Audit(tenant,operatorId,"notification."+result.Status,id,ct);
        return new{result.Status,message=result.Status=="provider-accepted"?"Accepted by provider; delivery is not yet verified.":"Delivery unconfirmed. Review provider history before retrying."};
    }
}
