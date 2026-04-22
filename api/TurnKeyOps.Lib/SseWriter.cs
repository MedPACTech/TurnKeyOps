

using MedInsights.Lib;
using Microsoft.AspNetCore.Http;

public sealed class SseWriter : IEventStreamWriter
{
    private readonly HttpResponse _res;

    public SseWriter(HttpResponse res)
    {
        _res = res;
        _res.ContentType = "text/event-stream";
        //_res.Headers.CacheControl = "no-cache";
        //_res.Headers.Connection = "keep-alive";
        _res.Headers["X-Accel-Buffering"] = "no";
    }

    public async Task WriteAsync(StreamDelta delta, CancellationToken ct = default)
    {
        if (_res.HttpContext.RequestAborted.IsCancellationRequested || ct.IsCancellationRequested) return;

        // Expect delta.Json to already be JSON (we just wrap with "data: ...\n\n")
        try
        {
            await _res.WriteAsync($"data: {delta.Json}\n\n");
            await _res.Body.FlushAsync();
        }
        catch
        {
            // client disconnected; swallow
        }
    }
}
