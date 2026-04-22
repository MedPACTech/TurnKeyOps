using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IEstimateDefaultsService
{
    Task<EstimateDefaultsDto> GetAsync(CancellationToken ct = default);
    Task<EstimateDefaultsDto> UpsertAsync(EstimateDefaultsDto dto, CancellationToken ct = default);
}
