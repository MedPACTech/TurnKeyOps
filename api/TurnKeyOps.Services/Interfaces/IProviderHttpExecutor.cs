using System.Net;

namespace MedInsights.Services.Interfaces;

public interface IProviderHttpExecutor
{
    Task<ProviderHttpResponse> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        bool retrySafe,
        CancellationToken ct = default);
}

public sealed record ProviderHttpResponse(HttpStatusCode StatusCode, string Payload);
