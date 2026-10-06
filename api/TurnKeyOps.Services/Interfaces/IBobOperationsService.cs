using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IBobOperationsService
{
    Task<BobActionDto> ApproveEstimateAsync(Guid estimateId, Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ProposeEstimateAsync(Guid estimateId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ApproveLeadAsync(Guid leadId, Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ProposeLeadAsync(Guid leadId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ProposeAsync(Guid conversationId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ApproveAsync(Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ExecuteAsync(Guid actionId, CancellationToken ct = default);
    Task<IReadOnlyList<BobActionDto>> ListAsync(Guid conversationId, CancellationToken ct = default);
}
