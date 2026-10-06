using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Services;

// Trusted downstream events have their own scope; they cannot be called by Bob or a browser with an arbitrary tenant.
public sealed class LeadWorkflowEvents(ILeadRepository leads, LeadActivityStore activityStore)
{
    public async Task FromEstimateAsync(Guid tenantId, Guid intakeId, string status, string label, CancellationToken ct)
    {
        var partition = RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
        var entity = await leads.GetAsync(partition, RepositoryKeyHelper.ToRowKey(intakeId), ct);
        if (entity is null || entity.IsDeleted || entity.PartitionKey != partition || entity.Data.TenantId != tenantId) return;
        await activityStore.HydrateAsync(entity, ct);
        var data = entity.Data;
        // Intake IDs are deterministic; explicit manual links also use the Lead ID.
        if (data.IntakeRequestId != intakeId) return;
        var stage = LeadStages.FromIntake(status);
        if (stage is not ("ESTIMATING" or "PROPOSAL" or "WON")) return;
        if (data.Stage == stage) return;
        data.Activity.Add(new() { Type = "estimate", Actor = stage == "WON" ? "Customer" : "Estimate workflow", Text = label, RelatedId = intakeId });
        if (!LeadStages.IsClosed(data.Stage))
        {
            data.Stage = stage; data.NextAction = LeadStages.NextAction(stage);
            if (stage == "WON") { data.WonAtUtc = DateTime.UtcNow; data.CloseReason = "Customer approved estimate"; }
        }
        data.Version = Guid.NewGuid().ToString("N");
        entity.DateUpdated = data.UpdatedAtUtc = DateTime.UtcNow;
        await activityStore.CommitAsync(entity, false, ct);
    }
}
