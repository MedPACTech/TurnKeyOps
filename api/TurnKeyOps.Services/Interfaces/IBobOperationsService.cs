using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IBobOperationsService
{
    Task<BobActionDto> ApproveSupplyAsync(Guid supplyId, Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ProposeSupplyAsync(Guid supplyId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ApproveJobAsync(Guid jobId, Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ProposeJobAsync(Guid jobId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ApproveEstimateAsync(Guid estimateId, Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ProposeEstimateAsync(Guid estimateId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ApproveLeadAsync(Guid leadId, Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ProposeLeadAsync(Guid leadId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ProposeAsync(Guid conversationId, ProposeBobActionDto input, CancellationToken ct = default);
    Task<BobActionDto> ApproveAsync(Guid actionId, CancellationToken ct = default);
    Task<BobActionDto> ExecuteAsync(Guid actionId, CancellationToken ct = default);
    Task<IReadOnlyList<BobActionDto>> ListAsync(Guid conversationId, CancellationToken ct = default);
}
