using Microsoft.AspNetCore.Http;

namespace MedInsights.Lib.Dtos;

public class BulkPatientUploadRequestDto
{
    public IFormFile File { get; set; } = default!;
}
