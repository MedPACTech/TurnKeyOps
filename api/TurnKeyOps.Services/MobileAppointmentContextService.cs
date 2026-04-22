using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Models;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class MobileAppointmentContextService : IMobileAppointmentContextService
{
    private readonly IPatientAppointmentRepository _appointmentRepository;
    private readonly TurnKeyOps.Lib.Utils.IUserContext _userContext;

    public MobileAppointmentContextService(
        IPatientAppointmentRepository appointmentRepository,
        TurnKeyOps.Lib.Utils.IUserContext userContext)
    {
        _appointmentRepository = appointmentRepository;
        _userContext = userContext;
    }

    public async Task<MobileCurrentAppointmentContextDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var todayStartUtc = DateTimeHelper.DateFloorUtc(nowUtc, _userContext.Timezone);
        var tomorrowStartUtc = DateTimeHelper.DateCeilingUtcExclusive(nowUtc, _userContext.Timezone);

        var appointments = await _appointmentRepository.SearchAsync(
            new AppointmentSearchRepositoryFilter
            {
                TenantPartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                ProviderRowKey = EntityKeyPolicy.Row(_userContext.UserId),
                FromUtc = todayStartUtc,
                ToExclusiveUtc = tomorrowStartUtc,
                Sort = "start",
                Order = "asc",
                Page = 1,
                PageSize = 50
            },
            ct);

        var candidate = appointments
            .Where(x => !x.IsDeleted)
            .Where(x => x.AppointmentStatus is AppointmentStatus.Scheduled or AppointmentStatus.In_Progress)
            .OrderBy(x => Math.Abs((x.AppointmentStartTime - nowUtc).TotalMinutes))
            .ThenBy(x => x.AppointmentStartTime)
            .FirstOrDefault();

        return candidate is null ? null : Map(candidate);
    }

    private MobileCurrentAppointmentContextDto Map(PatientAppointment appointment)
    {
        var customerName = JoinParts(
            appointment.PrimaryContactFirstName,
            appointment.PrimaryContactLastName);

        if (string.IsNullOrWhiteSpace(customerName))
        {
            customerName = JoinParts(
                appointment.PatientFirstName,
                appointment.PatientLastName);
        }

        return new MobileCurrentAppointmentContextDto
        {
            AppointmentId = appointment.Id,
            CustomerName = customerName,
            CustomerCompany = null,
            ProjectAddress = FormatAddress(appointment),
            AppointmentDateTime = DateTime.SpecifyKind(appointment.AppointmentStartTime, DateTimeKind.Utc),
            EstimatorName = string.IsNullOrWhiteSpace(appointment.DisplayName)
                ? JoinParts(_userContext.FirstName, _userContext.LastName)
                : appointment.DisplayName,
            EstimateId = null,
            EstimateNumber = null,
            ProjectName = string.IsNullOrWhiteSpace(appointment.Reason) ? null : appointment.Reason
        };
    }

    private static string JoinParts(params string?[] values)
        => string.Join(" ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

    private static string FormatAddress(PatientAppointment appointment)
    {
        var line1 = appointment.VisitAddressLine1?.Trim();
        var line2 = appointment.VisitAddressLine2?.Trim();
        var cityStatePostal = string.Join(", ",
            new[]
            {
                appointment.VisitCity?.Trim(),
                JoinStatePostal(appointment.VisitState, appointment.VisitPostalCode)
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.Join(", ",
            new[] { line1, line2, cityStatePostal }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string JoinStatePostal(string? state, string? postalCode)
        => string.Join(" ", new[] { state?.Trim(), postalCode?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
