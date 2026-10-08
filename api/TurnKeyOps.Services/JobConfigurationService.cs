using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
namespace TurnKeyOps.Services;
public sealed class JobConfigurationService(ITenantSettingsRepository settings)
{
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public async Task<JobConfigurationDto> GetAsync(Guid tenant,CancellationToken ct=default)
    {
        var row=await settings.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(tenant),"SETTINGS|OPERATIONAL",ct);
        using var data=JsonDocument.Parse(row?.ValuesJson??"{}");
        var config=data.RootElement.TryGetProperty("jobs",out var value)?value.Deserialize<JobConfigurationDto>(Json)??new():new JobConfigurationDto();
        Validate(config);return config;
    }
    public async Task<TenantSettingsDocumentDto> UpdateAsync(Guid tenant,JobConfigurationDto policy,string version,TurnKeyOps.Services.Interfaces.ITenantSettingsService service,CancellationToken ct)
    {
        var row=await settings.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(tenant),"SETTINGS|OPERATIONAL",ct);
        var values=System.Text.Json.Nodes.JsonNode.Parse(row?.ValuesJson??"{}")!.AsObject();values["jobs"]=JsonSerializer.SerializeToNode(policy,Json);
        return await service.UpsertAsync("operational",new(){ExpectedVersion=version,SchemaVersion=row?.SchemaVersion??1,Values=JsonSerializer.SerializeToElement(values),SecretReferences=JsonSerializer.Deserialize<Dictionary<string,string>>(row?.SecretReferencesJson??"{}")??[]},ct);
    }
    public static void Validate(JobConfigurationDto c)
    {
        if(c.EnabledTrades.Length is 0 or >30||c.EnabledTrades.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Choose the enabled Job trade profiles.");
        if(c.AiActions.Any(a=>!a.Key.StartsWith("job.")||a.Value is not ("disabled" or "read" or "recommend" or "draft" or "approval" or "auto")))throw new ArgumentException("Invalid Job action policy.");
        foreach(var p in c.Profiles.Values){
            if(p.Tasks.Count>100||p.Fields.Count>60||p.CompletionPhotos is <0 or >30)throw new ArgumentException("Job profile exceeds supported limits.");
            if(p.RequiredFields.Any(k=>!p.Fields.ContainsKey(k))||p.Tasks.Any(t=>string.IsNullOrWhiteSpace(t.Title)||t.Stage is not ("schedule" or "start" or "complete")))throw new ArgumentException("Invalid profile fields or task stages.");
            if(p.StateLabels.Any(x=>!JobExecutionRules.States.ContainsKey(x.Key)||string.IsNullOrWhiteSpace(x.Value)))throw new ArgumentException("Labels must use canonical Job states.");
        }
    }
    public static JobProfileDto Profile(JobConfigurationDto config,string trade,string service="")
    {
        if(!config.EnabledTrades.Contains(trade))throw new ArgumentException("This trade is not enabled for Jobs.");
        if(config.Profiles.TryGetValue($"{trade}/{service}",out var typed)||config.Profiles.TryGetValue(trade,out typed))return Clone(typed);
        var p=new JobProfileDto();
        p.Tasks.Add(new(){Title="Confirm customer and site access",Stage="schedule",TemplateSource=trade});
        void Task(string title,string stage="complete")=>p.Tasks.Add(new(){Title=title,Stage=stage,TemplateSource=trade});
        void Field(string key,string label)=>p.Fields[key]=label;
        switch(trade){
            case "concrete":
                Field("mix","Mix / specification");Field("dimensions","Area / depth / volume");Field("reinforcement","Reinforcement / forms");Field("finish","Finish and cure requirements");
                Task("Confirm delivery, equipment and weather","start");Task("Verify base, forms and reinforcement","start");Task("Place and finish concrete");Task("Cut joints, cure and clean up");break;
            case "framing":
                Field("structure","Floor / wall / roof scope");Field("plans","Plans and rough openings");Field("package","Lumber, hardware and lifting equipment");
                Task("Confirm plans and material package","start");Task("Complete layout and framing");Task("Complete required inspections");break;
            case "land-clearing":
                Field("acreage","Acreage and vegetation");Field("terrain","Terrain and access");Field("disposal","Hauling / disposal strategy");Field("restrictions","Environmental / site restrictions");
                Task("Confirm access, restrictions and ground conditions","start");Task("Confirm equipment and disposal","start");Task("Clear, haul and restore site");break;
            case "doors-locks":
                Field("openings","Opening measurements and handing");Field("hardware","Door / frame / hardware / keying");Field("access","Customer access contact");
                Task("Verify opening and product availability","start");Task("Protect area and install approved scope");Task("Test door and hardware function");break;
        }
        return p;
    }
    public static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Json),Json)!;
    public static JobExecutionDto Initialize(JobDto job,JobConfigurationDto config)
    {
        var trade=job.AcceptedEstimate?.Document?.TradeProfile??JobExecutionRules.Trade(job.TradeType);
        var profile=Profile(config,trade);
        var x=new JobExecutionDto{Origin=job.AcceptedEstimate is null?"Imported":"Won Estimate",TradeProfile=trade,
            SourceSystem=job.AcceptedEstimate is null?"legacy-turnkey":null,SoldScope=job.AcceptedEstimate?.Document?.Scope??job.Description??"",Profile=profile};
        x.Tasks=Clone(profile.Tasks);foreach(var task in x.Tasks)task.Id=Guid.NewGuid();
        if(job.RequiredDepositPercent>0)x.Tasks.Add(new(){Title="Confirm deposit / release requirement satisfied",Stage="schedule",TemplateSource="sold-terms",EvidenceRequired=true});
        foreach(var line in job.AcceptedEstimate?.SelectedOptions.SelectMany(o=>o.Lines)??[])
            if(line.Kind is "material" or "equipment")x.Requirements.Add(new(){Kind=line.Kind,Description=line.Name,CatalogItemId=line.CatalogId,Quantity=line.Quantity,Unit=line.Unit,Source=$"Accepted Estimate revision {job.AcceptedEstimate!.Revision}",Status="Needed"});
        return x;
    }
}
