using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed partial class QuoteEstimateService
{
    internal async Task<QuoteEstimateDto> PortalPacketAsync(PortalActor actor, Guid id, CancellationToken ct)
    {
        var entity = await GetEntityAsync(actor.TenantId, id, ct) ?? throw new KeyNotFoundException();
        if (!entity.PayloadBlobName.StartsWith($"{actor.TenantId:N}/{id:N}/", StringComparison.Ordinal)) throw new KeyNotFoundException();
        var packet = await LoadPayloadAsync(entity, ct);
        if (packet.Document is null || packet.SentAtUtc is null || !PortalAccessService.Allows(actor, "estimate", id, packet.CustomerId, packet.Document.SiteId)) throw new KeyNotFoundException();
        return packet;
    }
    public async Task<object> PortalProposalAsync(PortalActor actor, Guid id, CancellationToken ct)
    {
        var p = await PortalPacketAsync(actor, id, ct);
        // Explicit allowlist: no delivery link/token, internal attribution, calculations or source DTO.
        return new {
            id, p.RevisionNumber, p.DocumentHash, p.ServiceSummary, p.CustomerName, p.SiteName, p.ExpiresAtUtc,
            status = p.Outcome ?? p.Delivery?.Status ?? "unavailable", p.AcceptedOptionIds,
            scope = p.Document!.Scope, terms = p.Document.Terms, exclusions = p.Document.Exclusions, timing = p.Document.Timing,
            total = p.Totals.EstimatedTotal,
            options = p.Pricing?.Options.Select(o => new { o.Id, o.Name, o.Required, o.ExclusiveGroup, o.Total,
                lines = o.Lines.Select(l => new { l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Total }) }),
            files = p.Document.Attachments.Select(f => new PortalFile(f.Id, f.Name, f.ContentType)),
            signature = p.ApprovalSignature is null ? null : new { p.ApprovalSignature.SignerPrintedName, p.ApprovalSignature.SignedAtUtc, p.ApprovalSignature.RevisionNumber, p.ApprovalSignature.DocumentHash, p.ApprovalSignature.ConsentText, p.ApprovalSignature.CustomerComment },
            consentVersion = QuoteApprovalConsent.Version, consentText = QuoteApprovalConsent.Text
        };
    }
    public async Task<object> PortalDecideAsync(PortalActor actor, string slug, Guid id, QuoteEstimateDecisionDto decision, bool approve, CancellationToken ct)
    {
        await PortalPacketAsync(actor, id, ct);
        await DecideAsync(slug, id, decision, approve, ct, actor);
        return await PortalProposalAsync(actor, id, ct);
    }
    public async Task<QuoteRequestAttachmentDownload> PortalFileAsync(PortalActor actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var packet = await PortalPacketAsync(actor, id, ct);
        var f = packet.Document!.Attachments.SingleOrDefault(f => f.Id == fileId) ?? throw new KeyNotFoundException();
        if (!f.BlobName.StartsWith($"{actor.TenantId:N}/{id:N}/attachments/", StringComparison.Ordinal)) throw new KeyNotFoundException();
        return new(f.Name, f.ContentType, f.SizeBytes, await _blobStorage.OpenReadAsync(ContainerName, f.BlobName, ct));
    }
}
