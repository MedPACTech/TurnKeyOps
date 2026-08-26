using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class BobOperationsService : IBobOperationsService
{
    private const string Proposed = "proposed";
    private const string Approved = "approved";
    private const string Executing = "executing";
    private const string Completed = "completed";
    private const string Failed = "failed";

    private readonly IBobActionRepository _repository;
    private readonly MedInsights.Repositories.Interfaces.IChatRepository _chatRepository;
    private readonly IReadOnlyDictionary<string, IBobActionProvider> _providers;
    private readonly IUserContext _userContext;
    private readonly IRoleAccessService _roleAccess;
    private readonly IAuditService _audit;
    private readonly IBobContextMinimizer _minimizer;
    private readonly BobOperationsOptions _options;

    public BobOperationsService(
        IBobActionRepository repository,
        MedInsights.Repositories.Interfaces.IChatRepository chatRepository,
        IEnumerable<IBobActionProvider> providers,
        IUserContext userContext,
        IRoleAccessService roleAccess,
        IAuditService audit,
        IBobContextMinimizer minimizer,
        IOptions<BobOperationsOptions> options)
    {
        _repository = repository;
        _chatRepository = chatRepository;
        _providers = providers.ToDictionary(provider => provider.ToolKey, StringComparer.OrdinalIgnoreCase);
        _userContext = userContext;
        _roleAccess = roleAccess;
        _audit = audit;
        _minimizer = minimizer;
        _options = options.Value;
    }

    public async Task<BobActionDto> ProposeAsync(
        Guid conversationId,
        ProposeBobActionDto input,
        CancellationToken ct = default)
    {
        EnsureEnabled();
        if (conversationId == Guid.Empty) throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(input.ToolKey)) throw new ArgumentException("Tool key is required.", nameof(input.ToolKey));
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Trim().Length > 128)
            throw new ArgumentException("A stable idempotency key of 1-128 characters is required.", nameof(input.IdempotencyKey));

        var provider = GetProvider(input.ToolKey);
        EnsureWriteEnabled(provider);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);

        var partitionKey = PartitionKey();
        await RequireConversationAsync(partitionKey, conversationId, ct);
        var idempotencyKey = input.IdempotencyKey.Trim();
        var replay = await _repository.FindByIdempotencyKeyAsync(partitionKey, idempotencyKey, ct);
        if (replay is not null)
        {
            if (replay.ConversationId != conversationId ||
                !string.Equals(replay.ToolKey, provider.ToolKey, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The idempotency key is already bound to another Bob action.");
            return Map(replay);
        }

        var now = DateTime.UtcNow;
        var actionId = Guid.NewGuid();
        var confirmationRequired = RequiresConfirmation(provider.Risk);
        var minimizedInput = _minimizer.Minimize(
            input.Input.ValueKind == JsonValueKind.Undefined ? new { } : input.Input,
            _options.MaxStoredInputCharacters);
        var entity = new BobActionRecord
        {
            Id = actionId,
            PartitionKey = partitionKey,
            RowKey = BobActionRepository.ActionRowKey(actionId),
            TenantId = _userContext.TenantId,
            ActorUserId = _userContext.UserId,
            ConversationId = conversationId,
            ToolKey = provider.ToolKey,
            Risk = provider.Risk.ToString().ToLowerInvariant(),
            Status = Proposed,
            ConfirmationRequired = confirmationRequired,
            IdempotencyKey = idempotencyKey,
            InputJson = minimizedInput.GetRawText(),
            ProposedAtUtc = now,
            UpdatedAtUtc = now
        };

        entity = await _repository.SaveAsync(entity, ct);
        await AuditAsync(entity, "proposed", ct);
        return confirmationRequired
            ? Map(entity)
            : await ExecuteEntityAsync(entity, provider, ct);
    }

    public async Task<BobActionDto> ApproveAsync(Guid actionId, CancellationToken ct = default)
    {
        EnsureEnabled();
        var entity = await RequireActionAsync(actionId, ct);
        var provider = GetProvider(entity.ToolKey);
        EnsureWriteEnabled(provider);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);

        if (!entity.ConfirmationRequired || entity.Status == Completed)
            return Map(entity);
        if (entity.Status is not Proposed and not Failed and not Approved)
            throw new InvalidOperationException($"Action cannot be approved while it is {entity.Status}.");

        if (entity.Status != Approved)
        {
            entity.Status = Approved;
            entity.ApprovedAtUtc = DateTime.UtcNow;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.FailureCode = string.Empty;
            entity = await _repository.SaveAsync(entity, ct);
            await AuditAsync(entity, "approved", ct);
        }
        return Map(entity);
    }

    public async Task<BobActionDto> ExecuteAsync(Guid actionId, CancellationToken ct = default)
    {
        EnsureEnabled();
        var entity = await RequireActionAsync(actionId, ct);
        var provider = GetProvider(entity.ToolKey);
        EnsureWriteEnabled(provider);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        if (entity.Status == Completed) return Map(entity);
        if (entity.ConfirmationRequired && entity.Status != Approved && entity.Status != Failed)
            throw new InvalidOperationException("This Bob action requires explicit approval before execution.");
        if (entity.ConfirmationRequired && entity.Status == Failed && !entity.ApprovedAtUtc.HasValue)
            throw new InvalidOperationException("This Bob action requires explicit approval before retry.");
        return await ExecuteEntityAsync(entity, provider, ct);
    }

    public async Task<IReadOnlyList<BobActionDto>> ListAsync(Guid conversationId, CancellationToken ct = default)
    {
        EnsureEnabled();
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsRead, ct);
        var partitionKey = PartitionKey();
        await RequireConversationAsync(partitionKey, conversationId, ct);
        var actions = await _repository.ListByConversationAsync(partitionKey, conversationId, ct);
        return actions.Select(Map).ToList();
    }

    private async Task<BobActionDto> ExecuteEntityAsync(
        BobActionRecord entity,
        IBobActionProvider provider,
        CancellationToken ct)
    {
        entity.Status = Executing;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.FailureCode = string.Empty;
        entity = await _repository.SaveAsync(entity, ct);

        try
        {
            using var inputDocument = JsonDocument.Parse(entity.InputJson);
            var context = new BobActionExecutionContext(
                entity.TenantId,
                entity.ActorUserId,
                entity.ConversationId,
                entity.PartitionKey);
            var result = await provider.ExecuteAsync(context, inputDocument.RootElement.Clone(), ct);
            entity.ResultJson = _minimizer.Minimize(result, _options.MaxStoredInputCharacters).GetRawText();
            entity.Status = Completed;
            entity.ExecutedAtUtc = DateTime.UtcNow;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity = await _repository.SaveAsync(entity, ct);
            await AuditAsync(entity, "completed", ct);
            return Map(entity);
        }
        catch (Exception exception)
        {
            entity.Status = Failed;
            entity.FailureCode = exception.GetType().Name;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _repository.SaveAsync(entity, ct);
            await AuditAsync(entity, "failed", ct);
            throw;
        }
    }

    private IBobActionProvider GetProvider(string toolKey) =>
        _providers.TryGetValue(toolKey.Trim(), out var provider)
            ? provider
            : throw new ArgumentException("Bob does not support that action.", nameof(toolKey));

    private async Task RequireConversationAsync(string partitionKey, Guid conversationId, CancellationToken ct)
    {
        var chat = await _chatRepository.GetAsync(
            partitionKey,
            MedInsights.Lib.EntityKeyPolicy.Row(conversationId),
            ct);
        if (chat is null)
            throw new KeyNotFoundException("Conversation not found.");
    }

    private async Task<BobActionRecord> RequireActionAsync(Guid actionId, CancellationToken ct) =>
        await _repository.GetAsync(PartitionKey(), actionId, ct)
        ?? throw new KeyNotFoundException("Bob action not found.");

    private string PartitionKey()
    {
        if (!_userContext.IsAuthenticated)
            throw new UnauthorizedAccessException();
        return MedInsights.Lib.EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Bob operational actions are disabled.");
    }

    private void EnsureWriteEnabled(IBobActionProvider provider)
    {
        if (provider.Risk != BobActionRisk.Read && !_options.WriteActionsEnabled)
            throw new InvalidOperationException("Bob write actions are disabled.");
    }

    public static bool RequiresConfirmation(BobActionRisk risk) => risk is
        BobActionRisk.Destructive or
        BobActionRisk.Financial or
        BobActionRisk.Scheduling or
        BobActionRisk.CustomerFacing;

    private Task AuditAsync(BobActionRecord entity, string action, CancellationToken ct) =>
        _audit.RecordAsync(new RecordAuditEventRequestDto
        {
            Category = "bob_action",
            Action = action,
            Severity = action == "failed" ? "warning" : "info",
            TargetType = "bob_action",
            TargetId = entity.Id.ToString("N"),
            Source = "bob_operations",
            Description = $"Bob action {entity.ToolKey} {action}.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                entity.ConversationId,
                entity.ToolKey,
                entity.Risk,
                entity.Status,
                entity.ConfirmationRequired
            })
        }, ct);

    private static BobActionDto Map(BobActionRecord entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        ToolKey = entity.ToolKey,
        Risk = entity.Risk,
        Status = entity.Status,
        ConfirmationRequired = entity.ConfirmationRequired,
        ResultJson = entity.ResultJson,
        FailureCode = entity.FailureCode,
        ProposedAtUtc = entity.ProposedAtUtc,
        ApprovedAtUtc = entity.ApprovedAtUtc,
        ExecutedAtUtc = entity.ExecutedAtUtc
    };
}
