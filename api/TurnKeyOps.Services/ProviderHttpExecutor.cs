using System.Net;
using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services;

public sealed class ProviderHttpExecutor : IProviderHttpExecutor
{
    private readonly BillingIntegrationOptions _options;
    private readonly object _circuitLock = new();
    private int _consecutiveFailures;
    private DateTimeOffset _circuitOpenUntilUtc;

    public ProviderHttpExecutor(IOptions<BillingIntegrationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ProviderHttpResponse> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        bool retrySafe,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ThrowIfCircuitOpen();

        var attempts = retrySafe ? Math.Clamp(_options.RetryCount, 0, 5) + 1 : 1;
        Exception? finalException = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var request = requestFactory();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 5, 120)));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                var payload = await response.Content.ReadAsStringAsync(timeout.Token);

                if (response.IsSuccessStatusCode)
                {
                    ResetCircuit();
                    return new ProviderHttpResponse(response.StatusCode, payload);
                }

                if (!IsTransient(response.StatusCode) || attempt == attempts - 1)
                {
                    RegisterFailure();
                    throw new NonTransientProviderException(
                        $"Provider request failed with status {(int)response.StatusCode}.",
                        response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                finalException = new TimeoutException("Provider request timed out.");
                if (attempt == attempts - 1)
                    break;
            }
            catch (NonTransientProviderException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                finalException = ex;
                if (attempt == attempts - 1)
                    break;
            }

            await Task.Delay(Backoff(attempt), ct);
        }

        RegisterFailure();
        throw finalException ?? new HttpRequestException("Provider request failed after retry attempts.");
    }

    private void ThrowIfCircuitOpen()
    {
        lock (_circuitLock)
        {
            if (_circuitOpenUntilUtc > DateTimeOffset.UtcNow)
                throw new HttpRequestException("Provider circuit is temporarily open.");

            if (_circuitOpenUntilUtc != default)
            {
                _circuitOpenUntilUtc = default;
                _consecutiveFailures = 0;
            }
        }
    }

    private void RegisterFailure()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures++;
            var threshold = Math.Clamp(_options.CircuitBreakerFailureThreshold, 1, 20);
            if (_consecutiveFailures >= threshold)
            {
                _circuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Clamp(_options.CircuitBreakerDurationSeconds, 5, 300));
            }
        }
    }

    private void ResetCircuit()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures = 0;
            _circuitOpenUntilUtc = default;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(2000, 200 * Math.Pow(2, attempt)));

    private sealed class NonTransientProviderException : HttpRequestException
    {
        public NonTransientProviderException(string message, HttpStatusCode statusCode)
            : base(message, null, statusCode)
        {
        }
    }
}
