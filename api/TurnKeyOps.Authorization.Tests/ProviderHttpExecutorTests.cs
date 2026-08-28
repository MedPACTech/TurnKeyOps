using System.Net;
using MedInsights.Lib.Configurations;
using MedInsights.Services;
using Microsoft.Extensions.Options;

namespace MedInsights.Authorization.Tests;

public sealed class ProviderHttpExecutorTests
{
    [Fact]
    public async Task RetriesTransientFailuresOnlyWhenRequestIsSafe()
    {
        var safeHandler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        var executor = Executor(retryCount: 1);

        var response = await executor.SendAsync(
            new HttpClient(safeHandler) { BaseAddress = new Uri("https://provider.example") },
            () => new HttpRequestMessage(HttpMethod.Post, "/checkout"),
            retrySafe: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, safeHandler.CallCount);

        var unsafeHandler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => executor.SendAsync(
            new HttpClient(unsafeHandler) { BaseAddress = new Uri("https://provider.example") },
            () => new HttpRequestMessage(HttpMethod.Post, "/checkout"),
            retrySafe: false));

        Assert.Equal(1, unsafeHandler.CallCount);
    }

    [Fact]
    public async Task DoesNotExposeProviderResponsePayloadInFailure()
    {
        var executor = Executor(retryCount: 2);
        var handler = new SequenceHandler(HttpStatusCode.BadRequest, "sensitive-provider-payload");

        var exception = await Assert.ThrowsAnyAsync<HttpRequestException>(() => executor.SendAsync(
            new HttpClient(handler) { BaseAddress = new Uri("https://provider.example") },
            () => new HttpRequestMessage(HttpMethod.Get, "/failure"),
            retrySafe: true));

        Assert.DoesNotContain("sensitive-provider-payload", exception.Message);
        Assert.Contains("400", exception.Message);
        Assert.Equal(1, handler.CallCount);
    }

    private static ProviderHttpExecutor Executor(int retryCount) => new(
        Options.Create(new BillingIntegrationOptions
        {
            RetryCount = retryCount,
            RequestTimeoutSeconds = 5,
            CircuitBreakerFailureThreshold = 5,
            CircuitBreakerDurationSeconds = 5
        }));

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Payload)> _responses;

        public SequenceHandler(params HttpStatusCode[] statuses)
            : this(statuses.Select(status => (status, string.Empty)).ToArray())
        {
        }

        public SequenceHandler(HttpStatusCode status, string payload)
            : this((status, payload))
        {
        }

        private SequenceHandler(params (HttpStatusCode Status, string Payload)[] responses)
        {
            _responses = new Queue<(HttpStatusCode Status, string Payload)>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var response = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Payload)
            });
        }
    }
}
