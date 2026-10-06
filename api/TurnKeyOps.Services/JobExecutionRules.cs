using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
namespace TurnKeyOps.Services;

public static class JobExecutionRules
{
    public static readonly Dictionary<string,JobStatus> States = new()
    {
        ["DRAFT"]=JobStatus.Draft,["PLANNING"]=JobStatus.Planning,["BLOCKED"]=JobStatus.Blocked,
        ["READY_TO_SCHEDULE"]=JobStatus.ReadyToSchedule,["SCHEDULED"]=JobStatus.Scheduled,
        ["READY_TO_START"]=JobStatus.ReadyToStart,["IN_PROGRESS"]=JobStatus.InProgress,
        ["PAUSED"]=JobStatus.Paused,["WAITING"]=JobStatus.Waiting,["READY_FOR_COMPLETION"]=JobStatus.ReadyForCompletion,
        ["COMPLETION_REVIEW"]=JobStatus.CompletionReview,["COMPLETED"]=JobStatus.Completed,["CLOSED"]=JobStatus.Closed,["CANCELLED"]=JobStatus.Cancelled
    };
    public static string State(JobStatus status) => States.FirstOrDefault(x=>x.Value==status).Key ??
        (status==JobStatus.OnHold?"PAUSED":"PLANNING");
    public static string Trade(TradeType type)=>type switch {TradeType.Concrete=>"concrete",TradeType.Framing=>"framing",TradeType.DoorsLocksmith=>"doors-locks",TradeType.LandClearing=>"land-clearing",_=>"general"};
    public static TradeType Trade(string key)=>key switch {"concrete"=>TradeType.Concrete,"framing"=>TradeType.Framing,"doors-locks"=>TradeType.DoorsLocksmith,"land-clearing"=>TradeType.LandClearing,_=>TradeType.General};
    public static string[] Next(string state)=>state switch {
        "DRAFT"=>["PLANNING","CANCELLED"],"PLANNING"=>["READY_TO_SCHEDULE","BLOCKED","CANCELLED"],
        "BLOCKED"=>["PLANNING","READY_TO_SCHEDULE","CANCELLED"],"READY_TO_SCHEDULE"=>["SCHEDULED","PLANNING","BLOCKED","CANCELLED"],
        "SCHEDULED"=>["READY_TO_START","BLOCKED","CANCELLED"],"READY_TO_START"=>["IN_PROGRESS","BLOCKED","CANCELLED"],
        "IN_PROGRESS"=>["READY_FOR_COMPLETION","PAUSED","WAITING","BLOCKED","CANCELLED"],
        "PAUSED" or "WAITING"=>["IN_PROGRESS","PLANNING","CANCELLED"],
        "READY_FOR_COMPLETION"=>["COMPLETION_REVIEW","IN_PROGRESS"],"COMPLETION_REVIEW"=>["COMPLETED","IN_PROGRESS"],
        "COMPLETED"=>["CLOSED","IN_PROGRESS"],"CLOSED" or "CANCELLED"=>["PLANNING"],_=>[]};
    public static string NextAction(string state)=>state switch {
        "DRAFT" or "PLANNING"=>"Complete pre-job planning","BLOCKED"=>"Resolve the blocker",
        "READY_TO_SCHEDULE"=>"Schedule qualified people and resources","SCHEDULED"=>"Confirm readiness to start",
        "READY_TO_START"=>"Start work","IN_PROGRESS"=>"Capture progress and complete the checklist",
        "PAUSED" or "WAITING"=>"Resolve the wait and resume work","READY_FOR_COMPLETION"=>"Review completion evidence",
        "COMPLETION_REVIEW"=>"Confirm operational completion","COMPLETED"=>"Obtain customer acceptance and close",
        "CLOSED"=>"Operational closeout finished",_=>"Review job"};
    public static List<string> Blockers(JobExecutionDto x,string stage)
    {
        var results=x.Issues.Where(i=>i.ResolvedAtUtc is null&&i.Severity is "blocker" or "critical").Select(i=>$"{i.Type}: {i.Description}").ToList();
        results.AddRange(x.Tasks.Where(t=>t.Required&&t.CompletedAtUtc is null&&(stage=="complete"||t.Stage=="schedule"||stage=="start"&&t.Stage=="start")).Select(t=>$"Complete: {t.Title}"));
        results.AddRange(x.Profile.RequiredFields.Where(k=>!x.TradeData.TryGetValue(k,out var v)||string.IsNullOrWhiteSpace(v)).Select(k=>$"Confirm {x.Profile.Fields.GetValueOrDefault(k,k)}"));
        if(stage!="schedule")results.AddRange(x.Requirements.Where(r=>r.Status is not ("Available" or "Delivered" or "Consumed" or "Returned" or "Cancelled")).Select(r=>$"{r.Kind}: {r.Description} is {r.Status}"));
        results.AddRange(x.Changes.Where(c=>c.Status is not ("IMPLEMENTED" or "DECLINED" or "CANCELLED")).Select(c=>$"Resolve change: {c.Description} ({c.Status})"));
        if(stage=="complete"&&x.Evidence.Count(e=>e.Purpose=="completion"&&e.ContentType.StartsWith("image/"))<x.Profile.CompletionPhotos)results.Add($"Capture {x.Profile.CompletionPhotos} completion photos");
        return results;
    }
    public static void Transition(JobDto job,string to,string note,bool hasEvent)
    {
        var from=State(job.Status);var x=job.Execution??throw new ArgumentException("Adopt the execution workflow first.");
        if(!Next(from).Contains(to))throw new ArgumentException($"Cannot move from {from} to {to}.");
        var reopening=from is "CLOSED" or "CANCELLED" || from=="COMPLETED"&&to!="CLOSED";
        if(reopening &&string.IsNullOrWhiteSpace(note))throw new ArgumentException("Explain why this job is being reopened.");
        var stage=to switch {"READY_TO_SCHEDULE" or "SCHEDULED"=>"schedule","READY_TO_START" or "IN_PROGRESS"=>"start","READY_FOR_COMPLETION" or "COMPLETION_REVIEW" or "COMPLETED" or "CLOSED"=>"complete",_=>null};
        var blockers=stage is null?[]:Blockers(x,stage);
        if(to is "SCHEDULED" or "READY_TO_START" or "IN_PROGRESS"&&!hasEvent)blockers.Add("Schedule a work event first");
        if(to=="CLOSED"&&x.Profile.CustomerAcceptanceRequired&&!x.Acceptances.Any(a=>a.CompletionRevision==x.CompletionRevision))blockers.Add("Customer completion acceptance is required");
        if(blockers.Count>0)throw new ArgumentException(string.Join("; ",blockers));
        if(reopening)x.CompletionRevision++;
        job.Status=States[to];
    }
}
