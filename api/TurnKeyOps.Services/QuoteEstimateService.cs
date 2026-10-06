using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using System.Globalization;
using MedInsights.Repositories.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed class QuoteEstimateService : IQuoteEstimateService
{
    internal const string ContainerName = "quote-estimate-packets";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IQuoteEstimateRepository _repository;
    private readonly LeadWorkflowEvents? _leadEvents;
    private readonly IQuoteRequestRepository _quoteRequests;
    private readonly IAzureBlobStorageService _blobStorage;
    private readonly IEstimateDefaultsService _defaults;
    private readonly IQuoteRequestTenantResolver _tenantResolver;
    private readonly IUserContext _userContext;
    private readonly ITenantSettingsRepository? _settings;
    private readonly ITenantMembershipRepository? _memberships;
    private readonly IJobRepository? _jobs;
    private readonly IOptions<QuoteRequestTenantOptions>? _tenantOptions;

    public QuoteEstimateService(
        IQuoteEstimateRepository repository,
        IQuoteRequestRepository quoteRequests,
        IAzureBlobStorageService blobStorage,
        IEstimateDefaultsService defaults,
        IQuoteRequestTenantResolver tenantResolver,
        IUserContext userContext,
        ITenantSettingsRepository? settings = null,
        ITenantMembershipRepository? memberships = null,
        IJobRepository? jobs = null,
        IOptions<QuoteRequestTenantOptions>? tenantOptions = null,
        LeadWorkflowEvents? leadEvents = null)
    {
        _repository = repository;
        _leadEvents = leadEvents;
        _quoteRequests = quoteRequests;
        _blobStorage = blobStorage;
        _defaults = defaults;
        _tenantResolver = tenantResolver;
        _userContext = userContext;
        _settings = settings;
        _memberships = memberships;
        _jobs = jobs;
        _tenantOptions = tenantOptions;
    }

    private async Task<bool> CanAccessQuoteAsync(Guid quoteRequestId, CancellationToken ct)
    {
        var operational = _settings is null ? null : await _settings.GetAsync(Partition(_userContext.TenantId), "SETTINGS|OPERATIONAL", ct);
        var configuredCarlZipf = _tenantOptions?.Value.Tenants.TryGetValue("carlzipf", out var tenant) == true && tenant.TenantId == _userContext.TenantId;
        var locksmith = configuredCarlZipf || (operational is not null && !operational.IsDeleted && LocksmithPolicy.IsConfigured(operational.ValuesJson));
        if (!locksmith) return true;
        if (!_userContext.IsAuthenticated || _memberships is null) return false;
        var member = await _memberships.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), _userContext.UserId, ct);
        if (member is null || member.TenantId != _userContext.TenantId || member.UserId != _userContext.UserId || member.IsDeleted || member.DateRemoved.HasValue ||
            !string.Equals(member.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase)) return false;
        if (member.IsOwner || TenantRoleCatalog.CanManageRoles(member.Role.Trim().ToLowerInvariant())) return true;
        if (string.Equals(member.Role, "contact", StringComparison.OrdinalIgnoreCase) || operational is null || operational.IsDeleted || _jobs is null) return false;
        var quote = await GetQuoteAsync(_userContext.TenantId, quoteRequestId, ct);
        if (quote is null || quote.TenantId != _userContext.TenantId || !LocksmithPolicy.CapabilitiesFor(operational.ValuesJson, member.Id).Contains(quote.PropertyType.ToLowerInvariant())) return false;
        var linkedJobs = (await _jobs.ListAsync(Partition(_userContext.TenantId), ct)).Where(job => !job.IsDeleted && job.QuoteRequestId == quoteRequestId);
        return linkedJobs.All(job => job.PartitionKey == Partition(_userContext.TenantId) &&
            (string.IsNullOrWhiteSpace(job.LocksmithJobType) || string.Equals(job.LocksmithJobType, quote.PropertyType, StringComparison.OrdinalIgnoreCase)) &&
            (!job.AssignedTechnicianMembershipId.HasValue || job.AssignedTechnicianMembershipId.Value == member.Id));
    }

    private async Task RequireQuoteAccessAsync(Guid quoteRequestId, CancellationToken ct)
    {
        if (!await CanAccessQuoteAsync(quoteRequestId, ct)) throw new ForbiddenAccessException("This quote is outside your job types or assigned work.");
    }

    public async Task<LocksmithQuoteContextDto> GetLocksmithContextAsync(CancellationToken ct = default)
    {
        var context = await LocksmithQuotePricing.RequireContextAsync(_settings, _memberships, _userContext, null, ct, allowOfficeAdmin: true);
        return LocksmithQuotePricing.Context(context.Settings, context.Capabilities);
    }

    public async Task<QuoteEstimateDto> ApproveLocksmithPricingAsync(Guid quoteRequestId, string? expectedVersion, CancellationToken ct = default)
    {
        if (_memberships is null || _settings is null) throw new UnauthorizedAccessException();
        var member = await _memberships.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), _userContext.UserId, ct);
        if (member is null || member.TenantId != _userContext.TenantId || member.UserId != _userContext.UserId || member.IsDeleted || member.DateRemoved.HasValue ||
            !string.Equals(member.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase) || (!member.IsOwner && !TenantRoleCatalog.CanManageRoles(member.Role)))
            throw new ForbiddenAccessException("Office admin access is required for pricing approval.");
        var entity = await GetEntityAsync(_userContext.TenantId, quoteRequestId, ct) ?? throw new ArgumentException("Estimate not found.");
        ValidateVersion(entity, expectedVersion);
        var packet = await LoadPayloadAsync(entity, ct);
        if (packet.LocksmithPricing is null || packet.Status is not ("draft" or "ready-to-send")) throw new ArgumentException("Only a locksmith draft can receive office pricing approval.");
        var settings = await _settings.GetAsync(Partition(_userContext.TenantId), "SETTINGS|OPERATIONAL", ct);
        if (settings is null || settings.IsDeleted || LocksmithQuotePricing.PolicyVersion(settings) != packet.LocksmithPricing.PolicyVersion)
            throw new ArgumentException("Pricing policy changed. Recalculate before office approval.");
        packet.LocksmithPricing.OfficeApprovedAtUtc = DateTime.UtcNow;
        packet.LocksmithPricing.OfficeApprovedBy = Actor();
        packet.Status = "ready-to-send";
        packet.SavedAtUtc = DateTime.UtcNow;
        return await PersistAsync(entity, packet, _userContext.TenantId, null, null, ct);
    }

    public async Task<IReadOnlyCollection<QuoteEstimateDto>> ListAsync(CancellationToken ct = default)
    {
        var entities = await _repository.ListAsync(Partition(_userContext.TenantId), ct);
        var results = new List<QuoteEstimateDto>(entities.Count);
        foreach (var entity in entities)
            if (await CanAccessQuoteAsync(entity.QuoteRequestId, ct)) results.Add(await LoadPayloadAsync(entity, ct));
        return results;
    }

    public async Task<QuoteEstimateDto?> GetAsync(Guid quoteRequestId, CancellationToken ct = default)
    {
        ValidateId(quoteRequestId);
        await RequireQuoteAccessAsync(quoteRequestId, ct);
        var entity = await GetEntityAsync(_userContext.TenantId, quoteRequestId, ct);
        return entity is null ? null : await LoadPayloadAsync(entity, ct);
    }

    public async Task<QuoteEstimateDto> PrepareFromLeadAsync(LeadDto lead, Guid requestId, CancellationToken ct = default)
    {
        if (lead.TenantId != _userContext.TenantId || lead.IntakeRequestId != requestId || !lead.CustomerId.HasValue)
            throw new ArgumentException("A linked tenant Lead and customer are required.");
        var quote = await GetQuoteAsync(_userContext.TenantId, requestId, ct) ?? throw new ArgumentException("Intake not found.");
        await RequireQuoteAccessAsync(requestId, ct);
        var existing = await GetEntityAsync(_userContext.TenantId, requestId, ct);
        if (existing is not null) return await LoadPayloadAsync(existing, ct);
        var packet = new QuoteEstimateDto { Id = requestId, QuoteRequestId = requestId, LeadId = lead.Id, CustomerId = lead.CustomerId,
            CustomerName = lead.ContactName, SiteName = lead.SiteAddress, ServiceSummary = lead.RequestedWork,
            Status = "draft", SavedAtUtc = DateTime.UtcNow, Notes = "Prepared from Lead; scope and pricing require estimator review.",
            CommercialSummary = "Not priced yet", ScopeLineItems = [lead.RequestedWork] };
        var saved = await PersistAsync(null, packet, _userContext.TenantId, null, null, ct);
        await UpdateQuoteAsync(quote, "estimate-drafted", "Complete estimate scope and pricing.", "Estimate prepared from Lead", ct);
        return saved;
    }

    public async Task<QuoteEstimateDto> SaveDraftAsync(
        Guid quoteRequestId,
        QuoteEstimateDraftInputDto input,
        CancellationToken ct = default)
    {
        ValidateId(quoteRequestId);
        var tenantId = _userContext.TenantId;
        var quote = await GetQuoteAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("The source quote request was not found.", nameof(quoteRequestId));
        await RequireQuoteAccessAsync(quoteRequestId, ct);
        EnsureQuoteTransition(quote, "estimate-drafted");
        var existing = await GetEntityAsync(tenantId, quoteRequestId, ct);
        ValidateVersion(existing, input.ExpectedVersion);
        var previous = existing is null ? null : await LoadPayloadAsync(existing, ct);
        if (previous?.Delivery?.Status == "approved")
            throw new ArgumentException("An approved estimate cannot be edited.");
        if (previous?.Delivery is not null)
            throw new ArgumentException("Create a new revision before editing a shared estimate.");

        var status = input.Status?.Trim().ToLowerInvariant();
        if (status is not ("draft" or "ready-to-send"))
            throw new ArgumentException("Draft status must be draft or ready-to-send.", nameof(input.Status));

        LocksmithPricingSnapshotDto? locksmithPricing = null;
        List<QuoteEstimateLocationDto> locations;
        QuoteEstimateTotalsDto totals;
        List<string> scope;
        List<string> assumptions;
        var operational = _settings is null ? null : await _settings.GetAsync(Partition(tenantId), "SETTINGS|OPERATIONAL", ct);
        if (input.Locksmith is not null)
        {
            if (!string.Equals(quote.PropertyType, input.Locksmith.JobType, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The quote job type must match its source request.");
            var context = await LocksmithQuotePricing.RequireContextAsync(_settings, _memberships, _userContext, input.Locksmith.JobType, ct, allowOfficeAdmin: true);
            locksmithPricing = LocksmithQuotePricing.Calculate(context.Settings, input.Locksmith);
            locations = [];
            totals = new() { MaterialCost = locksmithPricing.Lines.Sum(line => line.Total), LaborCost = Math.Round(locksmithPricing.LaborHours * locksmithPricing.LaborRatePerHour, 2, MidpointRounding.AwayFromZero), EstimatedTotal = locksmithPricing.Total };
            scope = locksmithPricing.Lines.Select(line => $"{line.OpeningName}: {line.Quantity} × {line.Name}").ToList();
            if (locksmithPricing.LaborHours > 0) scope.Add($"Labor: {locksmithPricing.LaborHours} hour(s)");
            assumptions = ["Prices use the saved tenant catalog and pricing policy. Technician observations require verification of product fit."];
            if (locksmithPricing.RequiresOfficeApproval) status = "draft";
        }
        else
        {
            if (operational is not null && !operational.IsDeleted && LocksmithPolicy.IsConfigured(operational.ValuesJson))
                throw new ArgumentException("Doors and locksmith quotes require locksmith scope and server catalog pricing.");
            locations = NormalizeLocations(input.Locations);
            var defaults = await _defaults.GetAsync();
            (totals, scope, assumptions) = Calculate(locations, defaults);
        }
        var now = DateTime.UtcNow;
        var packet = new QuoteEstimateDto
        {
            Id = existing?.Id ?? quoteRequestId,
            QuoteRequestId = quoteRequestId,
            RevisionNumber = previous?.RevisionNumber ?? 1,
            LeadId = previous?.LeadId, CustomerId = previous?.CustomerId,
            CustomerName = Required(input.CustomerName, nameof(input.CustomerName), 200),
            SiteName = Required(input.SiteName, nameof(input.SiteName), 300),
            ServiceSummary = Clean(input.ServiceSummary, 2000),
            VisitFindings = Clean(input.VisitFindings, 5000),
            ScopeLineItems = scope,
            Notes = Clean(input.Notes, 5000),
            Assumptions = assumptions,
            Status = status,
            CommercialSummary = locksmithPricing is not null ? $"{locksmithPricing.JobType} · {locksmithPricing.Lines.Count} line(s) · {locksmithPricing.Total.ToString("C", CultureInfo.GetCultureInfo("en-US"))}" : $"{locations.Count} location(s) · {totals.CubicYards:F1} CY · {totals.EstimatedTotal.ToString("C", CultureInfo.GetCultureInfo("en-US"))}",
            LocksmithPricing = locksmithPricing,
            Locations = locations,
            Totals = totals,
            SavedAtUtc = now,
            SentAtUtc = previous?.SentAtUtc,
            SentBy = previous?.SentBy,
            ExpiresAtUtc = previous?.ExpiresAtUtc,
            Delivery = previous?.Delivery,
            RevisionHistory = previous?.RevisionHistory ?? []
        };

        var saved = await PersistAsync(existing, packet, tenantId, null, null, ct);
        await UpdateQuoteAsync(quote, "estimate-drafted", "Estimate draft saved. Review totals and send when ready.", "Estimate draft saved", ct);
        return saved;
    }

    public async Task<QuoteEstimateDto> CreateRevisionAsync(
        Guid quoteRequestId,
        string? expectedVersion,
        CancellationToken ct = default)
    {
        await RequireQuoteAccessAsync(quoteRequestId, ct);
        var tenantId = _userContext.TenantId;
        var entity = await GetEntityAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("Estimate not found.", nameof(quoteRequestId));
        ValidateVersion(entity, expectedVersion);
        var packet = await LoadPayloadAsync(entity, ct);
        if (packet.Delivery?.Status == "approved")
            throw new ArgumentException("An approved estimate cannot be revised.");
        if (packet.Status != "sent" && packet.Delivery?.Status != "changes-requested")
            throw new ArgumentException("Only a sent estimate or requested change can create a revision.");

        packet.RevisionHistory.Add(ToRevision(packet));
        packet.DocumentHash = string.Empty;
        packet.ApprovalSignature = null;
        packet.RevisionNumber++;
        packet.Status = "draft";
        packet.SavedAtUtc = DateTime.UtcNow;
        packet.SentAtUtc = null;
        packet.SentBy = null;
        packet.ExpiresAtUtc = null;
        packet.Delivery = null;
        var quote = await GetQuoteAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("The source quote request was not found.", nameof(quoteRequestId));
        EnsureQuoteTransition(quote, "estimate-drafted");
        var saved = await PersistAsync(entity, packet, tenantId, null, null, ct);
        await UpdateQuoteAsync(quote, "estimate-drafted", $"Estimate revision v{packet.RevisionNumber} opened for review.", $"Estimate revision v{packet.RevisionNumber} created", ct);
        return saved;
    }

    public async Task<QuoteEstimateDto> SendAsync(
        Guid quoteRequestId,
        string? expectedVersion,
        string reviewBasePath,
        CancellationToken ct = default)
    {
        await RequireQuoteAccessAsync(quoteRequestId, ct);
        var tenantId = _userContext.TenantId;
        var entity = await GetEntityAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("Estimate not found.", nameof(quoteRequestId));
        ValidateVersion(entity, expectedVersion);
        var packet = await LoadPayloadAsync(entity, ct);
        if (packet.LocksmithPricing is not null)
        {
            var current = await LocksmithQuotePricing.RequireContextAsync(_settings, _memberships, _userContext, packet.LocksmithPricing.JobType, ct, allowOfficeAdmin: true);
            if (packet.LocksmithPricing.PolicyVersion != LocksmithQuotePricing.PolicyVersion(current.Settings))
                throw new ArgumentException("Pricing policy changed. Recalculate the draft before sharing it.");
            if (packet.LocksmithPricing.RequiresOfficeApproval && packet.LocksmithPricing.OfficeApprovedAtUtc is null)
                throw new ArgumentException("Office pricing approval is required before sharing this quote.");
        }
        else if (_settings is not null)
        {
            var settings = await _settings.GetAsync(Partition(tenantId), "SETTINGS|OPERATIONAL", ct);
            if (settings is not null && !settings.IsDeleted && LocksmithPolicy.IsConfigured(settings.ValuesJson))
                throw new ArgumentException("Recalculate this quote with doors and locksmith pricing before sharing.");
        }
        if (packet.Status != "ready-to-send")
            throw new ArgumentException("Move the estimate to ready-to-send before sending.");
        var quote = await GetQuoteAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("The source quote request was not found.", nameof(quoteRequestId));
        EnsureQuoteTransition(quote, "estimate-sent");
        if (!reviewBasePath.StartsWith("/", StringComparison.Ordinal) || reviewBasePath.Contains("//", StringComparison.Ordinal))
            throw new ArgumentException("The estimate review path is invalid.", nameof(reviewBasePath));

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var now = DateTime.UtcNow;
        packet.Status = "sent";
        packet.SavedAtUtc = now;
        packet.SentAtUtc = now;
        packet.SentBy = Actor();
        packet.ExpiresAtUtc = now.AddDays(30);
        packet.Delivery = new QuoteEstimateDeliveryDto
        {
            Status = "sent",
            ReviewUrl = $"{reviewBasePath}?token={token}",
            Email = quote.Email,
            Phone = quote.Phone,
            SentAtUtc = now
        };

        var saved = await PersistAsync(entity, packet, tenantId, HashToken(token), packet.ExpiresAtUtc, ct);
        await UpdateQuoteAsync(quote, "estimate-sent", $"Estimate sent by {packet.SentBy}.", "Estimate sent to customer", ct);
        return saved;
    }

    public async Task<QuoteEstimateDto?> GetPublicAsync(
        string tenantSlug,
        Guid quoteRequestId,
        string accessToken,
        CancellationToken ct = default)
    {
        var tenantId = _tenantResolver.Resolve(tenantSlug).TenantId;
        var entity = await GetEntityAsync(tenantId, quoteRequestId, ct);
        if (entity is null || !ValidToken(entity, accessToken)) return null;
        return await LoadPayloadAsync(entity, ct);
    }

    public Task<QuoteEstimateDto?> ApproveAsync(
        string tenantSlug,
        Guid quoteRequestId,
        QuoteEstimateDecisionDto decision,
        CancellationToken ct = default) =>
        DecideAsync(tenantSlug, quoteRequestId, decision, true, ct);

    public Task<QuoteEstimateDto?> RequestChangesAsync(
        string tenantSlug,
        Guid quoteRequestId,
        QuoteEstimateDecisionDto decision,
        CancellationToken ct = default) =>
        DecideAsync(tenantSlug, quoteRequestId, decision, false, ct);

    private async Task<QuoteEstimateDto?> DecideAsync(
        string tenantSlug,
        Guid quoteRequestId,
        QuoteEstimateDecisionDto decision,
        bool approve,
        CancellationToken ct)
    {
        var tenantId = _tenantResolver.Resolve(tenantSlug).TenantId;
        var entity = await GetEntityAsync(tenantId, quoteRequestId, ct);
        if (entity is null || !ValidToken(entity, decision.AccessToken)) return null;
        var packet = await LoadPayloadAsync(entity, ct);
        var target = approve ? "approved" : "changes-requested";
        if (packet.Delivery?.Status == target) return packet;
        if (packet.Delivery?.Status != "sent")
            throw new ArgumentException("This estimate already has a customer decision.");
        var note = Clean(decision.ResponseNote, 2000);
        if (!approve && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A change request note is required.", nameof(decision.ResponseNote));

        var now = DateTime.UtcNow;
        if (approve)
        {
            var signerName = Required(decision.SignerPrintedName ?? string.Empty, nameof(decision.SignerPrintedName), 200);
            if (signerName.Any(char.IsControl)) throw new ArgumentException("Signer name cannot contain control characters.");
            if (!decision.IntentToSign || decision.ConsentVersion != QuoteApprovalConsent.Version)
                throw new ArgumentException("Explicit electronic-signature intent and the current consent version are required.");
            if (decision.RevisionNumber != packet.RevisionNumber || string.IsNullOrWhiteSpace(decision.DocumentHash) ||
                !string.Equals(decision.DocumentHash, packet.DocumentHash, StringComparison.Ordinal))
                throw new ArgumentException("The quote revision changed. Reload and review the current document before signing.");
            packet.ApprovalSignature = new QuoteEstimateSignatureDto
            {
                SignerPrintedName = signerName, ConsentText = QuoteApprovalConsent.Text, ConsentVersion = QuoteApprovalConsent.Version,
                SignedAtUtc = now, RevisionNumber = packet.RevisionNumber, Total = packet.Totals.EstimatedTotal,
                DocumentHash = packet.DocumentHash, Method = "typed-name", QuoteRequestId = packet.QuoteRequestId
            };
        }
        packet.Delivery.Status = target;
        packet.Delivery.ResponseNote = approve ? null : note;
        packet.Delivery.ApprovedAtUtc = approve ? now : null;
        packet.Delivery.ChangesRequestedAtUtc = approve ? null : now;
        if (!approve) packet.Status = "ready-to-send";
        packet.SavedAtUtc = now;
        var quote = await GetQuoteAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("The source quote request was not found.", nameof(quoteRequestId));
        EnsureQuoteTransition(quote, approve ? "won" : "estimate-drafted");
        var saved = await PersistAsync(entity, packet, tenantId, entity.CustomerAccessTokenHash, entity.AccessTokenExpiresAtUtc, ct);
        await UpdateQuoteAsync(
            quote,
            approve ? "won" : "estimate-drafted",
            approve ? "Customer signed quote approval. Schedule and invoice separately when ready." : $"Customer requested estimate changes: {note}",
            approve ? "Quote approval electronically signed by customer" : "Customer requested estimate changes",
            ct,
            note);
        return saved;
    }

    private async Task<QuoteEstimateDto> PersistAsync(
        QuoteEstimate? existing,
        QuoteEstimateDto packet,
        Guid tenantId,
        string? tokenHash,
        DateTime? tokenExpiry,
        CancellationToken ct)
    {
        packet.DocumentHash = ComputeDocumentHash(packet);
        if (packet.ApprovalSignature is not null && packet.ApprovalSignature.DocumentHash != packet.DocumentHash)
            throw new ArgumentException("Signed quote contents cannot be changed.");
        var blobName = $"{tenantId:N}/{packet.QuoteRequestId:N}/v{packet.RevisionNumber}/{Guid.NewGuid():N}.json";
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(packet, JsonOptions));
        await _blobStorage.UploadAsync(ContainerName, blobName, content, "application/json", new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString("N"),
            ["quoteRequestId"] = packet.QuoteRequestId.ToString("N"),
            ["revision"] = packet.RevisionNumber.ToString(CultureInfo.InvariantCulture)
        }, ct);

        var entity = new QuoteEstimate
        {
            Id = existing?.Id ?? packet.QuoteRequestId,
            PartitionKey = Partition(tenantId),
            RowKey = Row(packet.QuoteRequestId),
            ETag = existing?.ETag ?? default,
            QuoteRequestId = packet.QuoteRequestId,
            RevisionNumber = packet.RevisionNumber,
            Status = packet.Status,
            DeliveryStatus = packet.Delivery?.Status,
            PayloadBlobName = blobName,
            CustomerAccessTokenHash = tokenHash,
            AccessTokenExpiresAtUtc = tokenExpiry,
            DateCreated = existing?.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };
        try
        {
            var saved = await _repository.SaveAsync(entity, ct);
            packet.Version = VersionOf(saved);
            if (existing is not null && !string.IsNullOrWhiteSpace(existing.PayloadBlobName))
            {
                try { await _blobStorage.DeleteIfExistsAsync(ContainerName, existing.PayloadBlobName, ct); }
                catch { /* Orphan reconciliation is safer than rolling back committed metadata. */ }
            }
            return packet;
        }
        catch
        {
            try { await _blobStorage.DeleteIfExistsAsync(ContainerName, blobName, CancellationToken.None); }
            catch { /* Preserve the repository failure. */ }
            throw;
        }
    }

    private async Task<QuoteEstimateDto> LoadPayloadAsync(QuoteEstimate entity, CancellationToken ct)
    {
        await using var stream = await _blobStorage.OpenReadAsync(ContainerName, entity.PayloadBlobName, ct);
        var packet = await JsonSerializer.DeserializeAsync<QuoteEstimateDto>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Estimate payload is invalid.");
        var computedHash = ComputeDocumentHash(packet);
        if ((!string.IsNullOrWhiteSpace(packet.DocumentHash) && packet.DocumentHash != computedHash) ||
            (packet.ApprovalSignature is not null && packet.ApprovalSignature.DocumentHash != computedHash))
            throw new InvalidOperationException("The stored quote document failed its integrity check.");
        packet.DocumentHash = computedHash;
        packet.Version = VersionOf(entity);
        return packet;
    }

    private async Task UpdateQuoteAsync(
        QuoteRequest quoteEntity,
        string status,
        string nextAction,
        string label,
        CancellationToken ct,
        string? note = null)
    {
        var quote = QuoteRequestMapper.ToDto(quoteEntity);
        if (quote.Status == "won" && status != "won") return;
        quote.Status = status;
        quote.NextAction = nextAction;
        quote.UpdatedAtUtc = DateTime.UtcNow;
        quote.Timeline.Add(new QuoteRequestTimelineEventDto
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Type = "estimate-updated",
            Actor = status == "won" || label.StartsWith("Customer", StringComparison.Ordinal) ? "Customer" : Actor(),
            Label = label,
            Note = note
        });
        var updated = QuoteRequestMapper.ToEntity(quote);
        updated.DateCreated = quoteEntity.DateCreated;
        updated.ETag = quoteEntity.ETag;
        await _quoteRequests.SaveAsync(updated, ct);
        if (_leadEvents is not null && label != "Estimate prepared from Lead") await _leadEvents.FromEstimateAsync(quoteEntity.TenantId, quoteEntity.Id, status, label, ct);
    }

    private static (QuoteEstimateTotalsDto Totals, List<string> Scope, List<string> Assumptions) Calculate(
        List<QuoteEstimateLocationDto> locations,
        EstimateDefaultsDto defaults)
    {
        double sqft = 0, yards = 0, forms = 0, rebar = 0;
        decimal materials = 0, labor = 0;
        var scope = new List<string>();
        var laborRatePerSquareFoot = defaults.LaborRatePerHour *
            Math.Max(1m, defaults.PourHoursPer100SqFt + defaults.FinishHoursPer100SqFt) / 100m;
        foreach (var location in locations)
        {
            var area = location.LengthFeet * location.WidthFeet;
            var volume = Math.Ceiling(((area * location.DepthInches / 12d / 27d) * (1d + location.WastePercent / 100d)) * 10d) / 10d;
            var locationForms = Math.Ceiling(4d * Math.Sqrt(area) * 1.1d);
            var locationRebar = Math.Ceiling(Math.Ceiling(Math.Sqrt(area)) * Math.Sqrt(area) * 2d * 1.1d);
            var locationMaterials = Round((decimal)volume * defaults.ConcreteCostPerYard + (decimal)locationRebar * defaults.RebarCostPerFoot);
            var locationLabor = Round((decimal)area * laborRatePerSquareFoot);
            location.SquareFeet = Math.Round(area, 2);
            location.CubicYards = Math.Round(volume, 2);
            location.FormLinearFeet = Math.Round(locationForms, 2);
            location.RebarLinearFeet = Math.Round(locationRebar, 2);
            location.MaterialCost = locationMaterials;
            location.LaborCost = locationLabor;
            location.EstimatedTotal = Round(locationMaterials + locationLabor);
            sqft += area; yards += volume; forms += locationForms; rebar += locationRebar;
            materials += locationMaterials; labor += locationLabor;
            scope.Add($"{location.Name}: {location.LengthFeet:F0} ft x {location.WidthFeet:F0} ft x {location.DepthInches:F0} in, {volume:F1} CY, {(locationMaterials + locationLabor).ToString("C", CultureInfo.GetCultureInfo("en-US"))}");
        }
        var total = Round(materials + labor);
        scope.Add($"{yards:F1} total CY");
        scope.Add($"{forms:F0} LF forms");
        scope.Add($"{rebar:F0} LF rebar");
        scope.Add($"Materials {materials.ToString("C", CultureInfo.GetCultureInfo("en-US"))}");
        scope.Add($"Labor {labor.ToString("C", CultureInfo.GetCultureInfo("en-US"))}");
        scope.Add($"Estimated total {total.ToString("C", CultureInfo.GetCultureInfo("en-US"))}");
        return (new QuoteEstimateTotalsDto
        {
            SquareFeet = Math.Round(sqft, 2), CubicYards = Math.Round(yards, 2),
            FormLinearFeet = Math.Round(forms, 2), RebarLinearFeet = Math.Round(rebar, 2),
            MaterialCost = Round(materials), LaborCost = Round(labor), EstimatedTotal = total
        }, scope,
        [
            $"Concrete cost: {defaults.ConcreteCostPerYard.ToString("C", CultureInfo.GetCultureInfo("en-US"))} / yard",
            $"Labor model: {laborRatePerSquareFoot.ToString("C", CultureInfo.GetCultureInfo("en-US"))} / sqft",
            $"Rebar: {defaults.RebarCostPerFoot.ToString("C", CultureInfo.GetCultureInfo("en-US"))} / LF"
        ]);
    }

    private static List<QuoteEstimateLocationDto> NormalizeLocations(IEnumerable<QuoteEstimateLocationDto> values)
    {
        var results = values.Take(51).Select((value, index) => new QuoteEstimateLocationDto
        {
            Id = string.IsNullOrWhiteSpace(value.Id) ? $"location-{index + 1}" : Clean(value.Id, 100),
            Name = Required(value.Name, nameof(value.Name), 200),
            LengthFeet = Range(value.LengthFeet, 0.1, 10000, nameof(value.LengthFeet)),
            WidthFeet = Range(value.WidthFeet, 0.1, 10000, nameof(value.WidthFeet)),
            DepthInches = Range(value.DepthInches, 0.1, 120, nameof(value.DepthInches)),
            WastePercent = Range(value.WastePercent, 0, 100, nameof(value.WastePercent)),
            NumberOfPours = value.NumberOfPours is >= 1 and <= 100 ? value.NumberOfPours : throw new ArgumentException("Number of pours must be between 1 and 100.")
        }).ToList();
        if (results.Count == 0) throw new ArgumentException("At least one estimate location is required.", nameof(values));
        if (results.Count > 50) throw new ArgumentException("An estimate cannot contain more than 50 locations.", nameof(values));
        return results;
    }

    private static QuoteEstimateRevisionDto ToRevision(QuoteEstimateDto packet) => new()
    {
        DocumentHash = packet.DocumentHash, ApprovalSignature = packet.ApprovalSignature,
        RevisionNumber = packet.RevisionNumber, CustomerName = packet.CustomerName, SiteName = packet.SiteName,
        ServiceSummary = packet.ServiceSummary, VisitFindings = packet.VisitFindings,
        ScopeLineItems = [.. packet.ScopeLineItems], Notes = packet.Notes, Assumptions = [.. packet.Assumptions],
        Status = packet.Status, CommercialSummary = packet.CommercialSummary,
        Locations = packet.Locations.Select(item => new QuoteEstimateLocationDto
        {
            Id = item.Id, Name = item.Name, LengthFeet = item.LengthFeet, WidthFeet = item.WidthFeet,
            DepthInches = item.DepthInches, WastePercent = item.WastePercent, NumberOfPours = item.NumberOfPours,
            SquareFeet = item.SquareFeet, CubicYards = item.CubicYards, FormLinearFeet = item.FormLinearFeet,
            RebarLinearFeet = item.RebarLinearFeet, MaterialCost = item.MaterialCost,
            LaborCost = item.LaborCost, EstimatedTotal = item.EstimatedTotal
        }).ToList(),
        LocksmithPricing = packet.LocksmithPricing, Totals = packet.Totals, SavedAtUtc = packet.SavedAtUtc, SentAtUtc = packet.SentAtUtc, SentBy = packet.SentBy
    };

    private static string ComputeDocumentHash(QuoteEstimateDto packet)
    {
        // Hash only the issued document, excluding workflow state, access tokens, and signature evidence.
        var document = new
        {
            packet.Id, packet.QuoteRequestId, packet.RevisionNumber, packet.CustomerName, packet.SiteName,
            packet.ServiceSummary, packet.VisitFindings, packet.ScopeLineItems, packet.Notes, packet.Assumptions,
            packet.CommercialSummary, packet.Locations, packet.Totals, packet.LocksmithPricing,
            packet.SentAtUtc, packet.ExpiresAtUtc
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions))).ToLowerInvariant();
    }

    private static string VersionOf(QuoteEstimate entity) => !string.IsNullOrWhiteSpace(entity.ETag.ToString())
        ? entity.ETag.ToString() : entity.DateUpdated.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);

    private static void ValidateVersion(QuoteEstimate? existing, string? expected)
    {
        if (existing is null) return;
        if (string.IsNullOrWhiteSpace(expected) || expected != VersionOf(existing))
            throw new ArgumentException("The estimate changed after it was loaded. Refresh and try again.", nameof(expected));
    }

    private static void EnsureQuoteTransition(QuoteRequest entity, string target)
    {
        var current = QuoteRequestMapper.ToDto(entity).Status;
        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return;
        var allowed = target switch
        {
            "estimate-drafted" => current is "qualified" or "contacted" or "inspection-scheduled" or "estimate-sent",
            "estimate-sent" => current is "estimate-drafted",
            "won" => current is "estimate-sent",
            _ => false
        };
        if (!allowed) throw new ArgumentException($"A quote request cannot move from {current} to {target}.");
    }

    private static bool ValidToken(QuoteEstimate entity, string token)
    {
        if (string.IsNullOrWhiteSpace(entity.CustomerAccessTokenHash) || string.IsNullOrWhiteSpace(token) ||
            entity.AccessTokenExpiresAtUtc is null || entity.AccessTokenExpiresAtUtc <= DateTime.UtcNow) return false;
        var expected = Encoding.UTF8.GetBytes(entity.CustomerAccessTokenHash);
        var actual = Encoding.UTF8.GetBytes(HashToken(token));
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private Task<QuoteEstimate?> GetEntityAsync(Guid tenantId, Guid requestId, CancellationToken ct) =>
        _repository.GetAsync(Partition(tenantId), Row(requestId), ct);
    private async Task<QuoteRequest?> GetQuoteAsync(Guid tenantId, Guid requestId, CancellationToken ct) =>
        await _quoteRequests.GetAsync(Partition(tenantId), Row(requestId), ct) is { IsDeleted: false } quote ? quote : null;
    private string Actor() => string.Join(' ', new[] { _userContext.FirstName, _userContext.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } actor ? actor : "Tenant Admin";
    private static string Required(string? value, string name, int max) => !string.IsNullOrWhiteSpace(value) ? Clean(value, max) : throw new ArgumentException($"{name} is required.", name);
    private static string Clean(string? value, int max) { var clean = value?.Trim() ?? string.Empty; if (clean.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters."); return clean; }
    private static double Range(double value, double min, double max, string name) => double.IsFinite(value) && value >= min && value <= max ? value : throw new ArgumentException($"{name} must be between {min} and {max}.", name);
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw new ArgumentException("A quote request id is required.", nameof(id)); }
    private static string Partition(Guid tenantId) => RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private static string Row(Guid id) => RepositoryKeyHelper.ToRowKey(id);
}
