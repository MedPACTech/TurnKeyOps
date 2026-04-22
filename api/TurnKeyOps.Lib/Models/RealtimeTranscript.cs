
namespace MedInsights.Lib.Models;

public record RealtimeTranscript(Guid UserId, Guid TenantId, Guid ChatId, string Role, string Content, int TokensUsed, DateTime DateAt);
