using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed partial class PortalService
{
    public async Task<object> JobActionAsync(PortalActor a, Guid id, PortalCommand input, CancellationToken ct)
    {
        var j=await Job(a,id,ct); PortalRules.ExactVersion(input.ExpectedVersion,Version(j));
        var p=await LoadPayload(a,j,ct); var x=p.Execution??throw new ArgumentException("This project is not ready for customer actions.");
        var now=DateTime.UtcNow; string label;
        switch(input.Action) {
            case "report-issue":
                if(x.Issues.Count>=500)throw new ArgumentException("Please contact your contractor directly.");
                x.Issues.Add(new(){Description=PortalRules.Text(input.Text),Type="customer-service",Severity="warning",CreatedAtUtc=now,PortalUserId=a.UserId,OriginalJobId=id,ServiceKind="service-review"});
                if(x.Acceptances.Any(v=>v.CompletionRevision==x.CompletionRevision))x.CompletionRevision++;
                label="A customer issue was reported for review."; break;
            case "accept-completion":
                if(!a.Configuration.CompletionAcceptanceEnabled || j.Status is not (JobStatus.CompletionReview or JobStatus.Completed))throw new ArgumentException("Completion acceptance is not available.");
                if(JobExecutionRules.Blockers(x,"complete").Count>0)throw new ArgumentException("Your contractor needs to finish the completion review before you can accept this work.");
                if(x.Acceptances.Any(v=>v.CompletionRevision==x.CompletionRevision))throw new ArgumentException("This completion revision is already accepted.");
                if(!input.Consent || input.Hash!=PortalRules.CompletionHash(x))throw new ArgumentException("Review the current completed scope and confirm your intent to sign.");
                if(!x.Profile.AllowQualifiedAcceptance && !string.IsNullOrWhiteSpace(input.Text))throw new ArgumentException("Report an issue before accepting work with exceptions.");
                var signer=PortalRules.Text(input.Signer,200);
                x.Acceptances.Add(new(){CompletionRevision=x.CompletionRevision,CompletionHash=input.Hash,AcceptedState=JobExecutionRules.State(j.Status),Signer=signer,Signature=signer,
                    Statement=PortalRules.CompletionStatement,Exceptions=string.IsNullOrWhiteSpace(input.Text)?"":PortalRules.Text(input.Text),RecordedBy=$"portal:{a.UserId:D}",AcceptedAtUtc=now});
                label="Completion accepted by customer.";break;
            case "approve-change": case "decline-change":
                if(!a.Configuration.ChangeApprovalEnabled)throw new ArgumentException("Change decisions are not enabled.");
                var c=x.Changes.SingleOrDefault(c=>c.Id==input.ItemId&&c.CustomerVisible)??throw new KeyNotFoundException();
                if(c.Status!="APPROVAL_REQUIRED"||input.Hash!=PortalRules.ChangeHash(c))throw new ArgumentException("Review the current change before deciding.");
                if(!input.Consent)throw new ArgumentException("Confirm your decision after reviewing the change.");
                if(input.Action=="approve-change"&&c.PricingImpact){
                    if(c.AcceptedEstimateId is null || c.AcceptedEstimateId==p.AcceptedEstimate?.EstimateId)throw new ArgumentException("A separate accepted change proposal is required.");
                    var estimate=await estimates.PortalPacketAsync(a,c.AcceptedEstimateId.Value,ct);
                    if(estimate.CustomerId!=j.CustomerId || estimate.Document?.SiteId!=j.JobSiteId || estimate.ApprovalSignature is null || estimate.Delivery?.Status!="approved" || c.AcceptedRevision!=estimate.RevisionNumber || c.AcceptedDocumentHash!=estimate.DocumentHash)
                        throw new ArgumentException("Accept the exact change proposal for this project and site before approving the change.");
                }
                c.CustomerDecisionHash=input.Hash;c.PortalUserId=a.UserId;c.CustomerDecidedAtUtc=now;c.CustomerApproval=input.Action=="approve-change"?"Verified customer approval":"Verified customer decline";
                c.Status=input.Action=="approve-change"?"APPROVED":"DECLINED";
                if(x.Acceptances.Any(v=>v.CompletionRevision==x.CompletionRevision))x.CompletionRevision++;
                label=input.Action=="approve-change"?"Change approved by customer.":"Change declined by customer.";break;
            default:throw new ArgumentException("Unknown customer action.");
        }
        p.Activity.Add(new(){Id=Guid.NewGuid(),CustomerVisible=true,Type="portal."+input.Action,Label=label,Actor=$"portal:{a.UserId:D}",OccurredAtUtc=now});
        p.PreviousVersionBlobName=j.WorkflowPayloadBlobName;
        j.WorkflowPayloadBlobName=await payloads.SaveAsync(a.TenantId,id,p,ct);j.DateUpdated=now;
        await jobs.SaveAsync(j,ct);
        await access.Audit(a.TenantId,a.UserId,input.Action,id,ct);
        return await WorkAsync(a,"job",id,ct);
    }
    public async Task<QuoteRequestAttachmentDownload> JobFileAsync(PortalActor a,Guid id,Guid fileId,CancellationToken ct)
    {
        var j=await Job(a,id,ct);var p=await LoadPayload(a,j,ct);
        var f=p.Execution?.Evidence.SingleOrDefault(f=>f.Id==fileId&&f.CustomerVisible);
        if(f is null){
            var source=p.AcceptedEstimate?.Document?.Attachments.SingleOrDefault(v=>v.Id==fileId)??throw new KeyNotFoundException();
            if(string.IsNullOrWhiteSpace(source.ContentHash)||!source.BlobName.StartsWith($"{a.TenantId:N}/{p.AcceptedEstimate!.EstimateId:N}/attachments/",StringComparison.Ordinal))throw new KeyNotFoundException();
            return new(source.Name,source.ContentType,source.SizeBytes,await blobs.OpenReadAsync(QuoteEstimateService.ContainerName,source.BlobName,ct));
        }
        if(!f.BlobName.StartsWith($"{a.TenantId:N}/{id:N}/",StringComparison.Ordinal))throw new KeyNotFoundException();
        var stream=await blobs.OpenReadAsync("job-workflows",f.BlobName,ct);
        return new(f.Name,f.ContentType,stream.CanSeek?stream.Length:0,stream);
    }
}
