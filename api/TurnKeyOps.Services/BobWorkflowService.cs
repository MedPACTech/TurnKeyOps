using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;

public sealed partial class BobIntelligenceService
{
    public async Task<BobWorkflow> PlanAttentionAsync(BobAttentionWorkflowRequest request, CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct);
        var allowed = await Readable(state.Policy, permissions, ct);
        var attention = state.Attention.SingleOrDefault(a => a.Id == request.AttentionId && a.ActorUserId == user.UserId && a.Status == "open" && allowed.Contains(a.Source.Module)) ?? throw new KeyNotFoundException("Attention is no longer available.");
        var context = await ContextAsync(attention.Source.Module, attention.Source.EntityId, ct);
        var fact = context.Facts.SingleOrDefault(f => f.Source.Condition == attention.Source.Condition && f.Source.EntityId == attention.Source.EntityId) ?? throw new InvalidOperationException("Source condition changed. Refresh before planning.");
        if (!Guid.TryParse(fact.Source.EntityId, out var entity)) entity = user.TenantId;
        var steps = new List<BobWorkflowStep>();
        void Add(string capability, object input, string rationale) => steps.Add(new() { CapabilityId = capability, ConversationId = entity, Input = JsonSerializer.SerializeToElement(input, JobConfigurationService.Json), Rationale = rationale });
        switch (request.Goal)
        {
            case "review":
                if (fact.CapabilityId is null) throw new ArgumentException("Review this condition in its source module.");
                var input = new Dictionary<string, object> { [fact.Source.Module switch { "leads" => "leadId", "estimates" => "estimateId", "jobs" => "jobId", _ => "entityId" }] = entity };
                Add(fact.CapabilityId, input, "Review the current authoritative record and its recorded blockers");
                break;
            case "assign-follow-up" when fact.Source.Module == "leads":
                Add("lead.assign", new { leadId = entity, expectedVersion = fact.Source.Version }, "Assign using the tenant's deterministic territory and workload rules");
                Add("lead.task", new { leadId = entity, expectedVersion = fact.Source.Version, text = "Review the lead and complete the next recorded follow-up step." }, "Create a source-module follow-up task after assignment");
                Add("lead.draft", new { leadId = entity, expectedVersion = fact.Source.Version, channel = "email", subject = "Following up on your request", body = "Thank you for your request. We are reviewing the details and will contact you about the next step." }, "Prepare an editable response draft; this workflow does not send it");
                break;
            case "coordinate-readiness" when fact.Source.Module == "jobs":
                Add("job.readiness", new { jobId = entity }, "Review deterministic Job readiness requirements");
                if (allowed.Contains("inventory")) Add("supply.availability", new { entityId = entity }, "Check authoritative material availability without changing reservations");
                if (allowed.Contains("purchasing")) Add("supply.purchasing", new { entityId = entity }, "Review recorded purchasing and delivery readiness");
                Add("job.task", new { jobId = entity, expectedVersion = fact.Source.Version, task = new { title = "Coordinate job and material readiness", stage = "start", required = false }, text = "Resolve the recorded readiness blockers before changing the schedule." }, "Create an internal coordination task; scheduling changes require a separate reviewed action");
                break;
            case "create-task" when fact.Source.Module == "jobs":
                Add("job.task", new { jobId = entity, expectedVersion = fact.Source.Version, task = new { title = "Review the recorded readiness blockers", stage = "start", required = false }, text = "Review current job readiness before proceeding." }, "Create an internal task in the authoritative Job workflow");
                break;
            default: throw new ArgumentException("Select a supported next step for this source module.");
        }
        return await CreateWorkflowAsync(new(request.Goal == "assign-follow-up" ? "Assign lead, create follow-up, and draft response" : request.Goal == "create-task" ? "Create a job readiness task" : "Review: " + fact.Title, request.IdempotencyKey, steps), ct);
    }
    public async Task<BobWorkflow> WatchAsync(string attentionId, CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct);
        if (!permissions.Contains("bob.write")) throw new MedInsights.Lib.ForbiddenAccessException("Bob write access is required.");
        var allowed = await Readable(state.Policy, permissions, ct);
        var item = state.Attention.SingleOrDefault(a => a.Id == attentionId && a.ActorUserId == user.UserId && a.Status == "open" && allowed.Contains(a.Source.Module)) ?? throw new KeyNotFoundException();
        if (item.CapabilityId is null) throw new ArgumentException("This condition has no registered watch capability.");
        var key = "watch:" + item.Id;
        var existing = state.Workflows.SingleOrDefault(w => w.ActorUserId == user.UserId && w.IdempotencyKey == key && w.Status == "watching");
        if (existing is not null) return existing;
        var workflow = new BobWorkflow { ActorUserId = user.UserId, Goal = "Watching: " + item.Title, Status = "watching", IdempotencyKey = key,
            Steps = [new() { CapabilityId = item.CapabilityId, Rationale = "Check the recorded condition during authorized briefing refreshes", Status = "watching", Watching = item.Source }] };
        state.Workflows.Add(workflow);
        await Save(state, "watch.created", workflow.Id.ToString(), "User requested a source-condition watch; no external notification was sent", ct, item.Source.Module);
        return PublicWorkflow(workflow);
    }
    public async Task DismissWorkflowAsync(Guid id, CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct);
        if (!permissions.Contains("bob.write")) throw new MedInsights.Lib.ForbiddenAccessException("Bob write access is required.");
        if (!(await VisibleWorkflows(state, ct)).Any(w => w.Id == id)) throw new KeyNotFoundException();
        var w = state.Workflows.Single(w => w.Id == id && w.ActorUserId == user.UserId);
        if (w.Status is not ("ready" or "waiting_approval" or "watching")) throw new InvalidOperationException("Only unexecuted checkpoints or watches can be dismissed.");
        w.Status = "dismissed"; w.UpdatedUtc = DateTime.UtcNow;
        await Save(state, "workflow.dismissed", id.ToString(), "User declined remaining actions; previously completed module actions remain recorded", ct);
    }
    public async Task<BobWorkflow> CreateWorkflowAsync(BobWorkflowRequest request, CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct);
        if (!permissions.Contains("bob.write")) throw new MedInsights.Lib.ForbiddenAccessException("Bob write access is required.");
        if (request.IdempotencyKey.Length is < 1 or > 100 || request.Goal.Length is < 1 or > 240 || request.Steps.Count is < 1 or > 12) throw new ArgumentException("Provide a goal, stable key, and 1–12 explicit steps.");
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request.Goal, Steps = request.Steps.Select(s => new { s.CapabilityId, s.ConversationId, Input = s.Input.GetRawText() }) }))));
        var replay = state.Workflows.SingleOrDefault(w => w.ActorUserId == user.UserId && w.IdempotencyKey == request.IdempotencyKey);
        if (replay is not null)
        {
            if (replay.InputFingerprint != fingerprint) throw new InvalidOperationException("Idempotency key is bound to another workflow.");
            if (!(await VisibleWorkflows(state, ct)).Any(w => w.Id == replay.Id)) throw new MedInsights.Lib.ForbiddenAccessException("Workflow is unavailable.");
            return PublicWorkflow(replay);
        }
        var workflow = new BobWorkflow { ActorUserId = user.UserId, Goal = request.Goal, IdempotencyKey = request.IdempotencyKey, InputFingerprint = fingerprint };
        foreach (var step in request.Steps)
        {
            var c = registry.Get(step.CapabilityId); c.Validate(step.Input);
            await guard.RequireAsync(_providers[c.Id], ct);
            if (step.ConversationId == Guid.Empty || step.Rationale.Length is < 1 or > 400) throw new ArgumentException("Each step requires an explicit entity workspace and concise operational rationale.");
            foreach (var key in new[] { "leadId", "estimateId", "jobId" })
                if (step.Input.TryGetProperty(key, out var id) && id.GetGuid() != step.ConversationId) throw new ArgumentException("Step must target its source workspace.");
            // Caller cannot inject step status, approval, action ID, or result.
            workflow.Steps.Add(new() { CapabilityId = c.Id, ConversationId = step.ConversationId,
                Input = new BobContextMinimizer().Minimize(step.Input, 8000), Rationale = step.Rationale });
        }
        state.Workflows.Add(workflow);
        await Save(state, "workflow.created", workflow.Id.ToString(), "Validated explicit steps against current permission and action policy", ct);
        return PublicWorkflow(workflow);
    }
    public async Task<BobWorkflow> AdvanceWorkflowAsync(Guid id, bool approve, CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct);
        if (!permissions.Contains("bob.write")) throw new MedInsights.Lib.ForbiddenAccessException("Bob write access is required.");
        if (!(await VisibleWorkflows(state, ct)).Any(w => w.Id == id)) throw new KeyNotFoundException("Workflow not found.");
        var workflow = state.Workflows.Single(w => w.Id == id && w.ActorUserId == user.UserId);
        if (workflow.Status is "completed" or "dismissed") return PublicWorkflow(workflow);
        if (workflow.Status == "watching") throw new InvalidOperationException("Watches advance only through authorized source checks.");
        // An interrupted side effect has an uncertain outcome. Never replay it automatically.
        if (workflow.Status is "running" or "needs_review") throw new InvalidOperationException("This workflow needs source reconciliation before it can continue. Completed steps remain recorded.");
        var firstPending = workflow.Steps.FirstOrDefault(s => s.Status != "completed");
        if (approve && (workflow.Status != "waiting_approval" || firstPending?.Status != "waiting_approval")) throw new InvalidOperationException("There is no current approval checkpoint.");
        workflow.Status = "running"; workflow.UpdatedUtc = DateTime.UtcNow;
        // Optimistic commit is the exclusive execution claim. A competing request fails before any side effect.
        await Save(state, "workflow.claimed", id.ToString(), "Claimed the next workflow checkpoint", ct);
        var approvedCheckpoint = false;
        for (var index = 0; index < workflow.Steps.Count; index++)
        {
            var step = workflow.Steps[index]; if (step.Status == "completed") continue;
            try
            {
                var provider = _providers[step.CapabilityId]; var capability = registry.Get(step.CapabilityId);
                capability.Validate(step.Input);
                var requiresApproval = await guard.RequireAsync(provider, ct);
                // Only an already displayed checkpoint can be approved; future high-risk steps stop again.
                var approveThisStep = approve && !approvedCheckpoint && step.Status == "waiting_approval";
                if (requiresApproval && !approveThisStep)
                {
                    workflow.Status = "waiting_approval"; step.Status = "waiting_approval";
                    await PersistWorkflow(workflow, "workflow.waiting_approval", "Stopped before an action requiring human approval", ct);
                    return PublicWorkflow(workflow);
                }
                approvedCheckpoint |= approveThisStep;
                step.Status = "executing";
                await PersistWorkflow(workflow, "workflow.step_started", step.Rationale, ct);
                // Create the actor-scoped action workspace only after all guards have passed.
                var partition = MedInsights.Lib.EntityKeyPolicy.TenantUserPartition(user.TenantId, user.UserId);
                var row = MedInsights.Lib.EntityKeyPolicy.Row(step.ConversationId);
                if (await chats.GetAsync(partition, row, ct) is null)
                    await chats.SaveAsync(new MedInsights.Lib.Entities.Chat { Id = step.ConversationId, TenantId = user.TenantId, ActorUserId = user.UserId,
                        PartitionKey = partition, RowKey = row, Title = "Bob workflow", Mode = "general", StateJson = "{}", DateChatCreated = DateTime.UtcNow, DateChatUpdated = DateTime.UtcNow }, ct);
                var action = await operations.ProposeAsync(step.ConversationId, new ProposeBobActionDto { ToolKey = step.CapabilityId, Input = step.Input,
                    IdempotencyKey = $"workflow:{workflow.Id:N}:{index}" }, ct);
                step.ActionId = action.Id;
                await PersistWorkflow(workflow, "workflow.action_linked", "Linked the durable module action", ct);
                if (action.ConfirmationRequired)
                {
                    if (!approveThisStep) throw new InvalidOperationException("Policy changed. Review the action before continuing.");
                    await operations.ApproveAsync(action.Id, ct);
                    action = await operations.ExecuteAsync(action.Id, ct);
                }
                if (action.Status != "completed") throw new InvalidOperationException("The module action did not complete.");
                step.Status = "completed"; step.Failure = null;
                // A successful module result can carry the new optimistic version to later steps on that same entity.
                // Never refresh a version from an unrelated read to bypass a concurrent human edit.
                if (!string.IsNullOrWhiteSpace(action.ResultJson))
                {
                    using var result = JsonDocument.Parse(action.ResultJson);
                    var value = result.RootElement;
                    if (value.TryGetProperty("job", out var job)) value = job;
                    if (value.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String)
                        foreach (var future in workflow.Steps.Skip(index + 1).Where(s => s.ConversationId == step.ConversationId && s.CapabilityId.Split('.')[0] == step.CapabilityId.Split('.')[0] && s.Input.TryGetProperty("expectedVersion", out _)))
                        {
                            var input = System.Text.Json.Nodes.JsonNode.Parse(future.Input.GetRawText())!.AsObject(); input["expectedVersion"] = version.GetString();
                            future.Input = JsonSerializer.SerializeToElement(input, JobConfigurationService.Json);
                        }
                }
                await PersistWorkflow(workflow, "workflow.step_completed", step.Rationale, ct);
            }
            catch (Exception e)
            {
                workflow.Status = "needs_review";
                step.Failure = step.Status == "executing" ? "Outcome may be incomplete. Review the linked source action before retrying." : "Permission, policy, or module validation prevented this step.";
                workflow.Failure = $"{workflow.Steps.Count(s => s.Status == "completed")} of {workflow.Steps.Count} steps completed. {step.Failure}";
                if (step.Status != "completed") step.Status = "needs_review";
                await PersistWorkflow(workflow, "workflow.needs_review", "Workflow stopped; prior completed actions remain authoritative", CancellationToken.None);
                if (e is OperationCanceledException) throw;
                return PublicWorkflow(workflow);
            }
        }
        workflow.Status = "completed"; workflow.Failure = null;
        await PersistWorkflow(workflow, "workflow.completed", "All module actions completed successfully", ct);
        return PublicWorkflow(workflow);
    }
    private async Task PersistWorkflow(BobWorkflow workflow, string action, string rationale, CancellationToken ct)
    {
        // Reload the pointer after an action so unrelated attention/configuration commits are retained.
        var current = await store.ReadAsync(user.TenantId, ct);
        var existing = current.Workflows.Single(w => w.Id == workflow.Id && w.ActorUserId == user.UserId);
        if (existing.Status is "dismissed" or "completed" && existing.Status != workflow.Status) throw new InvalidOperationException("Workflow changed.");
        current.Workflows.Remove(existing); workflow.UpdatedUtc = DateTime.UtcNow; current.Workflows.Add(workflow);
        await Save(current, action, workflow.Id.ToString(), rationale, ct);
    }
}
