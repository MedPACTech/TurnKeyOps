using System.Text.Json;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
// Owner-only deterministic operations explanations deliberately exclude labor rates and tax identifiers.
public sealed class BobFinanceOperationsActionProvider(FinanceOperationsService service,TurnKeyOps.Lib.Utils.IUserContext user,string tool):IBobActionProvider
{
 public string ToolKey=>tool;public string PermissionKey=>"finance.read";public BobActionRisk Risk=>BobActionRisk.Read;
 public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
 {
  if(context.TenantId!=user.TenantId||context.ActorUserId!=user.UserId)throw new UnauthorizedAccessException();
  if(input.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Operations review requires object input.");
  var asOf=DateOnly.FromDateTime(DateTime.UtcNow);var root=JsonSerializer.SerializeToElement(await service.ReadAsync(ct),JobConfigurationService.Json);var findings=new List<FinanceFinding>();
  if(tool=="finance.time-blockers")foreach(var period in root.GetProperty("close").EnumerateArray())foreach(var issue in period.GetProperty("issues").EnumerateArray())findings.Add(new("blocking",issue.GetString()??"Review close blocker",["period:"+period.GetProperty("id").GetString()],"Review operations time approvals and close evidence."));
  else if(tool=="finance.vendor-compliance")foreach(var vendor in root.GetProperty("state").GetProperty("vendors").EnumerateArray())
  {
   var expired=vendor.TryGetProperty("insuranceExpiry",out var expiry)&&expiry.ValueKind==JsonValueKind.String&&DateOnly.TryParse(expiry.GetString(),out var date)&&date<asOf;
   if(expired)findings.Add(new("warning","Vendor insurance has expired.",["vendor:"+vendor.GetProperty("vendorId").GetString()],"Review vendor compliance attachments and obtain current insurance evidence."));
  }
  else throw new ArgumentException("Unsupported operations review.");
  return new FinanceAnalysis(tool,asOf,"Finance operations review",$"{findings.Count} source-backed findings.",findings.Take(5).ToArray(),[$"{Math.Max(0,findings.Count-5)} additional findings omitted; review operations records for full detail.","Owner controls apply. Labor rates and sensitive tax identifiers are excluded. No approvals, payments, postings or close actions were performed."]);
 }
}
