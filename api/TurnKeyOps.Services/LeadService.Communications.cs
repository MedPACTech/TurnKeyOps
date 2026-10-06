using IBeam.Communications.Abstractions;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed partial class LeadService
{
    public async Task<LeadDto> CommunicationAsync(Guid id, LeadCommunicationDto input, bool send, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, input.ExpectedVersion, ct);
        if (input.Channel is not ("email" or "sms") || string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 4000 || input.Subject.Length > 200)
            throw new ArgumentException("Choose email or SMS and enter a message up to 4000 characters.");
        if (!send) return await SaveAsync(entity, "communication-draft", $"{input.Channel}: {input.Subject}\n{input.Body}", ct);
        var profile = communicationProfiles.Resolve(user.TenantId);
        if (input.Channel == "email" && string.IsNullOrWhiteSpace(entity.Data.Email) || input.Channel == "sms" && string.IsNullOrWhiteSpace(entity.Data.Phone))
            throw new ArgumentException("The customer has no address for this channel.");
        // Reserve the current version before the external effect. Concurrent submissions cannot both send.
        var reserved = await SaveAsync(entity, "communication-sending", $"Sending {input.Channel}: {input.Subject}", ct);
        entity = await EditableAsync(id, reserved.Version, ct);
        try
        {
            if (input.Channel == "email")
            {
                var message = new EmailMessage { FromAddress = profile.EmailFromAddress, FromName = profile.EmailFromName, Subject = input.Subject, TextBody = input.Body };
                message.To.Add(entity.Data.Email);
                await email.SendAsync(message, ct: ct);
            }
            else
            {
                var message = new SmsMessage { FromPhoneNumber = profile.SmsFromPhoneNumber, Body = input.Body };
                message.To.Add(entity.Data.Phone);
                await sms.SendAsync(message, ct: ct);
            }
        }
        catch
        {
            await SaveAsync(entity, "communication-unconfirmed", "Delivery was not confirmed. Check provider history before retrying.", ct);
            throw;
        }
        entity.Data.FirstResponseAtUtc ??= DateTime.UtcNow;
        return await SaveAsync(entity, "communication-sent", $"{input.Channel}: {input.Subject}\n{input.Body}", ct);
    }

    public async Task<QuoteRequestDto?> IntakeAsync(Guid id, CancellationToken ct = default)
    {
        await RequireAsync(false, ct);
        var lead = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Lead not found.");
        if (!lead.Data.IntakeRequestId.HasValue) return null;
        var request = await requests.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(lead.Data.IntakeRequestId.Value), ct);
        return request is null || request.IsDeleted || request.TenantId != user.TenantId || request.PartitionKey != Partition ? null : QuoteRequestMapper.ToDto(request);
    }
    public async Task<LeadDto> UploadAsync(Guid id, string version, IReadOnlyCollection<QuoteRequestAttachmentUpload> files, CancellationToken ct = default)
    {
        var entity = await EditableAsync(id, version, ct);
        var request = await EnsureIntakeAsync(entity, ct);
        var requestId = request.Id;
        await attachments.UploadAsync(requestId, files, ct);
        return await SaveAsync(entity, "files", $"Uploaded {files.Count} file(s).", ct, requestId);
    }
    private async Task<QuoteRequest> EnsureIntakeAsync(Lead entity, CancellationToken ct)
    {
        var requestId = entity.Data.IntakeRequestId ?? entity.Id;
        var request = await requests.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(requestId), ct);
        if (request is null)
        {
            request = new QuoteRequest { Id = requestId, TenantId = user.TenantId, PartitionKey = Partition, RowKey = RepositoryKeyHelper.ToRowKey(requestId),
                ContactName = entity.Data.ContactName, Email = entity.Data.Email, Phone = entity.Data.Phone, Need = entity.Data.RequestedWork,
                CompanyName = entity.Data.CompanyName, SiteName = entity.Data.Title, ServiceAddress = entity.Data.SiteAddress,
                ServiceType = entity.Data.Service, PropertyType = entity.Data.PropertyType,
                Source = "manual-lead", SubmittedAtUtc = entity.Data.CreatedAtUtc, DateCreated = entity.Data.CreatedAtUtc, DateUpdated = DateTime.UtcNow };
            await requests.SaveAsync(request, ct);
        }
        if (request.TenantId != user.TenantId || request.PartitionKey != Partition || request.IsDeleted) throw new ArgumentException("Intake is unavailable.");
        entity.Data.IntakeRequestId = requestId;
        return request;
    }
    public async Task<QuoteRequestAttachmentDownload?> DownloadAsync(Guid id, Guid fileId, CancellationToken ct = default)
    {
        var intakeRecord = await IntakeAsync(id, ct);
        return intakeRecord is null ? null : await attachments.DownloadAsync(intakeRecord.Id, fileId, ct);
    }
}
