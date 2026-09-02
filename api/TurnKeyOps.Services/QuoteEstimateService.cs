using System.Globalization;
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
    private readonly IQuoteRequestRepository _quoteRequests;
    private readonly IAzureBlobStorageService _blobStorage;
    private readonly IEstimateDefaultsService _defaults;
    private readonly IQuoteRequestTenantResolver _tenantResolver;
    private readonly IUserContext _userContext;

    public QuoteEstimateService(
        IQuoteEstimateRepository repository,
        IQuoteRequestRepository quoteRequests,
        IAzureBlobStorageService blobStorage,
        IEstimateDefaultsService defaults,
        IQuoteRequestTenantResolver tenantResolver,
        IUserContext userContext)
    {
        _repository = repository;
        _quoteRequests = quoteRequests;
        _blobStorage = blobStorage;
        _defaults = defaults;
        _tenantResolver = tenantResolver;
        _userContext = userContext;
    }

    public async Task<IReadOnlyCollection<QuoteEstimateDto>> ListAsync(CancellationToken ct = default)
    {
        var entities = await _repository.ListAsync(Partition(_userContext.TenantId), ct);
        var results = new List<QuoteEstimateDto>(entities.Count);
        foreach (var entity in entities) results.Add(await LoadPayloadAsync(entity, ct));
        return results;
    }

    public async Task<QuoteEstimateDto?> GetAsync(Guid quoteRequestId, CancellationToken ct = default)
    {
        ValidateId(quoteRequestId);
        var entity = await GetEntityAsync(_userContext.TenantId, quoteRequestId, ct);
        return entity is null ? null : await LoadPayloadAsync(entity, ct);
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
        EnsureQuoteTransition(quote, "estimate-drafted");
        var existing = await GetEntityAsync(tenantId, quoteRequestId, ct);
        ValidateVersion(existing, input.ExpectedVersion);
        var previous = existing is null ? null : await LoadPayloadAsync(existing, ct);
        if (previous?.Delivery?.Status == "approved")
            throw new ArgumentException("An approved estimate cannot be edited.");

        var status = input.Status?.Trim().ToLowerInvariant();
        if (status is not ("draft" or "ready-to-send"))
            throw new ArgumentException("Draft status must be draft or ready-to-send.", nameof(input.Status));

        var locations = NormalizeLocations(input.Locations);
        var defaults = await _defaults.GetAsync();
        var (totals, scope, assumptions) = Calculate(locations, defaults);
        var now = DateTime.UtcNow;
        var packet = new QuoteEstimateDto
        {
            Id = existing?.Id ?? quoteRequestId,
            QuoteRequestId = quoteRequestId,
            RevisionNumber = previous?.RevisionNumber ?? 1,
            CustomerName = Required(input.CustomerName, nameof(input.CustomerName), 200),
            SiteName = Required(input.SiteName, nameof(input.SiteName), 300),
            ServiceSummary = Clean(input.ServiceSummary, 2000),
            VisitFindings = Clean(input.VisitFindings, 5000),
            ScopeLineItems = scope,
            Notes = Clean(input.Notes, 5000),
            Assumptions = assumptions,
            Status = status,
            CommercialSummary = $"{locations.Count} location(s) · {totals.CubicYards:F1} CY · {totals.EstimatedTotal.ToString("C", CultureInfo.GetCultureInfo("en-US"))}",
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
        var tenantId = _userContext.TenantId;
        var entity = await GetEntityAsync(tenantId, quoteRequestId, ct)
            ?? throw new ArgumentException("Estimate not found.", nameof(quoteRequestId));
        ValidateVersion(entity, expectedVersion);
        var packet = await LoadPayloadAsync(entity, ct);
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
            approve ? "Customer approved the estimate. Draft invoice is ready for billing review." : $"Customer requested estimate changes: {note}",
            approve ? "Estimate approved by customer" : "Customer requested estimate changes",
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
            packet.Version = saved.ETag.ToString();
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
        packet.Version = entity.ETag.ToString();
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
        Totals = packet.Totals, SavedAtUtc = packet.SavedAtUtc, SentAtUtc = packet.SentAtUtc, SentBy = packet.SentBy
    };

    private static void ValidateVersion(QuoteEstimate? existing, string? expected)
    {
        if (existing is null) return;
        if (string.IsNullOrWhiteSpace(expected) || expected != existing.ETag.ToString())
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
