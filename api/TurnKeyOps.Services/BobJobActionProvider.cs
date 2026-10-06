using System.Text.Json;
using MedInsights.Lib.Authorization;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class BobJobActionProvider(JobExecutionService service,JobDeliveryService delivery,string tool):IBobActionProvider
{
    public string ToolKey=>tool;
    public string PermissionKey=>tool is "job.summarize" or "job.readiness" or "job.recommend" or "job.structure"?TurnKeyPermissionKeys.JobsRead:TurnKeyPermissionKeys.JobsWrite;
    public BobActionRisk Risk=>PermissionKey==TurnKeyPermissionKeys.JobsRead?BobActionRisk.Read:BobActionRisk.Destructive;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
    {
        var id=input.GetProperty("jobId").GetGuid();var workspace=await service.WorkspaceAsync(id,ct);
        if(tool=="job.structure"){
            var note=input.TryGetProperty("text",out var text)?text.GetString()??"":"";
            if(string.IsNullOrWhiteSpace(note)||note.Length>4000)throw new ArgumentException("Provide a field note up to 4000 characters.");
            var lines=note.Split(['\n',';'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            return new {observation=note,workPerformed=lines.Where(l=>l.StartsWith("work:",StringComparison.OrdinalIgnoreCase)).Select(l=>l[5..].Trim()),
                possibleIssues=lines.Where(l=>l.StartsWith("issue:",StringComparison.OrdinalIgnoreCase)||l.Contains("blocked",StringComparison.OrdinalIgnoreCase)||l.Contains("soft",StringComparison.OrdinalIgnoreCase)),
                possibleRequirements=lines.Where(l=>l.StartsWith("material:",StringComparison.OrdinalIgnoreCase)||l.Contains("need ",StringComparison.OrdinalIgnoreCase)),
                nextAction="Review suggestions, record the field update and create an Issue or material requirement where needed. No quantities, availability or business facts were confirmed."};
        }
        if(tool is "job.summarize" or "job.readiness")return new{workspace.State,workspace.NextAction,workspace.ScheduleBlockers,workspace.StartBlockers,workspace.CompletionBlockers,activity=workspace.Job.Activity.Take(10)};
        if(tool=="job.recommend")return await service.RecommendAsync(id,input.GetProperty("startUtc").GetDateTime(),input.GetProperty("endUtc").GetDateTime(),ct);
        if(tool=="job.schedule"){var schedule=input.Deserialize<JobEventInputDto>(JobConfigurationService.Json)??throw new ArgumentException("Schedule required.");return await service.ScheduleAsync(id,schedule,ct);}
        var version=input.GetProperty("expectedVersion").GetString()??"";
        if(tool=="job.notify")return await delivery.SendAsync(id,version,input.GetProperty("channel").GetString()??"",input.GetProperty("template").GetString()??"",ct);
        var command=input.Deserialize<JobCommandDto>(JobConfigurationService.Json)??new();command.ExpectedVersion=version;
        command.Action=tool switch{"job.task"=>"add-task","job.change"=>"change","job.material"=>"requirement","job.activity"=>"activity","job.transition"=>"transition",_=>throw new ArgumentException("Unknown Job action.")};
        return await service.CommandAsync(id,command,ct);
    }
}
