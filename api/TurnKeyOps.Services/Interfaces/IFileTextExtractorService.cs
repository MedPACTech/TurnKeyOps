
using MedInsights.Lib.Models;

namespace MedInsights.Services;

public interface IFileTextExtractorService
{
    Task<ExtractionResult> ExtractAsync(Stream content, string fileName, string? contentType = null, ExtractionOptions? options = null, CancellationToken ct = default);
}