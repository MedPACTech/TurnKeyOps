using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IMobileAppointmentContextService
{
    Task<MobileCurrentAppointmentContextDto?> GetCurrentAsync(CancellationToken ct = default);
}
