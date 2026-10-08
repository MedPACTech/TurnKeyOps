using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;

// Extends existing ChatMessages storage. Dedicated partition and explicit metadata isolate customer conversations from internal AI chat.
public sealed class PortalMessages(IChatMessageRepository messages,PortalService portal,IAzureBlobStorageService blobs,PortalAccessService access)
{
    public sealed class ContextMetadata
    {
        public bool CustomerVisible {get;set;}
        public string Kind {get;set;}="";
        public Guid RecordId {get;set;}
        public Guid CustomerId {get;set;}
        public string? BlobName {get;set;}
        public string? FileName {get;set;}
        public string? ContentType {get;set;}
    }
    private static string Partition(Guid tenant)=>RepositoryKeyHelper.ToTenantPartitionKey(tenant)+"|PORTAL";
    private static Guid Thread(Guid tenant,string kind,Guid id)=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenant:N}:{kind}:{id:N}"))[..16]);
    private static ContextMetadata? Metadata(ChatMessage m){try{return JsonSerializer.Deserialize<ContextMetadata>(m.MetadataJson);}catch(JsonException){return null;}}
    private async Task<List<ChatMessage>> Read(PortalActor a,string kind,Guid id,CancellationToken ct)
    {
        var customer=await portal.RequireContextAsync(a,kind,id,ct);var thread=Thread(a.TenantId,kind,id);
        return (await messages.GetMessagesByChatAsync(Partition(a.TenantId),thread,200,ct)).Where(m=>!m.IsDeleted&&m.TenantId==a.TenantId&&m.ChatId==thread&&m.PartitionKey==Partition(a.TenantId)&&
            Metadata(m) is {CustomerVisible:true} meta&&meta.Kind==kind&&meta.RecordId==id&&meta.CustomerId==customer).ToList();
    }
    public async Task<object> ListAsync(PortalActor a,string kind,Guid id,CancellationToken ct)=>
        (await Read(a,kind,id,ct)).Select(m=>new{id=m.Id,text=m.Content,atUtc=m.ChatTimestamp,from=m.Role=="portal-company"?"Your contractor":"Customer",file=Metadata(m)?.FileName is {} name?new PortalFile(m.Id,name,Metadata(m)!.ContentType??"application/octet-stream"):null});
    public async Task<object> SendAsync(PortalActor a,string kind,Guid id,string text,CancellationToken ct,bool company=false)
    {
        if(!a.Configuration.MessagingEnabled)throw new ArgumentException("Messaging is currently unavailable.");
        var customer=await portal.RequireContextAsync(a,kind,id,ct);
        await Save(a,kind,id,customer,PortalRules.Text(text),null,ct,company);
        await access.Audit(a.TenantId,a.UserId,"message.submitted",id,ct);
        return await ListAsync(a,kind,id,ct);
    }
    private async Task Save(PortalActor a,string kind,Guid id,Guid customer,string text,ContextMetadata? file,CancellationToken ct,bool company=false)
    {
        var messageId=Guid.NewGuid();var now=DateTime.UtcNow;var thread=Thread(a.TenantId,kind,id);
        var metadata=file??new();metadata.CustomerVisible=true;metadata.Kind=kind;metadata.RecordId=id;metadata.CustomerId=customer;
        var prefix=RepositoryKeyHelper.GetOrderedRowKeyPrefix(thread);
        await messages.AppendCustomerMessageAsync(new(){Id=messageId,MessageId=messageId,PartitionKey=Partition(a.TenantId),RowKey=$"{prefix}{now.Ticks:D19}|{messageId:N}",TenantId=a.TenantId,ActorUserId=a.UserId,ChatId=thread,Role=company?"portal-company":"portal-customer",Content=text,ChatTimestamp=now,MetadataJson=JsonSerializer.Serialize(metadata)},ct);
    }
    public async Task<object> UploadAsync(PortalActor a,string kind,Guid id,string name,string contentType,Stream input,CancellationToken ct)
    {
        if(!a.Configuration.UploadEnabled)throw new ArgumentException("Uploads are currently unavailable.");
        var customer=await portal.RequireContextAsync(a,kind,id,ct);
        if(contentType is not ("image/jpeg" or "image/png" or "application/pdf"))throw new ArgumentException("Upload a JPEG, PNG or PDF, up to 10 MB.");
        using var data=new MemoryStream();var buffer=new byte[81920];int read;
        while((read=await input.ReadAsync(buffer,ct))>0){if(data.Length+read>10*1024*1024)throw new ArgumentException("The file exceeds 10 MB.");await data.WriteAsync(buffer.AsMemory(0,read),ct);}
        var bytes=data.ToArray();
        var valid=contentType switch {"image/jpeg"=>bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255,"image/png"=>bytes.Length>=8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"application/pdf"=>bytes.Length>=5&&Encoding.ASCII.GetString(bytes,0,5)=="%PDF-",_=>false};
        if(!valid)throw new ArgumentException("The file content does not match its type.");
        name=PortalRules.Text(Path.GetFileName(name.Replace('\\','/')),160);var blob=$"{a.TenantId:N}/{kind}/{id:N}/{Guid.NewGuid():N}";data.Position=0;
        await blobs.UploadAsync("portal-files",blob,data,contentType,new Dictionary<string,string>(),ct);
        await Save(a,kind,id,customer,"File uploaded",new(){BlobName=blob,FileName=name,ContentType=contentType},ct);
        await access.Audit(a.TenantId,a.UserId,"file.uploaded",id,ct);
        return await ListAsync(a,kind,id,ct);
    }
    public async Task<QuoteRequestAttachmentDownload> DownloadAsync(PortalActor a,string kind,Guid id,Guid fileId,CancellationToken ct)
    {
        var m=(await Read(a,kind,id,ct)).SingleOrDefault(m=>m.Id==fileId)??throw new KeyNotFoundException();var f=Metadata(m)!;
        if(f.BlobName is null||!f.BlobName.StartsWith($"{a.TenantId:N}/{kind}/{id:N}/",StringComparison.Ordinal))throw new KeyNotFoundException();
        var stream=await blobs.OpenReadAsync("portal-files",f.BlobName,ct);return new(f.FileName!,f.ContentType!,stream.CanSeek?stream.Length:0,stream);
    }
}
