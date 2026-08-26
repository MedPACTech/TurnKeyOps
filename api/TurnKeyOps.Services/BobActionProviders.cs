using System.Text.Json;
using MedInsights.Lib.Authorization;
using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class BobConversationReadProvider : IBobActionProvider
{
    private readonly IChatRepository _repository;

    public BobConversationReadProvider(IChatRepository repository) => _repository = repository;

    public string ToolKey => "conversation.read";
    public string PermissionKey => TurnKeyPermissionKeys.OperationsRead;
    public BobActionRisk Risk => BobActionRisk.Read;

    public async Task<object?> ExecuteAsync(
        BobActionExecutionContext context,
        JsonElement input,
        CancellationToken ct = default)
    {
        var chat = await _repository.GetAsync(
            context.PartitionKey,
            MedInsights.Lib.EntityKeyPolicy.Row(context.ConversationId),
            ct);
        if (chat is null)
            throw new KeyNotFoundException("Conversation not found.");

        return new
        {
            conversationId = chat.Id,
            title = chat.Title,
            updatedAtUtc = chat.DateChatUpdated,
            archived = chat.ArchivedAtUtc.HasValue
        };
    }
}

public sealed class BobConversationArchiveProvider : IBobActionProvider
{
    private readonly IChatRepository _repository;

    public BobConversationArchiveProvider(IChatRepository repository) => _repository = repository;

    public string ToolKey => "conversation.archive";
    public string PermissionKey => TurnKeyPermissionKeys.OperationsManage;
    public BobActionRisk Risk => BobActionRisk.Destructive;

    public async Task<object?> ExecuteAsync(
        BobActionExecutionContext context,
        JsonElement input,
        CancellationToken ct = default)
    {
        var chat = await _repository.GetAsync(
            context.PartitionKey,
            MedInsights.Lib.EntityKeyPolicy.Row(context.ConversationId),
            ct);
        if (chat is null)
            throw new KeyNotFoundException("Conversation not found.");

        chat.ArchivedAtUtc ??= DateTime.UtcNow;
        chat.DateChatUpdated = DateTime.UtcNow;
        await _repository.SaveAsync(chat, ct);
        return new { conversationId = chat.Id, archivedAtUtc = chat.ArchivedAtUtc };
    }
}
