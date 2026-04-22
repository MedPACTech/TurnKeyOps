using System;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Lib.Dtos
{
public class DictationUploadRequest
{
    public IFormFile File { get; set; } = default!;
    public Guid? DictationId { get; set; }  // NEW: if present, attach to existing
}

}