using System.Security.Cryptography;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;
public sealed partial class QuoteEstimateService
{
    private async Task FreezeAttachmentsAsync(QuoteEstimateDto packet,QuoteRequest quote,CancellationToken ct)
    {
        if(packet.Document is null)return;
        var originals=QuoteRequestMapper.ToDto(quote).Attachments;
        foreach(var attachment in packet.Document.Attachments)
        {
            if(!string.IsNullOrWhiteSpace(attachment.BlobName))continue;
            var original=originals.SingleOrDefault(x=>x.Id==attachment.Id)??throw new ArgumentException("An attached file was removed. Review the draft before issuing.");
            var prefix=$"{_userContext.TenantId:N}/{quote.Id:N}/";
            if(original.BlobContainer!=QuoteRequestAttachmentService.ContainerName||original.BlobName is null||!original.BlobName.StartsWith(prefix,StringComparison.Ordinal))throw new ArgumentException("Invalid attachment source.");
            await using var source=await _blobStorage.OpenReadAsync(original.BlobContainer,original.BlobName,ct);
            using var data=new MemoryStream();await source.CopyToAsync(data,ct);
            attachment.ContentHash=Convert.ToHexString(SHA256.HashData(data.ToArray())).ToLowerInvariant();
            attachment.BlobName=$"{prefix}attachments/{attachment.ContentHash}";attachment.ContentType=original.ContentType;attachment.SizeBytes=data.Length;
            data.Position=0;await _blobStorage.UploadAsync(ContainerName,attachment.BlobName,data,attachment.ContentType,new Dictionary<string,string>(),ct);
        }
    }
    public async Task<QuoteRequestAttachmentDownload?> DownloadProposalFileAsync(string slug,Guid id,Guid fileId,string token,CancellationToken ct=default)
    {
        var tenant=_tenantResolver.Resolve(slug).TenantId;
        var entity=await GetEntityAsync(tenant,id,ct);
        if(entity is null||!ValidToken(entity,token))entity=await _repository.GetArchiveAsync(Partition(tenant),id,HashToken(token),ct);
        if(entity is null||entity.PartitionKey!=Partition(tenant)||entity.QuoteRequestId!=id||!ValidToken(entity,token))return null;
        var packet=await LoadPayloadAsync(entity,ct);
        var file=packet.Document?.Attachments.SingleOrDefault(x=>x.Id==fileId);
        if(file is null||!file.BlobName.StartsWith($"{tenant:N}/{id:N}/attachments/",StringComparison.Ordinal))return null;
        return new(file.Name,file.ContentType,file.SizeBytes,await _blobStorage.OpenReadAsync(ContainerName,file.BlobName,ct));
    }
    public async Task<QuoteEstimateDto> DeliverAsync(Guid id,EstimateTaskDto input,EstimateDeliveryService delivery,CancellationToken ct=default)
    {
        var(entity,packet)=await EditablePacketAsync(id,input.ExpectedVersion,ct);
        if(packet.Delivery is null||packet.ExpiresAtUtc<=DateTime.UtcNow||packet.Outcome is not null||packet.Delivery.Status!="sent")throw new ArgumentException("Only an open, unexpired issued proposal can be delivered/reminded.");
        if(input.Text.Length>2000||input.Channel is not("email" or "sms"))throw new ArgumentException("Choose email/SMS and use a message up to 2000 characters.");
        var policy=await PricingPolicyAsync(ct);
        AddEstimateEvent(packet,"delivery-pending",$"{input.Channel} delivery requested; confirm provider history if interrupted.");
        await PersistAsync(entity,packet,_userContext.TenantId,entity.CustomerAccessTokenHash,entity.AccessTokenExpiresAtUtc,ct);
        entity=(await GetEntityAsync(_userContext.TenantId,id,ct))!;
        try {await delivery.SendAsync(_userContext.TenantId,packet,policy.PublicOrigin,input.Channel,input.Text,ct);}
        catch {AddEstimateEvent(packet,"delivery-unconfirmed","Provider delivery was not confirmed. Check provider history before retrying.");await PersistAsync(entity,packet,_userContext.TenantId,entity.CustomerAccessTokenHash,entity.AccessTokenExpiresAtUtc,ct);throw;}
        AddEstimateEvent(packet,"delivered",$"Proposal/reminder sent by {input.Channel} to the issued recipient.");
        return await PersistAsync(entity,packet,_userContext.TenantId,entity.CustomerAccessTokenHash,entity.AccessTokenExpiresAtUtc,ct);
    }
    public async Task<QuoteEstimateDto> VoidAsync(Guid id,string version,CancellationToken ct=default)
    {
        var(entity,packet)=await EditablePacketAsync(id,version,ct);
        if(packet.ApprovalSignature is not null)throw new ArgumentException("Accepted proposals cannot be voided through this workflow.");
        packet.Outcome="VOID";AddEstimateEvent(packet,"void","Proposal withdrawn; its document remains preserved.");
        return await PersistAsync(entity,packet,_userContext.TenantId,null,null,ct);
    }
    public async Task RecordJobHandoffAsync(Guid id,Guid jobId,CancellationToken ct=default)
    {
        await RequireWorkspaceAsync(true,ct);
        var entity=await GetEntityAsync(_userContext.TenantId,id,ct)??throw new KeyNotFoundException();
        var packet=await LoadPayloadAsync(entity,ct);
        if(packet.ApprovalSignature is null||packet.Events.Any(x=>x.Type=="job-linked"&&x.Revision==packet.RevisionNumber))return;
        AddEstimateEvent(packet,"job-linked",$"Accepted scope linked to Job {jobId:D}; later estimate revisions do not replace the Job snapshot.");
        await PersistAsync(entity,packet,_userContext.TenantId,entity.CustomerAccessTokenHash,entity.AccessTokenExpiresAtUtc,ct);
    }
    public async Task<object> MetricsAsync(CancellationToken ct=default)
    {
        await RequireWorkspaceAsync(false,ct);var packets=await ListAsync(ct);
        return new {volume=packets.Count, value=packets.Sum(x=>x.Totals.EstimatedTotal),averageSize=packets.Count==0?(decimal?)null:packets.Average(x=>x.Totals.EstimatedTotal),
            sent=packets.Count(x=>x.SentAtUtc.HasValue),accepted=packets.Count(x=>x.ApprovalSignature is not null),revisions=packets.Sum(x=>x.RevisionNumber),
            stages=packets.GroupBy(State).Select(g=>new{state=g.Key,count=g.Count()}),
            attribution=packets.GroupBy(x=>new{x.Document?.TradeProfile,x.Document?.Source}).Select(g=>new{g.Key.TradeProfile,g.Key.Source,count=g.Count(),accepted=g.Count(x=>x.ApprovalSignature is not null)})};
    }
}
