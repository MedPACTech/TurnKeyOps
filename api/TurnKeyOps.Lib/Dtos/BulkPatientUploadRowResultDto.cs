namespace MedInsights.Lib.Dtos;

public class BulkPatientUploadRowResultDto
{
    public int RowNumber { get; set; }
    public bool Success { get; set; }
    public Guid? PatientId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Error { get; set; }
}
