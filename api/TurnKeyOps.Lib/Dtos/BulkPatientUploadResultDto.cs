namespace MedInsights.Lib.Dtos;

public class BulkPatientUploadResultDto
{
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkPatientUploadRowResultDto> Rows { get; set; } = new();
}
