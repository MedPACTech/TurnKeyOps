using System.Security.Cryptography;
using System.Text;
using IBeam.Communications.Abstractions;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories;
namespace TurnKeyOps.Services;
public sealed class JobDeliveryService(JobExecutionService jobs,JobConfigurationService configuration,IUserContext user,IEmailService email,ISmsService sms,ITenantCommunicationProfileResolver profiles,IJobAuthority authority,IJobNotificationStore receipts,JobNotificationDispatcher dispatcher)
{
    private string Partition=>RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
    public async Task<object> ListAsync(Guid id,CancellationToken ct)
    {
        await jobs.WorkspaceAsync(id,ct);
        var result=new List<object>();
        await foreach(var receipt in receipts.ListAsync(Partition,ct))if(receipt.JobId==id)result.Add(View(receipt));
        return result;
    }
    private static object View(JobNotification receipt)=>new{receiptId=receipt.RowKey,receipt.Channel,receipt.Template,receipt.Status,receipt.CreatedAtUtc,receipt.AttemptedAtUtc,receipt.ProviderAcceptedAtUtc,
        message=receipt.Status=="provider-accepted"?"Accepted by communication provider; customer delivery is not yet verified.":receipt.Status=="prepared"?"Prepared; not sent.":"Delivery unconfirmed. Review provider history before requesting another notification."};
    public async Task<object> SendAsync(Guid id,string version,string channel,string template,CancellationToken ct)
    {
        await authority.RequireAsync(true,ct);var workspace=await jobs.WorkspaceAsync(id,ct);var job=workspace.Job;
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}|{version}|{channel}|{template}")));
        var existing=await receipts.GetAsync(Partition,key,ct);
        if(existing is not null&&existing.Status!="prepared")return View(existing);
        if(job.Version!=version)throw new ArgumentException("The job changed. Review the notification again.");
        var config=await configuration.GetAsync(user.TenantId,ct);
        if(!config.CustomerChannels.TryGetValue(job.CustomerId,out var allowed)||!allowed.Contains(channel))throw new ArgumentException("Confirm this customer's communication preference before sending.");
        if(!config.NotificationTemplates.TryGetValue(template,out var text)||string.IsNullOrWhiteSpace(text))throw new ArgumentException("Configure this tenant's notification template first.");
        if(channel=="email"&&string.IsNullOrWhiteSpace(job.ContactEmail)||channel=="sms"&&string.IsNullOrWhiteSpace(job.ContactPhone)||channel is not ("email" or "sms"))throw new ArgumentException("A valid preferred communication channel and contact are required.");
        var body=text.Replace("{job}",job.Name).Replace("{state}",workspace.StateLabel).Replace("{site}",job.JobSiteName??"");
        var profile=profiles.Resolve(user.TenantId);
        var proposed=new JobNotification{PartitionKey=Partition,RowKey=key,JobId=id,ActorId=user.UserId,Channel=channel,Template=template,Recipient=channel=="email"?job.ContactEmail!:job.ContactPhone!,Body=body};
        var result=await dispatcher.DispatchAsync(proposed,async(receipt,token)=>{
            if(channel=="email"){var message=new EmailMessage{FromAddress=profile.EmailFromAddress,FromName=profile.EmailFromName,Subject=$"Job update · {job.Name}",TextBody=receipt.Body};message.To.Add(receipt.Recipient);await email.SendAsync(message,ct:token);}
            else{var message=new SmsMessage{FromPhoneNumber=profile.SmsFromPhoneNumber,Body=receipt.Body};message.To.Add(receipt.Recipient);await sms.SendAsync(message,ct:token);}
        },ct);
        return View(result);
    }
}
