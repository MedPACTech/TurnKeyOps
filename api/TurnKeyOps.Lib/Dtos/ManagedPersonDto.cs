namespace MedInsights.Lib.Dtos;

public sealed class SaveManagedPersonDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? LoginIdentifier { get; set; }
    public string[] ProfileTypes { get; set; } = [];
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string? Team { get; set; }
    public Guid? CustomerId { get; set; }
    public string[]? ModulePermissions { get; set; }
    public string? ExpectedVersion { get; set; }
}
public sealed record ManagedPersonDto(Guid Id, string FirstName, string LastName, string? ContactEmail,
    string? ContactPhone, string[] ProfileTypes, string? CompanyName, string? Title, string? Team,
    Guid? CustomerId, string[]? ModulePermissions, string[] EffectivePermissions, string? Role,
    Guid? MembershipId, bool IsOwner, bool IsActive, string Version);
