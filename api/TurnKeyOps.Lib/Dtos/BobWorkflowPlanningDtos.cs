namespace TurnKeyOps.Lib.Dtos;
public sealed record BobAttentionWorkflowRequest(string AttentionId, string Goal, string IdempotencyKey);
