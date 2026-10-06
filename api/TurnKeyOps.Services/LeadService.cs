using System.Text.Json;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed partial class LeadService(
    ILeadRepository leads, IQuoteRequestRepository requests, ICustomerRepository customers,
    ITenantMembershipRepository memberships, IJobRepository jobs,
    IUserContext user, IRoleAccessService access, IAuditService audit,
    LeadConfigurationService configuration, LeadIntakeBridge intake,
    ICalendarEventService calendar,
    IQuoteEstimateService quoteEstimates, IJobService jobService,
    IQuoteRequestAttachmentService attachments,
    IBeam.Communications.Abstractions.IEmailService email, IBeam.Communications.Abstractions.ISmsService sms,
    ITenantCommunicationProfileResolver communicationProfiles, LeadActivityStore activityStore, MedInsights.Services.UserModuleAccessService? moduleAccess = null, IManagedProfileStore? profiles = null)
{
    public static readonly string[] Sources = ["Website", "Referral", "Phone", "Manual", "Existing Customer", "Walk-in", "Other"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Partition => RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
    private string Actor => $"{user.FirstName} {user.LastName}".Trim();

    private async Task RequireAsync(bool write, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty) throw new UnauthorizedAccessException();
        await access.RequirePermissionAsync(write ? TurnKeyPermissionKeys.LeadsWrite : TurnKeyPermissionKeys.LeadsRead, ct);
    }
    private async Task<Lead?> FindAsync(Guid id, CancellationToken ct)
    {
        var entity = await leads.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(id), ct);
        if (entity is null || entity.IsDeleted || entity.PartitionKey != Partition || entity.Data.TenantId != user.TenantId) return null;
        await activityStore.HydrateAsync(entity, ct);
        return entity;
    }
    private async Task<Lead> EditableAsync(Guid id, string version, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        var entity = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Lead not found.");
        if (string.IsNullOrWhiteSpace(version) || entity.Data.Version != version)
            throw new InvalidOperationException("This lead changed. Refresh before saving.");
        // Repository caches may return the same reference. Never mutate it before validation/persistence succeeds.
        var copy = JsonSerializer.Deserialize<Lead>(JsonSerializer.Serialize(entity, Json), Json)!;
        copy.ETag = entity.ETag;
        return copy;
    }
    private async Task<List<Lead>> AllAsync(CancellationToken ct) => (await leads.ListAsync(Partition, ct))
        .Where(x => !x.IsDeleted && x.PartitionKey == Partition && x.Data.TenantId == user.TenantId).ToList();
    private async Task<IReadOnlyList<LeadMemberDto>> MembersAsync(CancellationToken ct) =>
        (await memberships.GetActiveAssignedByTenantAsync(user.TenantId, ct))
        .Where(x => x.TenantId == user.TenantId && !x.IsDeleted && !x.DateRemoved.HasValue && x.MembershipStatus == "Active" && x.Role != "contact")
        .Select(x => new LeadMemberDto(x.Id, x.InvitedEmail ?? x.InvitedPhone ?? x.UserId.ToString())).ToArray();

    private async Task<IReadOnlyList<LeadMemberDto>> AssociatesAsync(CancellationToken ct)
    {
        var result = new List<LeadMemberDto>();
        if (profiles is not null)
            await foreach (var p in profiles.ListAsync(Partition, ct))
                if (p.PartitionKey == Partition && p.IsActive && !p.IsDeleted && p.ProfileTypes.Contains("employee"))
                    result.Add(new(p.Id, string.Join(" ", new[] { p.FirstName, p.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } name ? name : p.ContactEmail ?? "Unnamed associate"));
        return result.OrderBy(x => x.Name).ToArray();
    }

    private static LeadDto Decorate(LeadDto lead, LeadConfigurationDto config, IReadOnlyList<LeadMemberDto> members, IReadOnlyList<LeadMemberDto> associates)
    {
        var copy = JsonSerializer.Deserialize<LeadDto>(JsonSerializer.Serialize(lead, Json), Json)!;
        copy.StageLabel = config.StageLabels.GetValueOrDefault(copy.Stage, copy.Stage.Replace('_', ' ').ToLowerInvariant());
        copy.Fields = LeadConfigurationService.Fields(copy, config).ToList();
        copy.MissingRequired = copy.Fields.Where(x => x.Required && string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Label).ToList();
        copy.OwnerName = copy.OwnerProfileId.HasValue
            ? associates.FirstOrDefault(x => x.Id == copy.OwnerProfileId)?.Name ?? "Unavailable associate"
            : members.FirstOrDefault(x => x.Id == copy.OwnerMembershipId)?.Name ?? (copy.OwnerMembershipId.HasValue ? "Unavailable associate" : "Unassigned");
        if (string.IsNullOrWhiteSpace(copy.NextAction)) copy.NextAction = LeadStages.NextAction(copy.Stage);
        copy.BobSummary = copy.MissingRequired.Count > 0
            ? $"Before qualification, ask for {string.Join(", ", copy.MissingRequired)}. Next: {copy.NextAction}."
            : $"The required qualification information is present. Next: {copy.NextAction}.";
        return copy;
    }
    public async Task<LeadWorkspaceDto> WorkspaceAsync(CancellationToken ct = default)
    {
        await RequireAsync(false, ct);
        var config = await configuration.GetAsync(user.TenantId, ct);
        var members = await MembersAsync(ct);
        var associates = await AssociatesAsync(ct);
        return new((await AllAsync(ct)).Select(x => Decorate(x.Data, config, members, associates)).ToArray(), config, members,
            await access.HasPermissionAsync(TurnKeyPermissionKeys.LeadsWrite, ct),
            await access.HasPermissionAsync(TurnKeyPermissionKeys.TenantSettingsManage, ct), associates);
    }
    public async Task<LeadDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await RequireAsync(false, ct);
        var lead = await FindAsync(id, ct);
        return lead is null ? null : Decorate(lead.Data, await configuration.GetAsync(user.TenantId, ct), await MembersAsync(ct), await AssociatesAsync(ct));
    }
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        await RequireAsync(true, ct);
        var count = 0;
        foreach (var request in await requests.ListAsync(Partition, ct))
        {
            if (request.IsDeleted || request.PartitionKey != Partition || request.TenantId != user.TenantId) continue;
            if (await FindAsync(request.Id, ct) is not null) continue;
            await intake.EnsureAsync(request, ct); count++;
        }
        await AuditAsync(Guid.Empty, "intake-reconciled", $"Reconciled {count} retained intake records.", ct);
        return count;
    }
    public async Task<LeadDto> CreateAsync(CreateLeadDto input, CancellationToken ct = default)
    {
        await RequireAsync(true, ct);
        await ValidateAsync(input, ct);
        var id = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id;
        var existing = await FindAsync(id, ct);
        if (existing is not null)
        {
            if (existing.Data.Title != input.Title || existing.Data.RequestedWork != input.RequestedWork || existing.Data.Email != input.Email)
                throw new ArgumentException("This creation ID is already in use.");
            return existing.Data;
        }
        var data = JsonSerializer.Deserialize<LeadDto>(JsonSerializer.Serialize(input, Json), Json)!;
        data.Id = id; data.TenantId = user.TenantId; data.Stage = "NEW";
        data.CreatedAtUtc = DateTime.UtcNow;
        if (input.CreateCustomer && input.CustomerId is null)
        {
            // Deliberate create, not an inferred match. Use a stable ID so a failed retry cannot create extra contacts.
            var customerId = id;
            var parts = input.ContactName.Trim().Split(' ', 2);
            var customer = await customers.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(customerId), ct);
            if (customer is null)
                await customers.SaveAsync(new Customer { Id = customerId, PartitionKey = Partition, RowKey = RepositoryKeyHelper.ToRowKey(customerId),
                    FirstName = parts[0], LastName = parts.Length > 1 ? parts[1] : "", CompanyName = input.CompanyName,
                    Email = input.Email, Phone = input.Phone }, ct);
            data.CustomerId = customerId;
        }
        var entity = new Lead { Id = id, PartitionKey = Partition, RowKey = RepositoryKeyHelper.ToRowKey(id), Data = data, DateCreated = data.CreatedAtUtc };
        return await SaveAsync(entity, "created", "Opportunity created", ct);
    }
    public async Task<LeadDto> UpdateAsync(Guid id, UpdateLeadDto input, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, input.ExpectedVersion, ct);
        await ValidateAsync(input, ct, entity.Data.OwnerProfileId != input.OwnerProfileId || entity.Data.OwnerMembershipId != input.OwnerMembershipId);
        // Copy only editable business fields; lifecycle, relationships to downstream work and audit are server-owned.
        var priorOwner = entity.Data.OwnerMembershipId;
        var priorProfile = entity.Data.OwnerProfileId;
        foreach (var property in typeof(LeadInput).GetProperties()) property.SetValue(entity.Data, property.GetValue(input));
        var assignmentChanged = priorOwner != input.OwnerMembershipId || priorProfile != input.OwnerProfileId;
        var summary = "Lead details / qualification updated";
        if (assignmentChanged)
        {
            var assignees = input.OwnerProfileId.HasValue ? await AssociatesAsync(ct) : await MembersAsync(ct);
            var selected = assignees.FirstOrDefault(x => x.Id == (input.OwnerProfileId ?? input.OwnerMembershipId));
            summary = selected is null ? "Associate assignment removed" : $"Assigned to {selected.Name}";
        }
        return await SaveAsync(entity, assignmentChanged ? "assignment" : "updated", summary, ct);
    }
    private async Task ValidateAsync(LeadInput input, CancellationToken ct, bool validateAssignment = true)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200) throw new ArgumentException("A lead name of 1–200 characters is required.");
        if (!Sources.Contains(input.Source)) throw new ArgumentException("Choose a valid lead source.");
        if (input.EstimatedValue < 0 || input.EstimatedValue > 100000000000m) throw new ArgumentException("Opportunity value is invalid.");
        if (input.RequestedWork.Length > 8000 || input.Qualification.Count > 50 || input.Attribution.Count > 20 ||
            input.Qualification.Concat(input.Attribution).Any(x => x.Key.Length > 100 || x.Value.Length > 2000))
            throw new ArgumentException("Lead details exceed the supported length.");
        foreach (var property in typeof(LeadInput).GetProperties().Where(x => x.PropertyType == typeof(string) && x.Name != nameof(LeadInput.RequestedWork)))
            if (((string?)property.GetValue(input))?.Length > 1000) throw new ArgumentException("Lead field exceeds 1000 characters.");
        if (JsonSerializer.SerializeToUtf8Bytes(input).Length > 24000) throw new ArgumentException("Lead details exceed 24 KB. Attach a document for longer information.");
        var config = await configuration.GetAsync(user.TenantId, ct);
        if (!config.TradeProfiles.Contains(input.TradeProfile)) throw new ArgumentException("Select a configured trade profile.");
        if (config.ReferralRequired && input.Source == "Referral" && input.ReferralContactId is null && string.IsNullOrWhiteSpace(input.ReferralName))
            throw new ArgumentException("A referral source is required.");
        foreach (var customerId in new[] { input.CustomerId, input.ReferralContactId }.Where(x => x.HasValue))
        {
            var customer = await customers.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(customerId!.Value), ct);
            if (customer is null || customer.IsDeleted || customer.PartitionKey != Partition) throw new ArgumentException("Linked contact was not found in this tenant.");
        }
        if (input.OwnerMembershipId.HasValue && input.OwnerProfileId.HasValue)
            throw new ArgumentException("Choose one assigned associate.");
        if (validateAssignment && input.OwnerProfileId.HasValue && !(await AssociatesAsync(ct)).Any(x => x.Id == input.OwnerProfileId))
            throw new ArgumentException("Choose an active employee profile in this company.");
        if (validateAssignment && input.OwnerMembershipId.HasValue && !(await MembersAsync(ct)).Any(x => x.Id == input.OwnerMembershipId))
            throw new ArgumentException("The lead owner must be an active employee in this tenant.");
    }
    public async Task<LeadDto> StageAsync(Guid id, LeadStageDto input, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, input.ExpectedVersion, ct);
        if (!LeadStages.All.Contains(input.Stage)) throw new ArgumentException("Unknown canonical stage.");
        var config = await configuration.GetAsync(user.TenantId, ct);
        if (entity.Data.Stage == "WON" && input.Stage != "WON") throw new ArgumentException("Won leads retain their downstream history. Create a new opportunity for new work.");
        if (input.Stage is "QUALIFIED" or "DISCOVERY" or "READY_TO_ESTIMATE" or "ESTIMATING" or "PROPOSAL" or "WON") RequireQualified(entity.Data, config);
        if (input.Stage == "WON" && !config.WonReasons.Contains(input.Reason) || input.Stage == "LOST" && !config.LostReasons.Contains(input.Reason))
            throw new ArgumentException("Choose a configured outcome reason.");
        var previous = entity.Data.Stage;
        entity.Data.Stage = input.Stage;
        entity.Data.NextAction = LeadStages.NextAction(input.Stage);
        entity.Data.CloseReason = LeadStages.IsClosed(input.Stage) ? input.Reason : "";
        if (input.Stage == "WON") entity.Data.WonAtUtc ??= DateTime.UtcNow;
        if (input.Stage == "LOST") entity.Data.LostAtUtc = DateTime.UtcNow;
        return await SaveAsync(entity, "stage", $"{previous} → {input.Stage}{(input.Reason.Length > 0 ? ": " + input.Reason : "")}", ct);
    }
    private static void RequireQualified(LeadDto lead, LeadConfigurationDto config)
    {
        var missing = LeadConfigurationService.Fields(lead, config).Where(x => x.Required && string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Label).ToArray();
        if (missing.Length > 0) throw new ArgumentException($"Required before advancing: {string.Join(", ", missing)}.");
    }
    public async Task<LeadDto> NoteAsync(Guid id, LeadNoteDto input, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, input.ExpectedVersion, ct);
        if (input.Type is not ("note" or "call" or "customer-response" or "task" or "discovery")) throw new ArgumentException("Unsupported activity type.");
        if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 4000) throw new ArgumentException("Enter a note of 1–4000 characters.");
        if (input.Type == "call") entity.Data.FirstResponseAtUtc ??= DateTime.UtcNow;
        return await SaveAsync(entity, input.Type, input.Text, ct);
    }
    public async Task<LeadDto> CreateCustomerAsync(Guid id, string version, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, version, ct);
        if (entity.Data.CustomerId.HasValue) return entity.Data;
        if (string.IsNullOrWhiteSpace(entity.Data.ContactName)) throw new ArgumentException("Capture the contact name first.");
        var existing = await customers.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(id), ct);
        if (existing is not null && existing.IsDeleted) throw new ArgumentException("The customer record is archived; choose another customer.");
        if (existing is null)
        {
            var parts = entity.Data.ContactName.Trim().Split(' ', 2);
            await customers.SaveAsync(new Customer { Id = id, PartitionKey = Partition, RowKey = RepositoryKeyHelper.ToRowKey(id),
                FirstName = parts[0], LastName = parts.Length > 1 ? parts[1] : "", CompanyName = entity.Data.CompanyName,
                Email = entity.Data.Email, Phone = entity.Data.Phone }, ct);
        }
        entity.Data.CustomerId = id;
        return await SaveAsync(entity, "contact", "Created and linked a customer from captured details; no suggested matches were merged.", ct, id);
    }
    public async Task<IReadOnlyList<LeadDuplicateDto>> DuplicatesAsync(Guid id, CancellationToken ct = default)
    {
        await RequireAsync(false, ct);
        var entity = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Lead not found.");
        var lead = entity.Data;
        var result = new List<LeadDuplicateDto>();
        foreach (var customer in await customers.ListAsync(Partition, ct))
        {
            if (customer.IsDeleted || customer.PartitionKey != Partition) continue;
            var match = Match(lead, customer.Email, customer.Phone, customer.Address, customer.Id);
            if (match is not null) result.Add(new(customer.Id, "contact", $"{customer.FirstName} {customer.LastName} {customer.CompanyName}".Trim(), match));
        }
        foreach (var other in await AllAsync(ct))
        {
            if (other.Id == id || LeadStages.IsClosed(other.Data.Stage)) continue;
            var match = Match(lead, other.Data.Email, other.Data.Phone, other.Data.SiteAddress, other.Data.CustomerId);
            if (match is not null) result.Add(new(other.Id, "lead", other.Data.Title, match));
        }
        return result;
    }
    private static string? Match(LeadDto lead, string? email, string? phone, string? address, Guid? customerId)
    {
        if (lead.CustomerId.HasValue && lead.CustomerId == customerId) return "Same linked customer; may be separate work";
        if (!string.IsNullOrWhiteSpace(lead.Email) && string.Equals(lead.Email.Trim(), email?.Trim(), StringComparison.OrdinalIgnoreCase)) return "Matching email (verify before linking)";
        static string Digits(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());
        var normalized = Digits(lead.Phone);
        if (normalized.Length >= 7 && normalized == Digits(phone)) return "Matching phone";
        if (!string.IsNullOrWhiteSpace(lead.SiteAddress) && string.Equals(lead.SiteAddress.Trim(), address?.Trim(), StringComparison.OrdinalIgnoreCase)) return "Matching site address";
        return null;
    }
    public async Task<LeadDto> AssignAsync(Guid id, string version, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, version, ct);
        var config = await configuration.GetAsync(user.TenantId, ct);
        if (config.AssignmentMode == "manual") throw new ArgumentException("Automatic assignment is disabled; choose an owner.");
        var members = await MembersAsync(ct);
        var candidates = config.AssignmentRules.Where(rule => members.Any(x => x.Id == rule.MembershipId))
            .Where(rule => (rule.Trade == "" || rule.Trade == entity.Data.TradeProfile) && (rule.Service == "" || rule.Service == entity.Data.Service) &&
                (rule.PropertyType == "" || rule.PropertyType == entity.Data.PropertyType) && (rule.Source == "" || rule.Source == entity.Data.Source) &&
                (rule.Territory == "" || entity.Data.SiteAddress.Contains(rule.Territory, StringComparison.OrdinalIgnoreCase))).ToList();
        if (candidates.Count == 0) throw new ArgumentException("No eligible assignment rule matched; assign manually.");
        var all = await AllAsync(ct);
        var chosen = candidates.OrderByDescending(x => x.Priority)
            .ThenBy(x => config.AssignmentMode == "workload" ? all.Count(l => l.Data.OwnerMembershipId == x.MembershipId && !LeadStages.IsClosed(l.Data.Stage)) : 0)
            .ThenBy(x => config.AssignmentMode == "round-robin" ? all.Where(l => l.Data.OwnerMembershipId == x.MembershipId).Select(l => l.Data.UpdatedAtUtc).DefaultIfEmpty(DateTime.MinValue).Max() : DateTime.MinValue)
            .ThenBy(x => x.MembershipId).First();
        entity.Data.OwnerMembershipId = chosen.MembershipId;
        entity.Data.OwnerProfileId = null;
        return await SaveAsync(entity, "assignment", $"Assigned to {members.First(x => x.Id == chosen.MembershipId).Name}: matching trade/service/type/source/territory rule; policy {config.AssignmentMode}, priority {chosen.Priority}.", ct);
    }
    public async Task<LeadDto> EstimateAsync(Guid id, string version, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, version, ct);
        await access.RequirePermissionAsync(TurnKeyPermissionKeys.EstimatesWrite, ct);
        RequireQualified(entity.Data, await configuration.GetAsync(user.TenantId, ct));
        if (LeadStages.IsClosed(entity.Data.Stage)) throw new ArgumentException("Reopen a lost lead before preparing an estimate.");
        if (!entity.Data.CustomerId.HasValue) throw new ArgumentException("Link a customer before creating the estimate.");
        var request = await EnsureIntakeAsync(entity, ct);
        var packet = await quoteEstimates.PrepareFromLeadAsync(entity.Data, request.Id, ct);
        entity.Data.EstimateId = packet.Id; entity.Data.Stage = "ESTIMATING"; entity.Data.NextAction = LeadStages.NextAction("ESTIMATING");
        return await SaveAsync(entity, "estimate", "Linked estimate draft in the existing estimator; original opportunity and attribution retained.", ct, packet.Id);
    }
    public async Task<LeadDto> ConvertAsync(Guid id, string version, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, version, ct);
        await access.RequirePermissionAsync(TurnKeyPermissionKeys.EstimatesWrite, ct);
        await access.RequirePermissionAsync(TurnKeyPermissionKeys.JobsWrite, ct);
        if(moduleAccess is not null && !UserModulePermissions.Allows(await moduleAccess.GetAsync(ct),"jobs",true))throw new MedInsights.Lib.ForbiddenAccessException("Jobs write permission is required for handoff.");
        if (entity.Data.Stage != "WON" || !entity.Data.IntakeRequestId.HasValue || !entity.Data.CustomerId.HasValue)
            throw new ArgumentException("Mark the lead won, link its customer and obtain estimate approval first.");
        var packet = await quoteEstimates.GetAsync(entity.Data.IntakeRequestId.Value, ct);
        if (packet is null || packet.Delivery?.Status != "approved") throw new ArgumentException("Customer approval of the estimate is required before job handoff.");
        var existing = await jobs.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(id), ct);
        if (existing is not null && (existing.LeadId != id || existing.IsDeleted)) throw new InvalidOperationException("Job identity is already in use.");
        var job = await jobService.AddAsync(new JobDto {
            Id = id, LeadId = id, Name = entity.Data.Title, CustomerId = entity.Data.CustomerId.Value,
            JobSiteId = packet.Document?.SiteId, EstimateId = packet.Id,
            CustomerName = entity.Data.ContactName, ContactName = entity.Data.ContactName, ContactEmail = entity.Data.Email,
            ContactPhone = entity.Data.Phone, ProjectAddress = entity.Data.SiteAddress, QuoteRequestId = entity.Data.IntakeRequestId,
            EstimatedTotal = packet.AcceptedTotal, RequiredDepositPercent = packet.Document?.DepositPercent ?? 0, Status = Lib.Enums.JobStatus.Created,
            Description = packet.Document?.Scope ?? packet.ServiceSummary,
            AcceptedEstimate = new() { EstimateId=packet.Id,LeadId=id,Revision=packet.RevisionNumber,DocumentHash=packet.DocumentHash,
                Document=QuoteEstimateService.CustomerProjection(packet,false).Document,Signature=packet.ApprovalSignature,
                SelectedOptions=QuoteEstimateService.CustomerProjection(packet).Pricing?.Options.Where(x=>packet.AcceptedOptionIds.Contains(x.Id)).ToList()??[],
                Source=entity.Data.Source,Referral=entity.Data.ReferralName,SalesOwnerProfileId=entity.Data.OwnerProfileId,SalesOwnerMembershipId=entity.Data.OwnerMembershipId },
            TradeType = entity.Data.TradeProfile switch { "concrete" => Lib.Enums.TradeType.Concrete, "framing" => Lib.Enums.TradeType.Framing, "doors-locks" => Lib.Enums.TradeType.DoorsLocksmith, _ => Lib.Enums.TradeType.General },
            LocksmithJobType = entity.Data.TradeProfile == "doors-locks" ? entity.Data.PropertyType : null,
            Notes = $"Lead {id}; approved estimate revision {packet.RevisionNumber}; source {entity.Data.Source}; referral {entity.Data.ReferralName}. Sales activity and commitments remain on the linked Lead."
        }, ct);
        entity.Data.JobId = job.Id; entity.Data.ConvertedAtUtc ??= DateTime.UtcNow;
        return await SaveAsync(entity, "job", "Won opportunity handed off to job; approved estimate and sales history preserved.", ct, job.Id);
    }
    public async Task<LeadDto> ScheduleAsync(Guid id, LeadScheduleDto input, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, input.ExpectedVersion, ct);
        await access.RequirePermissionAsync(TurnKeyPermissionKeys.CalendarWrite, ct);
        if (input.StartUtc.Kind != DateTimeKind.Utc || input.EndUtc.Kind != DateTimeKind.Utc || input.EndUtc <= input.StartUtc || input.StartUtc < DateTime.UtcNow)
            throw new ArgumentException("Choose a future visit with a valid start and end time.");
        // Reserve before touching the calendar, so stale concurrent requests cannot both schedule.
        var reserved = await SaveAsync(entity, "appointment-pending", $"Scheduling site visit for {input.StartUtc:u}", ct);
        entity = await EditableAsync(id, reserved.Version, ct);
        var appointmentKey = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{user.TenantId:N}:{id:N}:{input.StartUtc.Ticks}:{input.EndUtc.Ticks}"));
        var appointmentId = new Guid(appointmentKey.AsSpan(0, 16));
        var existingAppointment = await calendar.GetAsync(appointmentId);
        if (existingAppointment is not null && existingAppointment.LeadId != id)
            throw new InvalidOperationException("The calendar entry does not belong to this opportunity.");
        var appointment = existingAppointment ?? await calendar.AddAsync(new CalendarEventDto { Id = appointmentId, Title = entity.Data.Title + " · Site visit", LeadId = id,
            Description = entity.Data.SiteAddress, EventType = Lib.Enums.CalendarEventType.SiteVisit, StartUtc = input.StartUtc, EndUtc = input.EndUtc });
        entity.Data.FollowUpAtUtc = input.StartUtc;
        return await SaveAsync(entity, "appointment", $"Site visit scheduled for {input.StartUtc:u}", ct, appointment.Id);
    }
    public async Task<object> MetricsAsync(CancellationToken ct = default)
    {
        await RequireAsync(false, ct);
        var all = (await AllAsync(ct)).Select(x => x.Data).ToList();
        var closed = all.Count(x => LeadStages.IsClosed(x.Stage));
        var responses = all.Where(x => x.FirstResponseAtUtc.HasValue).Select(x => (x.FirstResponseAtUtc!.Value - x.CreatedAtUtc).TotalMinutes).ToArray();
        return new { volume = all.Count, pipelineValue = all.Where(x => !LeadStages.IsClosed(x.Stage)).Sum(x => x.EstimatedValue ?? 0),
            closeRate = closed == 0 ? (double?)null : (double)all.Count(x => x.Stage == "WON") / closed,
            averageResponseMinutes = responses.Length == 0 ? (double?)null : responses.Average(),
            sources = all.GroupBy(x => x.Source).Select(g => new { source = g.Key, leads = g.Count(), won = g.Count(x => x.Stage == "WON"), jobs = g.Count(x => x.JobId.HasValue), value = g.Where(x => x.Stage == "WON").Sum(x => x.EstimatedValue ?? 0) }),
            referrals = all.Where(x => x.Source == "Referral").GroupBy(x => x.ReferralContactId?.ToString() ?? x.ReferralName).Select(g => new { referral = g.Key, leads = g.Count(), jobs = g.Count(x => x.JobId.HasValue), value = g.Where(x => x.Stage == "WON").Sum(x => x.EstimatedValue ?? 0) }) };
    }
    internal async Task<LeadDto> SaveAsync(Lead entity, string type, string text, CancellationToken ct, Guid? relatedId = null)
    {
        entity.Data.Activity.Add(new() { Type = type, Text = text, Actor = Actor, RelatedId = relatedId });
        entity.DateUpdated = entity.Data.UpdatedAtUtc = DateTime.UtcNow;
        entity.Data.Version = Guid.NewGuid().ToString("N");
        var saved = await activityStore.CommitAsync(entity, type == "created", ct);
        await AuditAsync(entity.Id, type, text, ct);
        return Decorate(saved.Data, await configuration.GetAsync(user.TenantId, ct), await MembersAsync(ct), await AssociatesAsync(ct));
    }
    private Task AuditAsync(Guid id, string type, string text, CancellationToken ct) => audit.RecordAsync(new RecordAuditEventRequestDto {
        TenantId = user.TenantId, UserId = user.UserId, Category = "lead", TargetType = "lead", TargetId = id.ToString(), Action = type,
        Source = "leads", Description = text }, ct);
}
