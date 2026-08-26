using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IContactAccessGrantService
{
    Task<IReadOnlyList<ContactAccessGrantDto>> ListAsync(CancellationToken ct = default);
    Task<ContactAccessGrantDto?> GetAsync(string contactId, CancellationToken ct = default);
    Task<ContactAccessGrantDto> UpsertAsync(
        string contactId,
        UpdateContactAccessGrantDto input,
        CancellationToken ct = default);
}
