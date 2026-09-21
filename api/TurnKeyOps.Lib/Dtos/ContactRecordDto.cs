namespace MedInsights.Lib.Dtos;

public sealed class SaveContactRecordDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? CompanyName { get; set; }
    public string[] ProfileTypes { get; set; } = ["customer"];
    public Guid? CustomerId { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Notes { get; set; }
    public string? ExpectedVersion { get; set; }
}
public sealed record ContactRecordDto(Guid Id, string FirstName, string LastName, string? ContactEmail,
    string? ContactPhone, string? CompanyName, string[] ProfileTypes, Guid? CustomerId,
    string? Address, string? City, string? State, string? PostalCode, string? Notes, string Version);
