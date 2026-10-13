using System.Text.Json;
using System.Globalization;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
// Read-only explanations consume the same authoritative results as the construction workspace.
// Financial mutations remain explicit, authorized construction commands.
public sealed class BobConstructionFinanceActionProvider(ConstructionFinanceService service,TurnKeyOps.Lib.Utils.IUserContext user,string tool):IBobActionProvider
{
    public string ToolKey=>tool;
    public string PermissionKey=>"finance.read";
    public BobActionRisk Risk=>BobActionRisk.Read;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
    {
        if(context.TenantId!=user.TenantId||context.ActorUserId!=user.UserId)throw new UnauthorizedAccessException();
        var asOf=DateOnly.FromDateTime(DateTime.UtcNow);
        if(input.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Construction review input must be an object.");
        if(input.TryGetProperty("asOf",out var value)&& (value.ValueKind!=JsonValueKind.String||!DateOnly.TryParseExact(value.GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out asOf)))throw new ArgumentException("An ISO as-of date is required.");
        var root=JsonSerializer.SerializeToElement(await service.WorkspaceAsync(asOf,ct),JobConfigurationService.Json);
        var jobs=root.GetProperty("jobs").Deserialize<ConstructionWipResult[]>(JobConfigurationService.Json)??[];
        var findings=new List<FinanceFinding>();
        foreach(var j in jobs)
        {
            var source=new[]{"job:"+j.JobId};
            foreach(var exception in j.Exceptions)findings.Add(new("unknown",exception,source,"Review the contract, incurred costs and forecast evidence before relying on this result."));
            var message=tool switch
            {
                "finance.construction-health"=>$"Projected final cost: {Amount(j.EstimatedFinalCost)}; projected profit: {Amount(j.ProjectedProfit)}. Actual cost {Amount(j.ActualCost)}, remaining commitment {Amount(j.RemainingCommitment)} and reviewed remaining forecast {Amount(j.RemainingForecast)}.",
                "finance.construction-wip"=>$"Earned revenue {Amount(j.EarnedRevenue)} versus billed {Amount(j.Billed)}; underbilling {Amount(j.Underbilling)}; overbilling {Amount(j.Overbilling)}.",
                "finance.construction-retainage"=>$"Retainage remaining: {Amount(j.RetainageHeld)}. Release eligibility requires review of contractual milestones and authorization.",
                "finance.construction-backlog"=>$"Contract less earned revenue: {Amount(j.EarnedRevenue.HasValue?j.ContractValue-j.EarnedRevenue.Value:null)}. Contract less billed: {Amount(j.ContractValue-j.Billed)}. These are separate backlog definitions.",
                _=>throw new ArgumentException("Unsupported construction review.")
            };
            findings.Add(new(j.ProjectedProfit<0?"warning":"info",message,source,"Open Construction finance to review calculation inputs and source records."));
        }
        return new FinanceAnalysis(tool,asOf,"Construction financial review",$"{jobs.Length} configured construction Jobs reviewed.",findings.Take(5).ToArray(),[$"{Math.Max(0,findings.Count-5)} additional findings omitted; open Construction finance for the full report.","Values come from deterministic TurnKey records; missing inputs remain unknown.","Results use the construction workspace as-of view, including a posted snapshot when one exists for that period end. Review the immutable WIP snapshot and linked journal for period posting evidence.","No forecast, time approval, retainage release, journal posting or period close was performed."]);
    }
    private static string Amount(decimal? value)=>value.HasValue?$"{value.Value:0.00} USD":"Unknown";
}
