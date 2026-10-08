using System.Security.Cryptography;
using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class PortalRules
{
    public static string JobStatus(string state) => state switch {
        "PLANNING" or "DRAFT" or "READY_TO_SCHEDULE" => "Preparing your project",
        "SCHEDULED" or "READY_TO_START" => "Scheduled", "IN_PROGRESS" => "Work in progress",
        "WAITING" or "BLOCKED" or "PAUSED" => "Waiting on next step",
        "READY_FOR_COMPLETION" or "COMPLETION_REVIEW" => "Finalizing your project",
        "COMPLETED" or "CLOSED" => "Completed", "CANCELLED" => "Cancelled", _ => "Preparing your project"
    };
    public static string NextStep(string state) => state switch {
        "SCHEDULED" or "READY_TO_START" => "Review your upcoming appointment.",
        "IN_PROGRESS" => "We will share updates as work progresses.",
        "WAITING" or "BLOCKED" or "PAUSED" => "We will contact you when the next step is ready.",
        "COMPLETION_REVIEW" or "COMPLETED" => "Review the completion details when acceptance is requested.",
        "CLOSED" => "Contact us if you need help with your completed project.",
        "CANCELLED" => "Contact us if you have questions.", _ => "We will contact you about the next step."
    };
    public static string LeadStatus(string stage) => stage switch {
        "NEW" => "Request received", "NEEDS_RESPONSE" or "QUALIFYING" or "QUALIFIED" => "Reviewing details",
        "DISCOVERY" => "Reviewing your project", "READY_TO_ESTIMATE" or "ESTIMATING" => "Preparing estimate",
        "PROPOSAL" or "FOLLOW_UP" => "Proposal ready", "WON" => "Approved", "LOST" => "Request closed", _ => "Request received"
    };
    public static string ChangeHash(JobChangeDto c) => Hash(new { c.Id, c.Description, c.Reason, c.ScopeImpact, c.ScheduleImpact, c.PricingImpact, c.AcceptedEstimateId, c.AcceptedRevision, c.AcceptedDocumentHash, c.EvidenceIds });
    public static string CompletionHash(JobExecutionDto x) => Hash(new { x.SoldScope, x.TradeData, x.Tasks, x.Requirements, x.Changes, x.Issues, x.Evidence, x.CompletionRevision });
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JobConfigurationService.Json)));
    public const string CompletionStatement = "I confirm that I have reviewed the completed scope and shared completion documents and accept this work.";
    public static void ExactVersion(string expected, string actual) { if (string.IsNullOrWhiteSpace(expected) || expected == "*" || expected != actual) throw new InvalidOperationException("This record changed. Refresh before submitting."); }
    public static string Text(string value, int max = 4000) { if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw new ArgumentException($"Enter text between 1 and {max} characters."); return value.Trim(); }
}
