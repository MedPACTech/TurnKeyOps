using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync();
}
