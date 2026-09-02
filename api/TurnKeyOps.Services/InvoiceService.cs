using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed class InvoiceService : IInvoiceService
{
    private const decimal MoneyTolerance = 0.01m;
    private static readonly TimeSpan ReminderCooldown = TimeSpan.FromHours(24);

    private readonly IInvoiceRepository _repo;
    private readonly IInvoiceLineItemRepository _lineItemRepo;
    private readonly IEstimateRepository _estimateRepo;
    private readonly IEstimateLineItemRepository _estimateLineItemRepo;
    private readonly IQuoteEstimateService _quoteEstimates;
    private readonly IInvoiceWorkflowPayloadStore _payloadStore;
    private readonly IUserContext _userContext;

    public InvoiceService(
        IInvoiceRepository repo,
        IInvoiceLineItemRepository lineItemRepo,
        IEstimateRepository estimateRepo,
        IEstimateLineItemRepository estimateLineItemRepo,
        IQuoteEstimateService quoteEstimates,
        IInvoiceWorkflowPayloadStore payloadStore,
        IUserContext userContext)
    {
        _repo = repo;
        _lineItemRepo = lineItemRepo;
        _estimateRepo = estimateRepo;
        _estimateLineItemRepo = estimateLineItemRepo;
        _quoteEstimates = quoteEstimates;
        _payloadStore = payloadStore;
        _userContext = userContext;
    }

    private string Partition(Guid tenantId) => RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private string Row(Guid id) => RepositoryKeyHelper.ToRowKey(id);

    public async Task<InvoiceDto?> GetAsync(Guid id)
    {
        ValidateId(id);
        var entity = await GetEntityAsync(_userContext.TenantId, id, default);
        return entity is null ? null : await HydrateAsync(entity, _userContext.TenantId, default);
    }

    public async Task<(IEnumerable<InvoiceDto> Items, string? ContinuationToken)> GetPagedAsync(
        int pageSize,
        string? continuationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = int.TryParse(continuationToken, out var parsed) && parsed >= 0 ? parsed : 0;
        var entities = await _repo.ListAsync(Partition(_userContext.TenantId));
        var page = entities.Skip(offset).Take(pageSize).ToArray();
        var items = new List<InvoiceDto>(page.Length);
        foreach (var entity in page) items.Add(await HydrateAsync(entity, _userContext.TenantId, default));
        var token = offset + page.Length < entities.Count ? (offset + page.Length).ToString() : null;
        return (items, token);
    }

    public async Task<InvoiceDto> AddAsync(InvoiceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = _userContext.TenantId;
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        if (await GetEntityAsync(tenantId, dto.Id, default) is not null)
            throw new ArgumentException("Invoice already exists.", nameof(dto.Id));

        dto.InvoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber)
            ? $"INV-{DateTime.UtcNow:yyyyMMdd}-{dto.Id.ToString()[..8].ToUpperInvariant()}"
            : Clean(dto.InvoiceNumber, 80);
        dto.Status = InvoiceStatus.Draft;
        dto.IssueDate = NormalizeUtc(dto.IssueDate == default ? DateTime.UtcNow : dto.IssueDate);
        dto.DueDate = NormalizeUtc(dto.DueDate == default ? dto.IssueDate.AddDays(30) : dto.DueDate);
        if (dto.DueDate < dto.IssueDate) throw new ArgumentException("Due date cannot precede issue date.");
        NormalizeLineItems(dto);
        ApplyFinancials(dto, amountPaid: 0m);

        var now = DateTime.UtcNow;
        var payload = PayloadFrom(dto);
        payload.AuditEvents.Add(Audit("invoice_created", "Invoice created from server-calculated line items.", Actor(), now));
        var entity = InvoiceMapper.ToEntity(dto, Partition(tenantId));
        entity.DateCreated = now;
        entity.DateUpdated = now;
        await SaveLineItemsAsync(tenantId, dto.Id, dto.LineItems, default);
        var saved = await PersistAsync(entity, payload, tenantId, default);
        return await HydrateAsync(saved, tenantId, default);
    }

    public async Task<InvoiceDto> UpdateAsync(InvoiceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = _userContext.TenantId;
        var existing = await GetEntityAsync(tenantId, dto.Id, default)
            ?? throw new ArgumentException("Invoice not found.", nameof(dto.Id));
        ValidateVersion(existing, dto.Version);
        if (existing.Status != InvoiceStatus.Draft)
            throw new ArgumentException("Only draft invoices can be edited.");

        var payload = await _payloadStore.LoadAsync(existing.WorkflowPayloadBlobName);
        if (payload.Payments.Count != 0) throw new ArgumentException("An invoice with payment activity cannot be edited.");
        dto.Status = existing.Status;
        dto.InvoiceNumber = existing.InvoiceNumber;
        dto.IssueDate = existing.IssueDate;
        dto.DueDate = NormalizeUtc(dto.DueDate);
        NormalizeLineItems(dto);
        ApplyFinancials(dto, 0m);
        MergeEditablePayload(payload, dto);
        payload.AuditEvents.Add(Audit("invoice_updated", "Draft invoice details were updated.", Actor(), DateTime.UtcNow));

        var entity = InvoiceMapper.ToEntity(dto, existing.PartitionKey);
        entity.ETag = existing.ETag;
        entity.DateCreated = existing.DateCreated;
        entity.WorkflowPayloadBlobName = existing.WorkflowPayloadBlobName;
        await SaveLineItemsAsync(tenantId, dto.Id, dto.LineItems, default);
        var saved = await PersistAsync(entity, payload, tenantId, default);
        return await HydrateAsync(saved, tenantId, default);
    }

    public async Task<InvoiceDto> CreateFromEstimateAsync(Guid estimateId)
    {
        ValidateId(estimateId);
        var tenantId = _userContext.TenantId;
        var estimate = await _estimateRepo.GetAsync(Partition(tenantId), Row(estimateId))
            ?? throw new ArgumentException("Estimate not found.", nameof(estimateId));
        if (estimate.Status != EstimateStatus.Awarded)
            throw new ArgumentException("Only an awarded estimate can create an invoice.", nameof(estimateId));

        var linePartition = RepositoryKeyHelper.ToTenantEstimatePartitionKey(tenantId, estimateId);
        var sourceLines = (await _estimateLineItemRepo.GetAllAsync(false, false))
            .Where(item => item.PartitionKey == linePartition && !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ToArray();
        var lines = sourceLines.Select(item => new InvoiceLineItemDto
        {
            SortOrder = item.SortOrder,
            Description = item.Description,
            Quantity = (decimal)item.Quantity,
            Unit = item.Unit,
            UnitPrice = item.UnitPrice
        }).ToList();
        if (lines.Count == 0)
        {
            lines.Add(new InvoiceLineItemDto
            {
                Description = $"Approved estimate {estimate.EstimateNumber}",
                Quantity = 1m,
                UnitPrice = estimate.Subtotal
            });
        }

        return await AddAsync(new InvoiceDto
        {
            CustomerId = estimate.CustomerId,
            CustomerName = estimate.CustomerName,
            JobId = estimate.JobId,
            JobName = estimate.JobName,
            EstimateId = estimateId,
            TaxRate = estimate.TaxRate,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Notes = $"Invoice from awarded estimate {estimate.EstimateNumber}",
            LineItems = lines
        });
    }

    public async Task<IReadOnlyCollection<InvoiceDto>> SyncApprovedEstimatesAsync(CancellationToken ct = default)
    {
        var tenantId = _userContext.TenantId;
        var approved = (await _quoteEstimates.ListAsync(ct))
            .Where(item => string.Equals(item.Delivery?.Status, "approved", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SavedAtUtc)
            .ToArray();
        var results = new List<InvoiceDto>(approved.Length);
        foreach (var estimate in approved)
        {
            var existing = await GetEntityAsync(tenantId, estimate.QuoteRequestId, ct);
            if (existing is not null)
            {
                results.Add(await HydrateAsync(existing, tenantId, ct));
                continue;
            }

            var lines = new List<InvoiceLineItemDto>();
            if (estimate.Totals.MaterialCost > 0)
                lines.Add(new() { Description = "Approved estimate materials", Quantity = 1m, UnitPrice = estimate.Totals.MaterialCost });
            if (estimate.Totals.LaborCost > 0)
                lines.Add(new() { SortOrder = lines.Count, Description = "Approved estimate labor", Quantity = 1m, UnitPrice = estimate.Totals.LaborCost });
            if (lines.Count == 0)
                lines.Add(new() { Description = $"Approved estimate revision {estimate.RevisionNumber}", Quantity = 1m, UnitPrice = estimate.Totals.EstimatedTotal });

            var invoice = await AddAsync(new InvoiceDto
            {
                Id = estimate.QuoteRequestId,
                QuoteRequestId = estimate.QuoteRequestId,
                EstimateRevisionNumber = estimate.RevisionNumber,
                CustomerName = estimate.CustomerName,
                SiteName = estimate.SiteName,
                ServiceSummary = estimate.ServiceSummary,
                CustomerEmail = estimate.Delivery?.Email,
                CustomerPhone = estimate.Delivery?.Phone,
                ReviewUrl = estimate.Delivery?.ReviewUrl,
                ScopeLineItems = [.. estimate.ScopeLineItems],
                TaxRate = 0m,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                Notes = $"Generated from approved estimate revision {estimate.RevisionNumber}.",
                LineItems = lines
            });
            results.Add(invoice);
        }
        return results;
    }

    public async Task<InvoiceDto> SendAsync(Guid id, string? expectedVersion, CancellationToken ct = default)
    {
        var tenantId = _userContext.TenantId;
        var (entity, payload) = await LoadForMutationAsync(tenantId, id, expectedVersion, ct);
        if (entity.Status != InvoiceStatus.Draft)
        {
            if (payload.SentAtUtc.HasValue) return await HydrateAsync(entity, tenantId, ct);
            throw new ArgumentException("Only a draft invoice can be sent.");
        }
        if (string.IsNullOrWhiteSpace(payload.CustomerEmail) && string.IsNullOrWhiteSpace(payload.CustomerPhone))
            throw new ArgumentException("A customer email or phone is required before sending an invoice.");

        var now = DateTime.UtcNow;
        entity.Status = InvoiceStatus.Sent;
        payload.SentAtUtc = now;
        payload.AuditEvents.Add(Audit("invoice_sent", "Invoice moved to the customer delivery workflow.", Actor(), now));
        var saved = await PersistAsync(entity, payload, tenantId, ct);
        return await HydrateAsync(saved, tenantId, ct);
    }

    public Task<InvoiceDto> RecordPaymentAsync(Guid id, InvoicePaymentInputDto input, CancellationToken ct = default) =>
        ApplyPaymentEventAsync(_userContext.TenantId, id, "payment", input, Actor(), ct);

    public Task<InvoiceDto> RecordRefundAsync(Guid id, InvoicePaymentInputDto input, CancellationToken ct = default) =>
        ApplyPaymentEventAsync(_userContext.TenantId, id, "refund", input, Actor(), ct);

    public async Task<InvoiceDto> ReconcileProviderEventAsync(
        Guid tenantId,
        Guid id,
        InvoicePaymentInputDto input,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant correlation is required.", nameof(tenantId));
        var kind = string.Equals(input.Kind, "refund", StringComparison.OrdinalIgnoreCase) ? "refund" : "payment";
        return await ApplyPaymentEventAsync(tenantId, id, kind, input, input.Provider ?? "Payment provider", ct);
    }

    public async Task<InvoiceDto> RecordReminderAsync(Guid id, InvoiceReminderInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = _userContext.TenantId;
        var (entity, payload) = await LoadForMutationAsync(tenantId, id, input.ExpectedVersion, ct);
        ApplyFinancialState(entity, payload);
        if (entity.Status is InvoiceStatus.Draft or InvoiceStatus.Paid or InvoiceStatus.Void || entity.BalanceDue <= MoneyTolerance)
            throw new ArgumentException("This invoice is not eligible for a reminder.");

        var channel = input.Channel.Trim().ToLowerInvariant();
        if (channel is not ("email" or "sms")) throw new ArgumentException("Reminder channel must be email or sms.");
        var recipient = Clean(input.Recipient, 320);
        if (string.IsNullOrWhiteSpace(recipient))
            recipient = channel == "email" ? payload.CustomerEmail ?? string.Empty : payload.CustomerPhone ?? string.Empty;
        if (string.IsNullOrWhiteSpace(recipient)) throw new ArgumentException("The reminder recipient is required.");
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? $"manual:{Guid.NewGuid():N}" : Clean(input.IdempotencyKey, 200);
        if (payload.Reminders.Any(item => item.IdempotencyKey == key)) return await HydrateAsync(entity, tenantId, ct);

        var now = DateTime.UtcNow;
        var recent = payload.Reminders.LastOrDefault(item => item.Channel == channel && item.Recipient == recipient);
        if (recent is not null && now - recent.SentAtUtc < ReminderCooldown)
            throw new ArgumentException("A reminder was already recorded for this channel and recipient in the last 24 hours.");
        payload.Reminders.Add(new InvoiceReminderDto
        {
            Id = Guid.NewGuid(), Channel = channel, Recipient = recipient, IdempotencyKey = key,
            SentAtUtc = now, Actor = Actor()
        });
        payload.AuditEvents.Add(Audit("reminder_recorded", $"{channel} reminder recorded.", Actor(), now));
        var saved = await PersistAsync(entity, payload, tenantId, ct);
        return await HydrateAsync(saved, tenantId, ct);
    }

    public async Task<InvoiceJobReleaseDto> GetJobReleaseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(_userContext.TenantId, id, ct)
            ?? throw new ArgumentException("Invoice not found.", nameof(id));
        var payload = await _payloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        ApplyFinancialState(entity, payload);
        return CalculateJobRelease(entity, payload);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetEntityAsync(_userContext.TenantId, id, default);
        if (entity is null) return;
        var payload = await _payloadStore.LoadAsync(entity.WorkflowPayloadBlobName);
        if (entity.Status != InvoiceStatus.Draft || payload.Payments.Count != 0)
            throw new ArgumentException("Only a draft invoice without payment activity can be deleted.");
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }

    private async Task<InvoiceDto> ApplyPaymentEventAsync(
        Guid tenantId,
        Guid id,
        string kind,
        InvoicePaymentInputDto input,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Amount <= 0m) throw new ArgumentException("Payment or refund amount must be greater than zero.");
        var (entity, payload) = await LoadForMutationAsync(tenantId, id, input.ExpectedVersion, ct);
        if (entity.Status is InvoiceStatus.Draft or InvoiceStatus.Void)
            throw new ArgumentException("Payment activity requires a sent, non-void invoice.");
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? $"manual:{Guid.NewGuid():N}" : Clean(input.IdempotencyKey, 200);
        if (payload.Payments.Any(item => item.IdempotencyKey == key)) return await HydrateAsync(entity, tenantId, ct);
        var status = NormalizePaymentStatus(input.Status);
        var now = DateTime.UtcNow;
        payload.Payments.Add(new InvoicePaymentDto
        {
            Id = Guid.NewGuid(), Kind = kind, Status = status, Amount = Round(input.Amount),
            Method = Clean(input.Method, 80), Note = Clean(input.Note, 1000),
            Provider = Clean(input.Provider, 80), ExternalReference = Clean(input.ExternalReference, 200),
            IdempotencyKey = key, OccurredAtUtc = now, Actor = actor
        });
        if (kind == "payment" && status == "succeeded" &&
            string.Equals(input.Provider, "Stripe", StringComparison.OrdinalIgnoreCase))
        {
            entity.StripePaymentIntentId = Clean(input.ExternalReference, 200);
        }
        ApplyFinancialState(entity, payload);
        payload.AuditEvents.Add(Audit(
            status == "succeeded" ? $"{kind}_reconciled" : $"{kind}_{status}",
            $"{kind} event for {Round(input.Amount):C} recorded with status {status}.", actor, now));
        var saved = await PersistAsync(entity, payload, tenantId, ct);
        return await HydrateAsync(saved, tenantId, ct);
    }

    private async Task<(Invoice Entity, InvoiceWorkflowPayloadDto Payload)> LoadForMutationAsync(
        Guid tenantId,
        Guid id,
        string? expectedVersion,
        CancellationToken ct)
    {
        ValidateId(id);
        var entity = await GetEntityAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Invoice not found.", nameof(id));
        ValidateVersion(entity, expectedVersion);
        return (entity, await _payloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct));
    }

    private async Task<Invoice?> GetEntityAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetAsync(Partition(tenantId), Row(id), ct);
        return entity is null || entity.IsDeleted ? null : entity;
    }

    private async Task<InvoiceDto> HydrateAsync(Invoice entity, Guid tenantId, CancellationToken ct)
    {
        var linePartition = RepositoryKeyHelper.ToTenantInvoicePartitionKey(tenantId, entity.Id);
        var lineItems = await _lineItemRepo.ListAsync(linePartition, ct);
        var payload = await _payloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        ApplyFinancialState(entity, payload);
        var dto = InvoiceMapper.ToDto(entity, lineItems);
        dto.SentAtUtc = payload.SentAtUtc;
        dto.CustomerEmail = payload.CustomerEmail;
        dto.CustomerPhone = payload.CustomerPhone;
        dto.SiteName = payload.SiteName;
        dto.ServiceSummary = payload.ServiceSummary;
        dto.ReviewUrl = payload.ReviewUrl;
        dto.RequiredDepositPercent = NormalizeDepositPercent(payload.RequiredDepositPercent);
        dto.ScopeLineItems = [.. payload.ScopeLineItems];
        dto.Payments = [.. payload.Payments.OrderBy(item => item.OccurredAtUtc)];
        dto.Reminders = [.. payload.Reminders.OrderBy(item => item.SentAtUtc)];
        dto.AuditEvents = [.. payload.AuditEvents.OrderBy(item => item.OccurredAtUtc)];
        dto.JobRelease = CalculateJobRelease(entity, payload);
        return dto;
    }

    private async Task<Invoice> PersistAsync(
        Invoice entity,
        InvoiceWorkflowPayloadDto payload,
        Guid tenantId,
        CancellationToken ct)
    {
        var oldBlob = entity.WorkflowPayloadBlobName;
        var newBlob = await _payloadStore.SaveAsync(tenantId, entity.Id, payload, ct);
        entity.WorkflowPayloadBlobName = newBlob;
        entity.DateUpdated = DateTime.UtcNow;
        try
        {
            var saved = await _repo.SaveAsync(entity, ct);
            if (!string.IsNullOrWhiteSpace(oldBlob) && oldBlob != newBlob)
            {
                try { await _payloadStore.DeleteIfExistsAsync(oldBlob, ct); }
                catch { /* Blob reconciliation can remove an orphan after committed metadata. */ }
            }
            return saved;
        }
        catch
        {
            try { await _payloadStore.DeleteIfExistsAsync(newBlob, CancellationToken.None); }
            catch { /* Preserve the repository failure. */ }
            throw;
        }
    }

    private async Task SaveLineItemsAsync(
        Guid tenantId,
        Guid invoiceId,
        IReadOnlyCollection<InvoiceLineItemDto> lineItems,
        CancellationToken ct)
    {
        var partition = RepositoryKeyHelper.ToTenantInvoicePartitionKey(tenantId, invoiceId);
        var existing = await _lineItemRepo.ListAsync(partition, ct);
        var retained = new HashSet<Guid>();
        foreach (var item in lineItems)
        {
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.InvoiceId = invoiceId;
            retained.Add(item.Id);
            var prior = existing.FirstOrDefault(value => value.Id == item.Id);
            var entity = InvoiceMapper.ToLineItemEntity(item, partition, invoiceId);
            entity.ETag = prior?.ETag ?? default;
            entity.DateCreated = prior?.DateCreated ?? DateTime.UtcNow;
            await _lineItemRepo.SaveAsync(entity, ct);
        }
        foreach (var stale in existing.Where(item => !retained.Contains(item.Id)))
        {
            stale.IsDeleted = true;
            stale.DateUpdated = DateTime.UtcNow;
            await _lineItemRepo.SaveAsync(stale, ct);
        }
    }

    private static void NormalizeLineItems(InvoiceDto dto)
    {
        if (dto.LineItems.Count is < 1 or > 100)
            throw new ArgumentException("An invoice requires between 1 and 100 line items.");
        for (var index = 0; index < dto.LineItems.Count; index++)
        {
            var item = dto.LineItems[index];
            item.SortOrder = index;
            item.Description = Clean(item.Description, 500);
            item.Unit = string.IsNullOrWhiteSpace(item.Unit) ? "ea" : Clean(item.Unit, 40);
            if (string.IsNullOrWhiteSpace(item.Description)) throw new ArgumentException("Each invoice line item requires a description.");
            if (item.Quantity <= 0m || item.Quantity > 1_000_000m) throw new ArgumentException("Line item quantity is outside the supported range.");
            if (item.UnitPrice < 0m || item.UnitPrice > 100_000_000m) throw new ArgumentException("Line item price is outside the supported range.");
            item.LineTotal = Round(item.Quantity * item.UnitPrice);
        }
    }

    private static void ApplyFinancials(InvoiceDto dto, decimal amountPaid)
    {
        if (dto.TaxRate is < 0m or > 1m) throw new ArgumentException("Tax rate must be expressed as a decimal from 0 through 1.");
        dto.Subtotal = Round(dto.LineItems.Sum(item => item.LineTotal));
        dto.TaxAmount = Round(dto.Subtotal * dto.TaxRate);
        dto.Total = Round(dto.Subtotal + dto.TaxAmount);
        dto.AmountPaid = Round(Math.Max(0m, amountPaid));
        dto.BalanceDue = Round(Math.Max(0m, dto.Total - dto.AmountPaid));
    }

    private static void ApplyFinancialState(Invoice entity, InvoiceWorkflowPayloadDto payload)
    {
        var successfulPayments = payload.Payments.Where(item => item.Kind == "payment" && item.Status == "succeeded").Sum(item => item.Amount);
        var successfulRefunds = payload.Payments.Where(item => item.Kind == "refund" && item.Status == "succeeded").Sum(item => item.Amount);
        entity.AmountPaid = Round(Math.Max(0m, successfulPayments - successfulRefunds));
        entity.BalanceDue = Round(Math.Max(0m, entity.Total - entity.AmountPaid));
        entity.PaidDate = entity.BalanceDue <= MoneyTolerance
            ? payload.Payments.Where(item => item.Kind == "payment" && item.Status == "succeeded").MaxBy(item => item.OccurredAtUtc)?.OccurredAtUtc
            : null;
        if (entity.Status != InvoiceStatus.Void && payload.SentAtUtc.HasValue)
        {
            entity.Status = entity.BalanceDue <= MoneyTolerance
                ? InvoiceStatus.Paid
                : entity.AmountPaid > MoneyTolerance ? InvoiceStatus.PartiallyPaid
                : entity.DueDate < DateTime.UtcNow ? InvoiceStatus.Overdue : InvoiceStatus.Sent;
        }
    }

    private static InvoiceJobReleaseDto CalculateJobRelease(Invoice entity, InvoiceWorkflowPayloadDto payload)
    {
        var required = Round(entity.Total * (NormalizeDepositPercent(payload.RequiredDepositPercent) / 100m));
        var remaining = Round(Math.Max(0m, required - entity.AmountPaid));
        var validStatus = entity.Status is InvoiceStatus.Sent or InvoiceStatus.Viewed or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid or InvoiceStatus.Overdue;
        var eligible = validStatus && remaining <= MoneyTolerance;
        var reason = entity.Status == InvoiceStatus.Void
            ? "The invoice is void."
            : !payload.SentAtUtc.HasValue
                ? "The invoice must be sent before job release."
                : eligible
                    ? "The service-owned deposit rule is satisfied."
                    : $"An additional {remaining:C} is required before job release.";
        return new InvoiceJobReleaseDto
        {
            IsEligible = eligible,
            RequiredDepositAmount = required,
            AmountPaid = entity.AmountPaid,
            RemainingDepositAmount = remaining,
            Reason = reason
        };
    }

    private static InvoiceWorkflowPayloadDto PayloadFrom(InvoiceDto dto) => new()
    {
        SentAtUtc = dto.SentAtUtc,
        CustomerEmail = Clean(dto.CustomerEmail, 320),
        CustomerPhone = Clean(dto.CustomerPhone, 80),
        SiteName = Clean(dto.SiteName, 300),
        ServiceSummary = Clean(dto.ServiceSummary, 2000),
        ReviewUrl = Clean(dto.ReviewUrl, 2000),
        RequiredDepositPercent = NormalizeDepositPercent(dto.RequiredDepositPercent),
        ScopeLineItems = dto.ScopeLineItems.Select(item => Clean(item, 1000)).Where(item => item.Length > 0).Take(100).ToList()
    };

    private static void MergeEditablePayload(InvoiceWorkflowPayloadDto payload, InvoiceDto dto)
    {
        payload.CustomerEmail = Clean(dto.CustomerEmail, 320);
        payload.CustomerPhone = Clean(dto.CustomerPhone, 80);
        payload.SiteName = Clean(dto.SiteName, 300);
        payload.ServiceSummary = Clean(dto.ServiceSummary, 2000);
        payload.ReviewUrl = Clean(dto.ReviewUrl, 2000);
        payload.RequiredDepositPercent = NormalizeDepositPercent(dto.RequiredDepositPercent);
        payload.ScopeLineItems = dto.ScopeLineItems.Select(item => Clean(item, 1000)).Where(item => item.Length > 0).Take(100).ToList();
    }

    private static InvoiceAuditEventDto Audit(string type, string description, string actor, DateTime at) => new()
    {
        Id = Guid.NewGuid(), Type = type, Description = description, Actor = actor, OccurredAtUtc = at
    };

    private string Actor()
    {
        var name = $"{_userContext.FirstName} {_userContext.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? _userContext.UserId.ToString("D") : name;
    }

    private static void ValidateVersion(Invoice entity, string? expected)
    {
        if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(entity.ETag.ToString(), expected.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("The invoice changed after it was loaded. Refresh before retrying.", nameof(expected));
    }

    private static string NormalizePaymentStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "succeeded" or "pending" or "failed" ? normalized : throw new ArgumentException("Payment status must be succeeded, pending, or failed.");
    }

    private static decimal NormalizeDepositPercent(decimal value) => value is >= 0m and <= 100m ? value : 50m;
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw new ArgumentException("A non-empty identifier is required.", nameof(id)); }
}
