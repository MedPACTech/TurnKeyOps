using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;
namespace TurnKeyOps.Services;

public sealed class JobExecutionService(IJobRepository jobs,IJobWorkflowPayloadStore payloads,IJobService legacy,
    IJobAuthority authority,JobConfigurationService configuration,IUserContext user,ICalendarEventRepository calendar,
    ITenantMembershipRepository memberships,ICustomerRepository customers,IJobSiteRepository sites,
    IAzureBlobStorageService blobs,IQuoteEstimateService estimates, IUserProfileRepository profiles, ISupplyJobReadiness? supplyReadiness = null)
{
    private string Partition=>RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
    private string Actor=>user.UserId.ToString("D");
    private static string Version(Job job)=>string.IsNullOrWhiteSpace(job.ETag.ToString())?job.DateUpdated.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture):job.ETag.ToString();
    private async Task<(Job,JobWorkflowPayloadDto)> Load(Guid id,bool write,string? version,CancellationToken ct)
    {
        await authority.RequireAsync(write,ct);
        var job=await jobs.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(id),ct);
        if(job is null||job.IsDeleted||job.PartitionKey!=Partition)throw new KeyNotFoundException("Job not found.");
        if(write&&(string.IsNullOrWhiteSpace(version)||version!=Version(job)))throw new InvalidOperationException("The job changed. Refresh before saving.");
        var payload=await payloads.LoadAsync(job.WorkflowPayloadBlobName,ct);
        if(supplyReadiness is not null)await supplyReadiness.ApplyAsync(job.Id,payload.Execution,ct);
        return (job,payload);
    }
    private async Task Save(Job job,JobWorkflowPayloadDto payload,string type,string text,CancellationToken ct)
    {
        payload.Activity.Add(new(){Id=Guid.NewGuid(),Type=type,Label=text,Actor=Actor,OccurredAtUtc=DateTime.UtcNow});
        // Retain immutable workflow versions as audit evidence; failed CAS never overwrites earlier history.
        payload.PreviousVersionBlobName=job.WorkflowPayloadBlobName;
        var blob=await payloads.SaveAsync(user.TenantId,job.Id,payload,ct);
        job.WorkflowPayloadBlobName=blob;job.DateUpdated=DateTime.UtcNow;
        try{await jobs.SaveAsync(job,ct);}catch{await payloads.DeleteIfExistsAsync(blob,CancellationToken.None);throw;}
    }
    public async Task<object> ChoicesAsync(CancellationToken ct)
    {
        await authority.RequireAsync(false,ct);
        var people=new List<MedInsights.Lib.Entities.UserProfile>();string? token=null;
        do{var page=await profiles.GetByPartitionPagedAsync(Partition,100,token,ct);people.AddRange(page.Results.Where(p=>p.PartitionKey==Partition&&!p.IsDeleted));token=page.ContinuationToken;}while(token is not null);
        var members=await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct);
        return new {customers=(await customers.ListAsync(Partition,ct)).Where(c=>c.PartitionKey==Partition&&!c.IsDeleted).Select(c=>new{c.Id,name=$"{c.FirstName} {c.LastName}".Trim(),c.CompanyName}),
            sites=(await sites.GetAllAsync(false,false)).Where(s=>s.PartitionKey==Partition&&!s.IsDeleted).Select(s=>new{s.Id,s.Name,s.Address}),
            members=members.Where(m=>m.TenantId==user.TenantId&&!m.IsDeleted&&!m.DateRemoved.HasValue&&m.Role!="contact").Select(m=>new{m.Id,name=people.Where(p=>p.ApplicationUserId==m.UserId).Select(p=>$"{p.FirstName} {p.LastName}".Trim()).FirstOrDefault()??m.InvitedEmail??"Team member",team=people.FirstOrDefault(p=>p.ApplicationUserId==m.UserId)?.Team})};
    }
    public async Task<object> ListAsync(CancellationToken ct)
    {
        await authority.RequireAsync(false,ct);var result=new List<object>();
        var myMemberships=(await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct)).Where(m=>m.UserId==user.UserId&&m.TenantId==user.TenantId&&!m.IsDeleted).Select(m=>m.Id).ToHashSet();
        var scheduled=await calendar.ListAsync(Partition,ct);
        foreach(var j in await jobs.ListAsync(Partition,ct)){
            if(j.PartitionKey!=Partition||j.IsDeleted)continue;
            var p=await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct);if(supplyReadiness is not null)await supplyReadiness.ApplyAsync(j.Id,p.Execution,ct);var state=JobExecutionRules.State(j.Status);
            if(p.Execution is not null) ApplyCalendarSummary(j, scheduled);
            result.Add(new {j.Id,j.Name,j.CustomerId,j.CustomerName,j.JobSiteId,j.JobSiteName,j.ScheduledStart,j.ScheduledEnd,j.Crew,
                state,stateLabel=p.Execution?.Profile.StateLabels.GetValueOrDefault(state),trade=p.Execution?.TradeProfile??JobExecutionRules.Trade(j.TradeType),
                nextAction=JobExecutionRules.NextAction(state),modern=p.Execution is not null,
                blockers=p.Execution is null?0:JobExecutionRules.Blockers(p.Execution,state is "PLANNING" or "READY_TO_SCHEDULE"?"schedule":"start").Count+EventConflicts(scheduled,j.Id).Count,
                atRisk=j.ScheduledEnd<DateTime.UtcNow&&state is not ("COMPLETED" or "CLOSED" or "CANCELLED"),
                ownerMembershipId=p.Execution?.OwnerMembershipId,assignedToMe=p.Execution?.OwnerMembershipId is Guid owner&&myMemberships.Contains(owner)||scheduled.Any(e=>e.JobId==j.Id&&!e.IsDeleted&&e.EventStatus=="scheduled"&&(e.MembershipIds.Any(myMemberships.Contains)||e.AssignedTechnicianMembershipId is Guid tech&&myMemberships.Contains(tech)))});
        }
        var policy=await configuration.GetAsync(user.TenantId,ct);
        return new {jobs=result,canWrite=await authority.CanWriteAsync(ct),canConfigure=await authority.IsOwnerAsync(ct),configuration=new{policy.AllowManual,policy.EnabledTrades}};
    }
    private static void ApplyCalendarSummary(Job job, IEnumerable<CalendarEvent> events)
    {
        var active=events.Where(e=>e.PartitionKey==job.PartitionKey&&e.JobId==job.Id&&!e.IsDeleted&&e.EventStatus!="cancelled"&&e.JobEventType!="delivery").OrderBy(e=>e.StartUtc).ToList();
        job.ScheduledStart=active.Count==0?null:active.Min(e=>e.StartUtc);
        job.ScheduledEnd=active.Count==0?null:active.Max(e=>e.EndUtc);
        job.AssignedTechnicianMembershipId=active.FirstOrDefault()?.AssignedTechnicianMembershipId;
    }
    private static List<string> EventConflicts(IEnumerable<CalendarEvent> events,Guid jobId)
    {
        var active=events.Where(e=>!e.IsDeleted&&e.EventStatus=="scheduled").ToArray();
        return active.Where(e=>e.JobId==jobId).Where(e=>active.Any(other=>other.Id!=e.Id&&other.StartUtc<e.EndUtc&&other.EndUtc>e.StartUtc&&
            (other.MembershipIds.Intersect(e.MembershipIds).Any()||other.AssignedTechnicianMembershipId.HasValue&&(e.MembershipIds.Contains(other.AssignedTechnicianMembershipId.Value)||e.AssignedTechnicianMembershipId==other.AssignedTechnicianMembershipId)||other.ResourceIds.Intersect(e.ResourceIds,StringComparer.OrdinalIgnoreCase).Any())))
            .Select(e=>$"Schedule conflict: {e.Title}").Distinct().ToList();
    }
    public async Task<JobWorkspaceDto> WorkspaceAsync(Guid id,CancellationToken ct=default)
    {
        var (j,p)=await Load(id,false,null,ct);var dto=await legacy.GetAsync(id,ct)??throw new KeyNotFoundException();
        // Resolve authoritative relationships when present, retaining legacy free-form data only for unlinked records.
        if(j.JobSiteId is Guid siteId){var site=await sites.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(siteId),ct);if(site is not null&&!site.IsDeleted&&site.PartitionKey==Partition){dto.JobSiteName=site.Name;dto.ProjectAddress=string.Join(", ",new[]{site.Address,site.City,site.State,site.Zip}.Where(x=>!string.IsNullOrWhiteSpace(x)));}}
        if(j.CustomerId!=Guid.Empty){var c=await customers.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(j.CustomerId),ct);if(c is not null&&!c.IsDeleted&&c.PartitionKey==Partition){dto.CustomerName=$"{c.FirstName} {c.LastName}".Trim();dto.ContactEmail=c.Email;dto.ContactPhone=c.Phone;}}
        var state=JobExecutionRules.State(j.Status);var x=p.Execution;
        if(x is not null&&dto.Execution is not null)foreach(var requirement in dto.Execution.Requirements)if(x.SupplyRequirementStatuses.TryGetValue(requirement.Id,out var supplyStatus))requirement.Status=$"Supply: {supplyStatus.Replace('_',' ') }";
        var allEvents=(await calendar.ListAsync(Partition,ct)).Where(e=>e.PartitionKey==Partition&&!e.IsDeleted).ToList();var conflicts=EventConflicts(allEvents,id);
        if(x is not null){ApplyCalendarSummary(j,allEvents);dto.ScheduledStart=j.ScheduledStart;dto.ScheduledEnd=j.ScheduledEnd;dto.AssignedTechnicianMembershipId=j.AssignedTechnicianMembershipId;}
        return new(dto,state,x?.Profile.StateLabels.GetValueOrDefault(state)??state.Replace('_',' '),JobExecutionRules.NextAction(state),
            x is null?[]:JobExecutionRules.Blockers(x,"schedule").Concat(conflicts).ToList(),x is null?[]:JobExecutionRules.Blockers(x,"start").Concat(conflicts).ToList(),x is null?[]:JobExecutionRules.Blockers(x,"complete"),
            JobExecutionRules.Next(state),allEvents.Where(e=>e.JobId==id).OrderBy(e=>e.StartUtc).Select(CalendarEventMapper.ToDto).ToList(),await authority.CanWriteAsync(ct));
    }
    public async Task<JobWorkspaceDto> CreateAsync(JobCreateDto input,CancellationToken ct)
    {
        await authority.RequireAsync(true,ct);var config=await configuration.GetAsync(user.TenantId,ct);
        if(!config.AllowManual)throw new ArgumentException("Tenant policy requires an accepted Estimate handoff.");
        if(input.Origin is not ("Manual" or "Warranty" or "Service" or "Imported" or "Internal" or "Other"))throw new ArgumentException("Choose a manual Job origin.");
        if(input.Origin=="Imported"&&(string.IsNullOrWhiteSpace(input.SourceSystem)||string.IsNullOrWhiteSpace(input.ExternalId)))throw new ArgumentException("Imported jobs require source system and external identity.");
        var customer=await customers.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(input.CustomerId),ct);
        var site=await sites.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(input.JobSiteId),ct);
        if(customer is null||customer.IsDeleted||customer.PartitionKey!=Partition||site is null||site.IsDeleted||site.PartitionKey!=Partition)throw new ArgumentException("Choose this tenant's customer and site.");
        if(string.IsNullOrWhiteSpace(input.Name)||string.IsNullOrWhiteSpace(input.Scope))throw new ArgumentException("Job name and scope are required.");
        var profile=JobConfigurationService.Profile(config,input.TradeProfile,input.ServiceType);
        var id=input.Origin=="Imported"?new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{user.TenantId}|{input.SourceSystem}|{input.ExternalId}"))[..16]):Guid.NewGuid();
        if(await jobs.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(id),ct) is not null)return await WorkspaceAsync(id,ct);
        var j=new Job{Id=id,PartitionKey=Partition,RowKey=RepositoryKeyHelper.ToRowKey(id),Name=Text(input.Name,300),Description=Text(input.Scope),CustomerId=customer.Id,CustomerName=$"{customer.FirstName} {customer.LastName}",JobSiteId=site.Id,JobSiteName=site.Name,TradeType=JobExecutionRules.Trade(input.TradeProfile),Status=JobStatus.Draft};
        var x=new JobExecutionDto{Origin=input.Origin,SourceSystem=input.SourceSystem,ExternalId=input.ExternalId,TradeProfile=input.TradeProfile,ServiceType=Text(input.ServiceType,100),SoldScope=Text(input.Scope),Profile=profile,Tasks=JobConfigurationService.Clone(profile.Tasks)};
        foreach(var t in x.Tasks)t.Id=Guid.NewGuid();
        await Save(j,new(){Execution=x},"job.created",$"{input.Origin} job created",ct);return await WorkspaceAsync(id,ct);
    }
    public async Task<JobWorkspaceDto> CommandAsync(Guid id,JobCommandDto input,CancellationToken ct=default)
    {
        var(j,p)=await Load(id,true,input.ExpectedVersion,ct);
        if(input.Action=="adopt"){
            if(p.Execution is null){var dto=await legacy.GetAsync(id,ct)??throw new KeyNotFoundException();p.Execution=JobConfigurationService.Initialize(dto,await configuration.GetAsync(user.TenantId,ct));await Save(j,p,"job.adopted","Legacy job adopted; source dates and sold scope retained",ct);}
            return await WorkspaceAsync(id,ct);
        }
        var x=p.Execution??throw new ArgumentException("Adopt the execution workflow first.");
        if(JobExecutionRules.State(j.Status) is "CLOSED" or "CANCELLED"&&input.Action!="transition")throw new ArgumentException("Reopen the job with a reason before changing it.");
        var now=DateTime.UtcNow;var audit=Text(input.Text);
        switch(input.Action){
            case "transition":
                var dto=JobMapper.ToDto(j);dto.Execution=x;
                var events=await calendar.ListAsync(Partition,ct);
                if(input.State is "SCHEDULED" or "READY_TO_START" or "IN_PROGRESS"){var collisions=EventConflicts(events,id);if(collisions.Count>0)throw new ArgumentException(string.Join("; ",collisions));}
                if(JobExecutionRules.State(j.Status) is "CLOSED" or "CANCELLED"&&!await authority.IsOwnerAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("An owner must reopen a closed or cancelled job.");
                JobExecutionRules.Transition(dto,input.State,input.Text,events.Any(e=>e.JobId==id&&!e.IsDeleted&&e.EventStatus=="scheduled"&&e.JobEventType!="delivery"));
                var prior=JobExecutionRules.State(j.Status);j.Status=dto.Status;
                if(j.Status==JobStatus.InProgress)j.ActualStart??=now;
                if(j.Status==JobStatus.Completed)j.ActualEnd=now;
                if(prior is "CLOSED" or "CANCELLED" || prior=="COMPLETED"&&input.State!="CLOSED")j.ActualEnd=null;
                audit=$"{prior} → {input.State}. {audit}";break;
            case "site":
                if(j.JobSiteId.HasValue)throw new ArgumentException("The primary site is already linked. Use a reviewed change for a new location.");
                var site=await sites.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(input.ItemId??Guid.Empty),ct);
                if(site is null||site.IsDeleted||site.PartitionKey!=Partition)throw new ArgumentException("Choose this tenant's authoritative site.");
                j.JobSiteId=site.Id;j.JobSiteName=site.Name;audit=$"Primary site linked: {site.Name}";break;
            case "trade-data":
                var fields=input.TradeData??[];
                if(fields.Any(f=>!x.Profile.Fields.ContainsKey(f.Key)||f.Value.Length>2000))throw new ArgumentException("Use fields declared by the Job trade profile.");
                x.TradeData=fields;audit="Trade execution details confirmed";break;
            case "priority":
                if(input.State is not ("low" or "normal" or "high" or "urgent"))throw new ArgumentException("Invalid priority.");
                x.Priority=input.State;audit=$"Priority: {input.State}";break;
            case "owner":await ValidateMembers([input.OwnerMembershipId??Guid.Empty],[],ct);x.OwnerMembershipId=input.OwnerMembershipId;audit=$"Responsible member: {input.OwnerMembershipId}";break;
            case "task":
                var task=x.Tasks.SingleOrDefault(t=>t.Id==input.ItemId)??throw new ArgumentException("Task not found.");
                ValidateEvidence(x,input.EvidenceIds);
                if(task.DependsOn.Any(dep=>!x.Tasks.Any(t=>t.Id==dep&&t.CompletedAtUtc.HasValue)))throw new ArgumentException("Complete prerequisite tasks first.");
                if(task.EvidenceRequired&&input.EvidenceIds.Count==0)throw new ArgumentException("This task requires evidence.");
                task.CompletedAtUtc=now;task.CompletedBy=Actor;task.Notes=Text(input.Text);task.EvidenceIds=input.EvidenceIds;audit=$"Completed: {task.Title}. {audit}";break;
            case "add-task":
                var t=input.Task??throw new ArgumentException("Task required.");
                if(string.IsNullOrWhiteSpace(t.Title)||t.Stage is not ("schedule" or "start" or "complete")||x.Tasks.Count>=150)throw new ArgumentException("Provide a task title and valid gate.");
                if(t.OwnerMembershipId.HasValue)await ValidateMembers([t.OwnerMembershipId.Value],[],ct);
                if(t.DependsOn.Any(dep=>!x.Tasks.Any(v=>v.Id==dep)))throw new ArgumentException("Unknown task dependency.");
                t.Id=Guid.NewGuid();t.CompletedAtUtc=null;t.CompletedBy=null;t.TemplateSource="job";t.Title=Text(t.Title,300);x.Tasks.Add(t);audit=$"Task created: {t.Title}";break;
            case "requirement":
                var r=input.Requirement??throw new ArgumentException("Requirement required.");
                if(input.ItemId.HasValue&&supplyReadiness is not null&&await supplyReadiness.IsManagedAsync(id,input.ItemId.Value,ct))throw new ArgumentException("This requirement is managed by Inventory. Record a new requirement for a scope change.");
                if(r.Kind is not ("material" or "equipment")||r.Quantity<=0||r.Quantity>1000000||string.IsNullOrWhiteSpace(r.Description)||r.Status is not ("Needed" or "Ordered" or "Confirmed" or "Available" or "Delivered" or "Consumed" or "Returned" or "Cancelled"))throw new ArgumentException("Invalid material/equipment requirement.");
                var old=x.Requirements.FindIndex(v=>v.Id==input.ItemId);r.Id=old<0?Guid.NewGuid():x.Requirements[old].Id;
                if(old<0)x.Requirements.Add(r);else x.Requirements[old]=r;audit=$"{r.Kind}: {Text(r.Description,300)} — {r.Status}";break;
            case "issue":
                var issue=input.Issue??throw new ArgumentException("Issue required.");
                if(string.IsNullOrWhiteSpace(issue.Description)||issue.Severity is not ("info" or "warning" or "blocker" or "critical"))throw new ArgumentException("Describe the issue and severity.");
                ValidateEvidence(x,issue.EvidenceIds);if(issue.OwnerMembershipId.HasValue)await ValidateMembers([issue.OwnerMembershipId.Value],[],ct);
                issue.Id=Guid.NewGuid();issue.CreatedAtUtc=now;issue.ResolvedAtUtc=null;issue.Resolution="";x.Issues.Add(issue);audit=$"Issue: {Text(issue.Description)}";break;
            case "resolve-issue":
                var i=x.Issues.SingleOrDefault(i=>i.Id==input.ItemId)??throw new ArgumentException("Issue not found.");
                if(string.IsNullOrWhiteSpace(input.Text))throw new ArgumentException("Describe the resolution.");i.ResolvedAtUtc=now;i.Resolution=Text(input.Text);audit=$"Resolved {i.Description}: {audit}";break;
            case "change":
                var change=input.Change??throw new ArgumentException("Change required.");
                if(string.IsNullOrWhiteSpace(change.Description))throw new ArgumentException("Describe the scope change.");ValidateEvidence(x,change.EvidenceIds);
                change.Id=Guid.NewGuid();change.CreatedAtUtc=now;change.RequestedBy=Actor;change.Status="DRAFT";change.AcceptedEstimateId=null;change.AcceptedRevision=null;change.AcceptedDocumentHash=null;change.CustomerApproval="";x.Changes.Add(change);audit=$"Change requested: {Text(change.Description)}";break;
            case "change-status":
                var c=x.Changes.SingleOrDefault(c=>c.Id==input.ItemId)??throw new ArgumentException("Change not found.");
                var allowed=c.Status switch {"DRAFT"=>new[]{"REVIEW","CANCELLED"},"REVIEW"=>["PRICING_REQUIRED","APPROVAL_REQUIRED","DECLINED","CANCELLED"],"PRICING_REQUIRED"=>["APPROVAL_REQUIRED","CANCELLED"],"APPROVAL_REQUIRED"=>["APPROVED","DECLINED","CANCELLED"],"APPROVED"=>["IMPLEMENTED","CANCELLED"],_=>Array.Empty<string>()};
                if(!allowed.Contains(input.State))throw new ArgumentException("Invalid change transition.");
                if(input.State=="APPROVED"){
                    if(c.PricingImpact){
                        var estimateId=input.Change?.AcceptedEstimateId??throw new ArgumentException("Link the customer-approved change Estimate.");
                        if(estimateId==p.AcceptedEstimate?.EstimateId)throw new ArgumentException("Use a separate change Estimate; original sold scope cannot authorize additions.");
                        var packet=await estimates.GetAsync(estimateId,ct)??throw new ArgumentException("Change Estimate not found.");
                        if(packet.Delivery?.Status!="approved"||packet.CustomerId!=j.CustomerId||packet.Document?.SiteId!=j.JobSiteId)throw new ArgumentException("The change Estimate must be accepted for this customer and site.");
                        c.AcceptedEstimateId=estimateId;c.AcceptedRevision=packet.RevisionNumber;c.AcceptedDocumentHash=packet.DocumentHash;c.CustomerApproval=$"Accepted Estimate revision {packet.RevisionNumber}";
                    }else{if(string.IsNullOrWhiteSpace(input.Text))throw new ArgumentException("Record customer approval evidence.");c.CustomerApproval=Text(input.Text);}
                }
                audit=$"Change {c.Id}: {c.Status} → {input.State}. {audit}";c.Status=input.State;break;
            case "accept":
                if(j.Status is not (JobStatus.CompletionReview or JobStatus.Completed))throw new ArgumentException("Review or complete work before acceptance.");
                var a=input.Acceptance??throw new ArgumentException("Acceptance required.");
                if(string.IsNullOrWhiteSpace(a.Signer)||string.IsNullOrWhiteSpace(a.Statement)||string.IsNullOrWhiteSpace(a.Signature))throw new ArgumentException("Signer, exact completion statement and confirmation are required.");
                if(!x.Profile.AllowQualifiedAcceptance&&!string.IsNullOrWhiteSpace(a.Exceptions))throw new ArgumentException("Tenant policy does not permit acceptance with exceptions.");
                a.CompletionHash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{x.SoldScope,x.TradeData,x.Tasks,x.Requirements,x.Changes,x.Issues,x.Evidence,x.CompletionRevision},JobConfigurationService.Json)));a.AcceptedState=JobExecutionRules.State(j.Status);a.Id=Guid.NewGuid();a.AcceptedAtUtc=now;a.CompletionRevision=x.CompletionRevision;a.RecordedBy=Actor;x.Acceptances.Add(a);audit=$"Completion acceptance recorded for {Text(a.Signer,200)}, revision {a.CompletionRevision}";break;
            case "activity":
                if(string.IsNullOrWhiteSpace(input.Text))throw new ArgumentException("Describe the work performed, progress or observation.");ValidateEvidence(x,input.EvidenceIds);audit=$"{audit}";break;
            default:throw new ArgumentException("Unknown Job action.");
        }
        if(x.Tasks.Count>150||x.Requirements.Count>300||x.Issues.Count>500||x.Changes.Count>500)throw new ArgumentException("Job record limit reached.");
        if(input.Action is "change" or "change-status" or "trade-data" or "add-task" or "task" or "requirement" or "issue" or "resolve-issue" && x.Acceptances.Any(a=>a.CompletionRevision==x.CompletionRevision))x.CompletionRevision++;
        await Save(j,p,$"job.{input.Action}",audit,ct);return await WorkspaceAsync(id,ct);
    }
    private static string Text(string? text,int max=4000)=>string.IsNullOrWhiteSpace(text)?"":text.Trim()[..Math.Min(text.Trim().Length,max)];
    private static void ValidateEvidence(JobExecutionDto x,List<Guid> ids){if(ids.Any(id=>!x.Evidence.Any(e=>e.Id==id)))throw new ArgumentException("Evidence must belong to this Job.");}
    private async Task ValidateMembers(List<Guid> ids,List<string> skills,CancellationToken ct)
    {
        var people=await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct);var config=await configuration.GetAsync(user.TenantId,ct);
        if(ids.Any(id=>!people.Any(m=>m.Id==id&&m.TenantId==user.TenantId&&!m.IsDeleted&&!m.DateRemoved.HasValue&&m.Role!="contact")))throw new ArgumentException("Choose active members of this tenant.");
        var available=ids.SelectMany(id=>config.MemberSkills.GetValueOrDefault(id)??[]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(skills.Any(s=>!available.Contains(s)))throw new ArgumentException("Assigned crew does not cover required skills: "+string.Join(", ",skills.Where(s=>!available.Contains(s))));
    }
    public async Task<JobWorkspaceDto> ScheduleAsync(Guid id,JobEventInputDto input,CancellationToken ct=default)
    {
        await authority.RequireCalendarAsync(true,ct);
        var(j,p)=await Load(id,true,input.ExpectedVersion,ct);var x=p.Execution??throw new ArgumentException("Adopt execution first.");
        if(j.Status is JobStatus.Completed or JobStatus.Closed or JobStatus.Cancelled)throw new ArgumentException("Reopen before scheduling.");
        if(input.StartUtc.Kind!=DateTimeKind.Utc||input.EndUtc.Kind!=DateTimeKind.Utc||input.EndUtc<=input.StartUtc)throw new ArgumentException("Provide a valid UTC schedule window.");
        if(input.Status is not ("scheduled" or "completed" or "cancelled"))throw new ArgumentException("Invalid event status.");
        if(string.IsNullOrWhiteSpace(input.Title)||input.MembershipIds.Count==0&&input.Type!="delivery")throw new ArgumentException("Event title and assigned people are required.");
        var blockers=JobExecutionRules.Blockers(x,"schedule");if(input.Status=="scheduled"&&blockers.Count>0)throw new ArgumentException(string.Join("; ",blockers));
        if(input.Status=="scheduled")await ValidateMembers(input.MembershipIds,input.Type=="delivery"?[]:x.Profile.RequiredSkills,ct);
        var all=await calendar.ListAsync(Partition,ct);
        var existing=input.Id==Guid.Empty?null:all.SingleOrDefault(e=>e.Id==input.Id&&e.JobId==id);
        if(input.Id!=Guid.Empty&&existing is null)throw new ArgumentException("Job event not found.");
        var eid=existing?.Id??Guid.NewGuid();
        if(input.Status=="scheduled"){
            if(all.Any(e=>e.Id!=eid&&!e.IsDeleted&&e.EventStatus=="scheduled"&&e.StartUtc<input.EndUtc&&e.EndUtc>input.StartUtc&&
                (e.MembershipIds.Intersect(input.MembershipIds).Any()||e.AssignedTechnicianMembershipId.HasValue&&input.MembershipIds.Contains(e.AssignedTechnicianMembershipId.Value)||e.ResourceIds.Intersect(input.ResourceIds,StringComparer.OrdinalIgnoreCase).Any())))throw new ArgumentException("People or equipment already have an overlapping calendar assignment.");
            if((await jobs.ListAsync(Partition,ct)).Any(other=>other.Id!=id&&other.Status is JobStatus.Scheduled or JobStatus.InProgress&&other.ScheduledStart<input.EndUtc&&other.ScheduledEnd>input.StartUtc&&other.AssignedTechnicianMembershipId.HasValue&&input.MembershipIds.Contains(other.AssignedTechnicianMembershipId.Value)))throw new ArgumentException("Technician has an overlapping legacy job assignment.");
        }
        var e=existing??new CalendarEvent{Id=eid,PartitionKey=Partition,RowKey=RepositoryKeyHelper.ToRowKey(eid),JobId=id,JobName=j.Name,JobSiteId=j.JobSiteId,JobSiteName=j.JobSiteName,EventType=CalendarEventType.Other};
        e.Title=Text(input.Title,300);e.JobEventType=Text(input.Type,80);e.StartUtc=input.StartUtc;e.EndUtc=input.EndUtc;e.MembershipIds=input.MembershipIds.Distinct().ToList();e.AssignedTechnicianMembershipId=e.MembershipIds.Count==0?null:e.MembershipIds.First();e.EventType=input.Type=="delivery"?CalendarEventType.Delivery:CalendarEventType.Other;e.ResourceIds=input.ResourceIds.Distinct().ToList();e.CustomerVisible=input.CustomerVisible;e.EventStatus=input.Status;e.Description=Text(input.Notes);e.DateUpdated=DateTime.UtcNow;
        // Calendar is authoritative. Record intent first so a transport/storage failure never looks like a confirmed schedule.
        await Save(j,p,"job.schedule-requested",$"Event {eid}: {e.Title}, {e.StartUtc:O} to {e.EndUtc:O}; {e.EventStatus}. Awaiting Calendar confirmation.",ct);
        await calendar.SaveAsync(e,ct);
        var latest=await Load(id,false,null,ct);j=latest.Item1;p=latest.Item2;
        ApplyCalendarSummary(j,await calendar.ListAsync(Partition,ct));
        await Save(j,p,"job.scheduled",$"Calendar confirmed event {eid}: {e.Title} ({e.EventStatus})",ct);
        return await WorkspaceAsync(id,ct);
    }
    public async Task<object> RecommendAsync(Guid id,DateTime start,DateTime end,CancellationToken ct)
    {
        await authority.RequireCalendarAsync(false,ct);
        var(_,p)=await Load(id,false,null,ct);if(end<=start)throw new ArgumentException("Choose a valid window.");
        var config=await configuration.GetAsync(user.TenantId,ct);var all=await calendar.ListAsync(Partition,ct);
        var required=p.Execution?.Profile.RequiredSkills??[];var legacyJobs=await jobs.ListAsync(Partition,ct);
        return (await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct)).Where(m=>m.TenantId==user.TenantId&&!m.IsDeleted&&!m.DateRemoved.HasValue&&m.Role!="contact").Select(m=>{
            var skills=config.MemberSkills.GetValueOrDefault(m.Id)??[];
            var missing=required.Except(skills,StringComparer.OrdinalIgnoreCase).ToArray();
            var occupied=legacyJobs.Count(j=>j.Id!=id&&j.AssignedTechnicianMembershipId==m.Id&&j.Status is JobStatus.Scheduled or JobStatus.InProgress&&j.ScheduledStart<end&&j.ScheduledEnd>start)+all.Count(e=>e.EventStatus=="scheduled"&&!e.IsDeleted&&(e.MembershipIds.Contains(m.Id)||e.AssignedTechnicianMembershipId==m.Id)&&e.StartUtc<end&&e.EndUtc>start);
            return new {membershipId=m.Id,skills,missingSkills=missing,available=occupied==0,eligible=occupied==0&&missing.Length==0,explanation=occupied>0?"Overlapping Calendar assignment":missing.Length>0?"Missing confirmed skills: "+string.Join(", ",missing):required.Count==0?"No required skills configured; availability only. Confirm suitability before assigning.":"Confirmed skills cover this job; no Calendar conflict in selected window"};
        }).OrderByDescending(m=>m.eligible).ToArray();
    }
    public async Task<JobWorkspaceDto> UploadAsync(Guid id,string version,IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,CancellationToken ct,string purpose="field")
    {
        var(j,p)=await Load(id,true,version,ct);var x=p.Execution??throw new ArgumentException("Adopt execution first.");
        if(j.Status is JobStatus.Closed or JobStatus.Cancelled)throw new ArgumentException("Reopen before adding evidence.");
        if(purpose is not ("field" or "completion" or "planning"))throw new ArgumentException("Choose evidence purpose.");
        if(uploads.Count==0||uploads.Count>10||x.Evidence.Count+uploads.Count>200)throw new ArgumentException("Choose 1–10 evidence files (200 per job).");
        foreach(var upload in uploads){
            if(upload.Length<=0||upload.Length>10*1024*1024||upload.ContentType is not ("image/jpeg" or "image/png" or "image/webp" or "application/pdf"))throw new ArgumentException("Evidence must be a JPEG, PNG, WebP or PDF up to 10 MB.");
            using var bytes=new MemoryStream();await upload.Content.CopyToAsync(bytes,ct);if(bytes.Length>10*1024*1024)throw new ArgumentException("File is too large.");
            var evidence=new JobEvidenceDto{Purpose=purpose,Name=Path.GetFileName(upload.FileName),ContentType=upload.ContentType,Sha256=Convert.ToHexString(SHA256.HashData(bytes.ToArray())),CreatedAtUtc=DateTime.UtcNow,Actor=Actor};
            evidence.BlobName=$"{user.TenantId:N}/{id:N}/evidence/{evidence.Id:N}";bytes.Position=0;
            await blobs.UploadAsync("job-workflows",evidence.BlobName,bytes,evidence.ContentType,new Dictionary<string,string>{{"jobId",id.ToString("N")}},ct);x.Evidence.Add(evidence);
        }
        if(x.Acceptances.Any(a=>a.CompletionRevision==x.CompletionRevision))x.CompletionRevision++;
        await Save(j,p,"job.evidence",$"Added {uploads.Count} evidence files",ct);return await WorkspaceAsync(id,ct);
    }
    public async Task<QuoteRequestAttachmentDownload> DownloadAsync(Guid id,Guid file,CancellationToken ct)
    {
        var(_,p)=await Load(id,false,null,ct);var e=p.Execution?.Evidence.SingleOrDefault(e=>e.Id==file);
        if(e is null){var source=p.AcceptedEstimate?.Document?.Attachments.SingleOrDefault(a=>a.Id==file)??throw new KeyNotFoundException("Evidence not found.");
            if(string.IsNullOrWhiteSpace(source.ContentHash)||!source.BlobName.StartsWith($"{user.TenantId:N}/{p.AcceptedEstimate!.EstimateId:N}/attachments/",StringComparison.Ordinal))throw new ArgumentException("This legacy attachment remains available from its source Estimate.");
            return new(source.Name,source.ContentType,source.SizeBytes,await blobs.OpenReadAsync(QuoteEstimateService.ContainerName,source.BlobName,ct));}
        var stream=await blobs.OpenReadAsync("job-workflows",e.BlobName,ct);return new(e.Name,e.ContentType,stream.CanSeek?stream.Length:0,stream);
    }
    public async Task<object> MetricsAsync(CancellationToken ct)
    {
        await authority.RequireAsync(false,ct);var all=await jobs.ListAsync(Partition,ct);var rows=new List<object>();
        foreach(var j in all){var p=await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct);rows.Add(new{j.Id,state=JobExecutionRules.State(j.Status),trade=p.Execution?.TradeProfile??JobExecutionRules.Trade(j.TradeType),j.DateCreated,j.ScheduledStart,j.ActualStart,j.ActualEnd,
            startToCompleteDays=j.ActualStart.HasValue&&j.ActualEnd.HasValue?(double?)(j.ActualEnd.Value-j.ActualStart.Value).TotalDays:null,
            openBlockers=p.Execution?.Issues.Count(i=>i.ResolvedAtUtc is null),changes=p.Execution?.Changes.Count,acceptanceAt=p.Execution?.Acceptances.LastOrDefault()?.AcceptedAtUtc});}
        return new{generatedAtUtc=DateTime.UtcNow,jobs=rows};
    }
}
