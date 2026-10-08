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
    private readonly LeadConfigurationService? _leadConfiguration;
    private readonly JobConfigurationService? _jobConfiguration;
    private readonly ISupplyStore? _supplyStore;

    public BobOperationsService(
        IBobActionRepository repository,
        MedInsights.Repositories.Interfaces.IChatRepository chatRepository,
        IEnumerable<IBobActionProvider> providers,
        IUserContext userContext,
        IRoleAccessService roleAccess,
        IAuditService audit,
        IBobContextMinimizer minimizer,
        IOptions<BobOperationsOptions> options,
        LeadConfigurationService? leadConfiguration = null, JobConfigurationService? jobConfiguration = null, ISupplyStore? supplyStore = null)
    {
        _repository = repository;
        _chatRepository = chatRepository;
        _providers = providers.ToDictionary(provider => provider.ToolKey, StringComparer.OrdinalIgnoreCase);
        _userContext = userContext;
        _roleAccess = roleAccess;
        _audit = audit;
        _minimizer = minimizer;
        _options = options.Value;
        _leadConfiguration = leadConfiguration;
        _jobConfiguration = jobConfiguration;
        _supplyStore = supplyStore;
    }

    public async Task<BobActionDto> ApproveLeadAsync(Guid leadId, Guid actionId, CancellationToken ct = default)
    {
        var action = await RequireActionAsync(actionId, ct);
        if (action.ConversationId != leadId || !action.ToolKey.StartsWith("lead.", StringComparison.Ordinal))
            throw new ArgumentException("Action belongs to another Lead.");
        await ApproveAsync(actionId, ct);
        return await ExecuteAsync(actionId, ct);
    }

    public async Task<BobActionDto> ProposeLeadAsync(Guid leadId, ProposeBobActionDto input, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (leadId == Guid.Empty || !input.ToolKey.StartsWith("lead.", StringComparison.Ordinal) ||
            !input.Input.TryGetProperty("leadId", out var inputId) || inputId.GetGuid() != leadId)
            throw new ArgumentException("The action must target this Lead.");
        var provider = GetProvider(input.ToolKey);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        await PolicyRequiresApprovalAsync(provider, ct);
        var partition = PartitionKey();
        var row = MedInsights.Lib.EntityKeyPolicy.Row(leadId);
        var conversation = await _chatRepository.GetAsync(partition, row, ct);
        if (conversation is null)
            await _chatRepository.SaveAsync(new MedInsights.Lib.Entities.Chat {
                Id = leadId, TenantId = _userContext.TenantId, ActorUserId = _userContext.UserId,
                PartitionKey = partition, RowKey = row, Title = "Lead workspace", Mode = "lead",
                StateJson = JsonSerializer.Serialize(new { leadId }), DateChatCreated = DateTime.UtcNow, DateChatUpdated = DateTime.UtcNow
            }, ct);
        return await ProposeAsync(leadId, input, ct);
    }

    public async Task<BobActionDto> ApproveEstimateAsync(Guid estimateId, Guid actionId, CancellationToken ct = default)
    {
        var action = await RequireActionAsync(actionId, ct);
        if (action.ConversationId != estimateId || !action.ToolKey.StartsWith("estimate.", StringComparison.Ordinal))
            throw new ArgumentException("Action belongs to another Estimate.");
        await ApproveAsync(actionId, ct);
        return await ExecuteAsync(actionId, ct);
    }

    public async Task<BobActionDto> ProposeEstimateAsync(Guid estimateId, ProposeBobActionDto input, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (estimateId == Guid.Empty || !input.ToolKey.StartsWith("estimate.", StringComparison.Ordinal) ||
            !input.Input.TryGetProperty("estimateId", out var inputId) || inputId.GetGuid() != estimateId)
            throw new ArgumentException("The action must target this Estimate.");
        var provider = GetProvider(input.ToolKey);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        await PolicyRequiresApprovalAsync(provider, ct);
        var partition = PartitionKey();
        var row = MedInsights.Lib.EntityKeyPolicy.Row(estimateId);
        var conversation = await _chatRepository.GetAsync(partition, row, ct);
        if (conversation is null)
            await _chatRepository.SaveAsync(new MedInsights.Lib.Entities.Chat {
                Id = estimateId, TenantId = _userContext.TenantId, ActorUserId = _userContext.UserId,
                PartitionKey = partition, RowKey = row, Title = "Estimate workspace", Mode = "estimate",
                StateJson = JsonSerializer.Serialize(new { estimateId }), DateChatCreated = DateTime.UtcNow, DateChatUpdated = DateTime.UtcNow
            }, ct);
        return await ProposeAsync(estimateId, input, ct);
    }

    public async Task<BobActionDto> ApproveJobAsync(Guid jobId, Guid actionId, CancellationToken ct = default)
    {
        var action = await RequireActionAsync(actionId, ct);
        if (action.ConversationId != jobId || !action.ToolKey.StartsWith("job.", StringComparison.Ordinal))
            throw new ArgumentException("Action belongs to another Job.");
        await ApproveAsync(actionId, ct);
        return await ExecuteAsync(actionId, ct);
    }

    public async Task<BobActionDto> ProposeJobAsync(Guid jobId, ProposeBobActionDto input, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (jobId == Guid.Empty || !input.ToolKey.StartsWith("job.", StringComparison.Ordinal) ||
            !input.Input.TryGetProperty("jobId", out var inputId) || inputId.GetGuid() != jobId)
            throw new ArgumentException("The action must target this Job.");
        var provider = GetProvider(input.ToolKey);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        await PolicyRequiresApprovalAsync(provider, ct);
        var partition = PartitionKey();
        var row = MedInsights.Lib.EntityKeyPolicy.Row(jobId);
        var conversation = await _chatRepository.GetAsync(partition, row, ct);
        if (conversation is null)
            await _chatRepository.SaveAsync(new MedInsights.Lib.Entities.Chat {
                Id = jobId, TenantId = _userContext.TenantId, ActorUserId = _userContext.UserId,
                PartitionKey = partition, RowKey = row, Title = "Job workspace", Mode = "job",
                StateJson = JsonSerializer.Serialize(new { jobId }), DateChatCreated = DateTime.UtcNow, DateChatUpdated = DateTime.UtcNow
            }, ct);
        return await ProposeAsync(jobId, input, ct);
    }

    public async Task<BobActionDto> ApproveSupplyAsync(Guid supplyId, Guid actionId, CancellationToken ct = default)
    {
        var action = await RequireActionAsync(actionId, ct);
        if (action.ConversationId != supplyId || !action.ToolKey.StartsWith("supply.", StringComparison.Ordinal))
            throw new ArgumentException("Action belongs to another supply workspace.");
        await ApproveAsync(actionId, ct);
        return await ExecuteAsync(actionId, ct);
    }

    public async Task<BobActionDto> ProposeSupplyAsync(Guid supplyId, ProposeBobActionDto input, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (supplyId == Guid.Empty || !input.ToolKey.StartsWith("supply.", StringComparison.Ordinal) ||
            !input.Input.TryGetProperty("supplyId", out var inputId) || inputId.GetGuid() != supplyId)
            throw new ArgumentException("The action must target this supply workspace.");
        var provider = GetProvider(input.ToolKey);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        await PolicyRequiresApprovalAsync(provider, ct);
        var partition = PartitionKey();
        var row = MedInsights.Lib.EntityKeyPolicy.Row(supplyId);
        var conversation = await _chatRepository.GetAsync(partition, row, ct);
        if (conversation is null)
            await _chatRepository.SaveAsync(new MedInsights.Lib.Entities.Chat {
                Id = supplyId, TenantId = _userContext.TenantId, ActorUserId = _userContext.UserId,
                PartitionKey = partition, RowKey = row, Title = "Supply workspace", Mode = "supply",
                StateJson = JsonSerializer.Serialize(new { supplyId }), DateChatCreated = DateTime.UtcNow, DateChatUpdated = DateTime.UtcNow
            }, ct);
        return await ProposeAsync(supplyId, input, ct);
    }

    public async Task<BobActionDto> ApproveFinanceAsync(Guid financeId, Guid actionId, CancellationToken ct = default)
    {
        var action = await RequireActionAsync(actionId, ct);
        if (action.ConversationId != financeId || !action.ToolKey.StartsWith("finance.", StringComparison.Ordinal))
            throw new ArgumentException("Action belongs to another finance workspace.");
        await ApproveAsync(actionId, ct);
        return await ExecuteAsync(actionId, ct);
    }

    public async Task<BobActionDto> ProposeFinanceAsync(Guid financeId, ProposeBobActionDto input, CancellationToken ct = default)
    {
        EnsureEnabled();
        if (financeId == Guid.Empty || !input.ToolKey.StartsWith("finance.", StringComparison.Ordinal) ||
            !input.Input.TryGetProperty("financeId", out var inputId) || inputId.GetGuid() != financeId)
            throw new ArgumentException("The action must target this finance workspace.");
        var provider = GetProvider(input.ToolKey);
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        await PolicyRequiresApprovalAsync(provider, ct);
        var partition = PartitionKey();
        var row = MedInsights.Lib.EntityKeyPolicy.Row(financeId);
        var conversation = await _chatRepository.GetAsync(partition, row, ct);
        if (conversation is null)
            await _chatRepository.SaveAsync(new MedInsights.Lib.Entities.Chat {
                Id = financeId, TenantId = _userContext.TenantId, ActorUserId = _userContext.UserId,
                PartitionKey = partition, RowKey = row, Title = "Finance workspace", Mode = "finance",
                StateJson = JsonSerializer.Serialize(new { financeId }), DateChatCreated = DateTime.UtcNow, DateChatUpdated = DateTime.UtcNow
            }, ct);
        return await ProposeAsync(financeId, input, ct);
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
        var confirmationRequired = await PolicyRequiresApprovalAsync(provider, ct);
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
        await _roleAccess.RequirePermissionAsync(provider.PermissionKey, ct);
        if (await PolicyRequiresApprovalAsync(provider, ct) && !entity.ApprovedAtUtc.HasValue)
            throw new InvalidOperationException("Tenant policy requires approval for this action.");
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

    private async Task<bool> PolicyRequiresApprovalAsync(IBobActionProvider provider, CancellationToken ct)
    {
        var mode=provider.Risk==BobActionRisk.Read?"read":"approval";
        if(_leadConfiguration is not null)mode=(await _leadConfiguration.GetAsync(_userContext.TenantId,ct)).AiActions.GetValueOrDefault(provider.ToolKey,mode);
        if(provider.ToolKey.StartsWith("job.")&&_jobConfiguration is not null)mode=(await _jobConfiguration.GetAsync(_userContext.TenantId,ct)).AiActions.GetValueOrDefault(provider.ToolKey,mode);
        if(provider.ToolKey.StartsWith("supply.")&&_supplyStore is not null)mode=(await _supplyStore.ReadAsync(_userContext.TenantId,ct)).Policy.AiActions.GetValueOrDefault(provider.ToolKey,mode);
        if (mode is "disabled" or "recommend" || mode == "draft" && provider.ToolKey is not ("lead.draft" or "estimate.extract" or "job.change" or "supply.draft-order"))
            throw new InvalidOperationException("Tenant policy does not allow this action to execute.");
        if (mode == "read" && provider.Risk != BobActionRisk.Read)
            throw new InvalidOperationException("Read-only policy cannot execute a mutation.");
        if(provider.ToolKey=="finance.draft-journal")return true;
        // Financial outcomes remain approved until a deterministic outcome guardrail is configured.
        if (provider.ToolKey is "lead.stage" or "estimate.issue") return true;
        return mode == "approval" || (mode != "auto" && mode != "draft" && provider.Risk != BobActionRisk.Read);
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
