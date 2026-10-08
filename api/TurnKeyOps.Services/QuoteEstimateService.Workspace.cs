using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed partial class QuoteEstimateService
{
    public async Task<EstimatePricingPolicyDto> PricingPolicyAsync(CancellationToken ct = default)
    {
        var settings = _settings is null ? null : await _settings.GetAsync(Partition(_userContext.TenantId), "SETTINGS|OPERATIONAL", ct);
        using var root = JsonDocument.Parse(settings?.ValuesJson ?? "{}");
        return root.RootElement.TryGetProperty("estimates",out var value) ? value.Deserialize<EstimatePricingPolicyDto>(JsonOptions) ?? new() : new();
    }
    public async Task<TenantSettingsDocumentDto> UpdatePricingPolicyAsync(EstimatePricingPolicyDto policy,string version,TurnKeyOps.Services.Interfaces.ITenantSettingsService service,CancellationToken ct)
    {
        await RequireWorkspaceAsync(true,ct);
        if(!await _authority!.CanApproveAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("Owner access required.");
        EstimatePricingEngine.Validate(policy);
        var prior=await _settings!.GetAsync(Partition(_userContext.TenantId),"SETTINGS|OPERATIONAL",ct);
        var values=System.Text.Json.Nodes.JsonNode.Parse(prior?.ValuesJson??"{}")!.AsObject();values["estimates"]=JsonSerializer.SerializeToNode(policy,JsonOptions);
        return await service.UpsertAsync("operational",new(){ExpectedVersion=version,SchemaVersion=prior?.SchemaVersion??1,Values=JsonSerializer.SerializeToElement(values),SecretReferences=JsonSerializer.Deserialize<Dictionary<string,string>>(prior?.SecretReferencesJson??"{}")??[]},ct);
    }
    public async Task<QuoteEstimateDto> IssueAsync(Guid id,string version,CancellationToken ct=default)
    {
        await RequireWorkspaceAsync(true,ct);
        var slug=_tenantOptions?.Value.Tenants.FirstOrDefault(x=>x.Value.TenantId==_userContext.TenantId).Key;
        if(string.IsNullOrWhiteSpace(slug))throw new ArgumentException("No customer proposal surface is configured for this tenant.");
        return await SendAsync(id,version,$"/{Uri.EscapeDataString(slug)}/estimate/{id:D}",ct);
    }
    public async Task ReconcileDecisionAsync(Guid id,CancellationToken ct=default)
    {
        await RequireWorkspaceAsync(true,ct);var packet=await GetAsync(id,ct)??throw new KeyNotFoundException();
        if(packet.ApprovalSignature is not null)
        {
            var quote=await GetQuoteAsync(_userContext.TenantId,id,ct);
            if(quote is not null && QuoteRequestMapper.ToDto(quote).Status!="won")await UpdateQuoteAsync(quote,"won","Customer signed proposal.","Customer approval synchronized",ct);
            else if(_leadEvents is not null)await _leadEvents.FromEstimateAsync(_userContext.TenantId,id,"won","Signed proposal synchronized",ct);
        }
    }
    private async Task<EstimateDocumentDto> DocumentFromLeadAsync(LeadDto lead, QuoteRequest quote, CancellationToken ct)
    {
        var policy=await PricingPolicyAsync(ct);
        return new(){ TradeProfile=lead.TradeProfile,OwnerMembershipId=lead.OwnerMembershipId,Scope=lead.RequestedWork,
            Source=lead.Source,Referral=lead.ReferralName,LeadContext=new(lead.Qualification),Terms=policy.Terms,DepositPercent=policy.DepositPercent,
            CompanyName=policy.CompanyName,Timing=quote.RequestedTimeline,
            Attachments=QuoteRequestMapper.ToDto(quote).Attachments.Select(x=>new EstimateAttachmentDto{Id=x.Id,Name=x.FileName}).ToList(),
            Suggestions=Extract(lead.RequestedWork+"\n"+string.Join("\n",lead.Qualification.Select(x=>$"{x.Key}: {x.Value}")),lead.TradeProfile,"Lead qualification; not verified") };
    }
    private async Task RequireWorkspaceAsync(bool write,CancellationToken ct)
    { if(_authority is null) throw new InvalidOperationException("Estimate authority is unavailable."); await _authority.RequireAsync(write,ct); }
    public async Task<EstimateWorkspaceDto> WorkspaceAsync(Guid id,CancellationToken ct=default)
    {
        await RequireWorkspaceAsync(false,ct);
        var packet=await GetAsync(id,ct)??throw new KeyNotFoundException("Estimate not found.");
        var policy=await PricingPolicyAsync(ct);
        var owner=await _authority!.CanApproveAsync(ct);
        var state=State(packet);
        var catalog=policy.Catalog.Where(x=>x.Enabled&&!x.Sample&&(x.TradeProfile=="general"||x.TradeProfile==packet.Document?.TradeProfile)).ToList();
        if(!owner){packet=CustomerProjection(packet,false);catalog=catalog.Select(x=>new EstimateCatalogItemDto{Id=x.Id,Name=x.Name,Kind=x.Kind,Unit=x.Unit,TradeProfile=x.TradeProfile,Enabled=x.Enabled,Taxable=x.Taxable,UnitPrice=x.UnitPrice}).ToList();}
        return new(){Packet=packet,Fields=EstimatePricingEngine.Fields(packet.Document?.TradeProfile??"general",policy),Catalog=catalog,State=state,StateLabel=policy.StageLabels.GetValueOrDefault(state,state.ToLowerInvariant().Replace('_',' ')),
            CanWrite=await _authority.CanWriteAsync(ct),CanApprove=owner,CanViewCosts=owner,
            NextAction=state switch {"ACCEPTED"=>"Hand accepted scope to Job","SENT" or "VIEWED"=>"Follow up with the customer","REVISION_REQUESTED"=>"Create a revision","NEEDS_APPROVAL"=>"Request office pricing approval","READY_TO_SEND"=>"Preview and issue proposal",_=>"Confirm scope and price the estimate"}};
    }
    public static string State(QuoteEstimateDto p)=>p.Outcome??(p.Delivery?.Status=="approved"?"ACCEPTED":p.ExpiresAtUtc<DateTime.UtcNow&&p.Delivery is not null?"EXPIRED":p.Delivery?.Status=="changes-requested"?"REVISION_REQUESTED":p.Delivery is not null?p.ViewedAtUtc.HasValue?"VIEWED":"SENT":p.Pricing?.Blockers.Count>0?"NEEDS_INFORMATION":p.Pricing is null?"DRAFT":p.Pricing.ApprovalReasons.Count>0&&p.Pricing.ApprovedAtUtc is null?"NEEDS_APPROVAL":"READY_TO_SEND");
    private async Task<(QuoteEstimate Entity,QuoteEstimateDto Packet)> EditablePacketAsync(Guid id,string version,CancellationToken ct)
    {
        await RequireWorkspaceAsync(true,ct);await RequireQuoteAccessAsync(id,ct);
        var entity=await GetEntityAsync(_userContext.TenantId,id,ct)??throw new KeyNotFoundException("Estimate not found.");
        ValidateVersion(entity,version);var packet=await LoadPayloadAsync(entity,ct);
        if(packet.Document is null)throw new ArgumentException("This historical packet uses the existing trade editor.");
        return (entity,packet);
    }
    public async Task<QuoteEstimateDto> PriceWorkspaceAsync(Guid id,EstimateWorkspaceInputDto input,CancellationToken ct=default)
    {
        var (entity,packet)=await EditablePacketAsync(id,input.ExpectedVersion,ct);
        if(packet.Delivery is not null||packet.ApprovalSignature is not null||packet.Outcome is not null)throw new ArgumentException("Create a new revision before changing an issued estimate.");
        var prior=packet.Document!;var doc=input.Document;
        if(doc.TradeProfile!=prior.TradeProfile)throw new ArgumentException("Trade comes from the linked Lead.");
        // These references are authored from upstream records, never supplied by the price editor.
        doc.LeadContext=prior.LeadContext;doc.Source=prior.Source;doc.Referral=prior.Referral;doc.OwnerMembershipId=prior.OwnerMembershipId;
        doc.SiteId=prior.SiteId;doc.Attachments=prior.Attachments;doc.CompanyName=prior.CompanyName;doc.Suggestions=prior.Suggestions;
        foreach(var item in doc.Inputs.Values)item.Provenance="Confirmed by "+Actor();
        doc.NarrativeSource="Estimator reviewed scope";
        var policy=await PricingPolicyAsync(ct);
        var pricing=EstimatePricingEngine.Calculate(doc,policy,packet.CustomerId,input.DiscountPercent);
        doc.CompanyName=policy.CompanyName;
        packet.Document=doc;packet.Pricing=pricing;packet.ServiceSummary=doc.Scope;packet.ScopeLineItems=[doc.Scope];
        packet.Totals=new(){EstimatedTotal=pricing.BaseTotal};packet.Status=pricing.Blockers.Count==0&&pricing.ApprovalReasons.Count==0?"ready-to-send":"draft";
        packet.SavedAtUtc=DateTime.UtcNow;packet.CommercialSummary=$"{doc.TradeProfile} · {pricing.Options.Count} option(s)";
        AddEstimateEvent(packet,"priced",pricing.Blockers.Count>0?"Draft saved; confirmed information is still required.":"Authoritative pricing calculated from tenant catalog and confirmed inputs.");
        return await PersistAsync(entity,packet,_userContext.TenantId,null,null,ct);
    }
    public async Task<QuoteEstimateDto> UploadScopeFilesAsync(Guid id,string version,IReadOnlyCollection<TurnKeyOps.Services.Interfaces.QuoteRequestAttachmentUpload> uploads,TurnKeyOps.Services.Interfaces.IQuoteRequestAttachmentService files,CancellationToken ct)
    {
        var(entity,packet)=await EditablePacketAsync(id,version,ct);
        if(packet.Delivery is not null||packet.Outcome is not null)throw new ArgumentException("Create a new revision before adding files.");
        var attachments=await files.UploadAsync(id,uploads,ct)??throw new KeyNotFoundException("Source intake unavailable.");
        foreach(var file in attachments)
            if(packet.Document!.Attachments.All(x=>x.Id!=file.Id))packet.Document.Attachments.Add(new(){Id=file.Id,Name=file.FileName});
        packet.Pricing=null;packet.Status="draft";AddEstimateEvent(packet,"files","Source photos/files added; review and reprice before issue.");
        return await PersistAsync(entity,packet,_userContext.TenantId,null,null,ct);
    }
    public async Task<QuoteEstimateDto> ApproveWorkspaceAsync(Guid id,string version,CancellationToken ct=default)
    {
        var(entity,packet)=await EditablePacketAsync(id,version,ct);
        if(!await _authority!.CanApproveAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("An owner must approve pricing exceptions.");
        if(packet.Delivery is not null||packet.Pricing is null||packet.Pricing.Blockers.Count>0)throw new ArgumentException("Complete pricing before office approval.");
        if(packet.Pricing.RuleVersion!=EstimatePricingEngine.Version(await PricingPolicyAsync(ct)))throw new ArgumentException("Pricing rules changed; recalculate first.");
        packet.Pricing.ApprovedAtUtc=DateTime.UtcNow;packet.Pricing.ApprovedBy=Actor();packet.Status="ready-to-send";
        AddEstimateEvent(packet,"office-approved","Owner approved this exact pricing snapshot and its exceptions.");
        return await PersistAsync(entity,packet,_userContext.TenantId,null,null,ct);
    }
    public async Task<QuoteEstimateDto> StructureAsync(Guid id,EstimateTaskDto input,CancellationToken ct=default)
    {
        var(entity,packet)=await EditablePacketAsync(id,input.ExpectedVersion,ct);
        if(packet.Delivery is not null)throw new ArgumentException("Create a revision before drafting new scope.");
        if(string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>8000)throw new ArgumentException("Provide notes up to 8000 characters.");
        packet.Document!.Suggestions=Extract(input.Text,packet.Document.TradeProfile,"Bob extraction from estimator notes; confirmation required");
        packet.Document.NarrativeSource="Bob structured the supplied notes; estimator confirmation required";
        // Suggestions never populate confirmed inputs or change authoritative totals.
        packet.Pricing=null;packet.Status="draft";
        AddEstimateEvent(packet,"bob-draft","Bob extracted candidate inputs. Confirm each value before pricing.");
        return await PersistAsync(entity,packet,_userContext.TenantId,null,null,ct);
    }
    public static List<EstimateSuggestionDto> Extract(string text,string trade,string provenance)
    {
        var suggestions=new List<EstimateSuggestionDto>();
        var pair=Regex.Match(text,@"(?<a>\d+(?:\.\d+)?)\s*(?:ft|feet)?\s*(?:by|x|×)\s*(?<b>\d+(?:\.\d+)?)",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
        if(pair.Success&&(trade is "concrete" or "framing")&&decimal.TryParse(pair.Groups["a"].Value,NumberStyles.Number,CultureInfo.InvariantCulture,out var a)&&decimal.TryParse(pair.Groups["b"].Value,NumberStyles.Number,CultureInfo.InvariantCulture,out var b))
        {
            suggestions.Add(new(){Key="length",Value=a,Text=pair.Value,Provenance=provenance});
            suggestions.Add(new(){Key=trade=="concrete"?"width":"height",Value=b,Text=pair.Value,Provenance=provenance});
        }
        foreach(var(key,pattern)in new[]{("depth",@"(\d+(?:\.\d+)?)\s*[- ]?(?:inch|in\b|""\s*concrete)"),("acreage",@"(\d+(?:\.\d+)?)\s*acres?"),("openingCount",@"(\d+)\s*(?:doors|openings)"),("laborHours",@"(\d+(?:\.\d+)?)\s*(?:labor )?hours?")})
        {
            var match=Regex.Match(text,pattern,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
            if(match.Success&&decimal.TryParse(match.Groups[1].Value,NumberStyles.Number,CultureInfo.InvariantCulture,out var number))suggestions.Add(new(){Key=key,Value=number,Text=match.Value,Provenance=provenance});
        }
        suggestions.Add(new(){Key="scope",Text=text.Length>2000?text[..2000]:text,Provenance=provenance});return suggestions;
    }
    private void AddEstimateEvent(QuoteEstimateDto packet,string type,string text)=>packet.Events.Add(new(){Type=type,Text=text,Actor=Actor(),Revision=packet.RevisionNumber});
    public static QuoteEstimateDto CustomerProjection(QuoteEstimateDto packet,bool publicView=true)
    {
        var copy=JsonSerializer.Deserialize<QuoteEstimateDto>(JsonSerializer.Serialize(packet,JsonOptions),JsonOptions)!;
        copy.Totals.MaterialCost=0;copy.Totals.LaborCost=0;
        foreach(var location in copy.Locations){location.MaterialCost=0;location.LaborCost=0;}
        if(copy.Pricing is not null)foreach(var option in copy.Pricing.Options){option.Cost=null;option.MarginPercent=null;foreach(var line in option.Lines)line.Cost=null;}
        if(publicView)
        {
            copy.Notes="";copy.VisitFindings="";copy.Assumptions=[];copy.RevisionHistory=[];copy.Events=[];copy.Version="";
            if(copy.Document is not null){copy.Document.LeadContext=[];copy.Document.Source="";copy.Document.Referral="";copy.Document.OwnerMembershipId=null;copy.Document.Suggestions=[];copy.Document.NarrativeSource="";copy.Document.Inputs=copy.Document.Inputs.Where(x=>x.Value.Confirmed).ToDictionary(x=>x.Key,x=>new EstimateInputValueDto{Value=x.Value.Value,Confirmed=true,Provenance="Confirmed measurement"});foreach(var option in copy.Document.Options)option.Items=[];foreach(var file in copy.Document.Attachments)file.BlobName="";}
            if(copy.Pricing is not null){copy.Pricing.ApprovalReasons=[];copy.Pricing.ApprovedBy=null;foreach(var option in copy.Pricing.Options)foreach(var line in option.Lines)line.Rule="";}
        }
        else copy.RevisionHistory=copy.RevisionHistory.Select(x=>{if(x.Pricing is not null)foreach(var option in x.Pricing.Options){option.Cost=null;option.MarginPercent=null;foreach(var line in option.Lines)line.Cost=null;}x.Totals.MaterialCost=0;x.Totals.LaborCost=0;foreach(var loc in x.Locations){loc.MaterialCost=0;loc.LaborCost=0;}return x;}).ToList();
        return copy;
    }
}
