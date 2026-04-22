using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Models;
using MedInsights.Services.BackgroundServices.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace MedInsights.Services.BackgroundServices;

public class EncounterTranscriptionWorker : BackgroundService
{
    private readonly IEncounterTranscriptionQueue _queue;
    private readonly IAzureSpeechService _transcriptionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EncounterTranscriptionWorker> _logger;

    // Limit concurrency (e.g., 4 parallel jobs max)
    private readonly SemaphoreSlim _concurrencyLimiter = new(4);

    // Retry policy for transient errors
    private readonly AsyncRetryPolicy _retryPolicy;

    public EncounterTranscriptionWorker(
        IEncounterTranscriptionQueue queue,
        IAzureSpeechService transcriptionService,
        IServiceScopeFactory scopeFactory,
        ILogger<EncounterTranscriptionWorker> logger)
    {
        _queue = queue;
        _transcriptionService = transcriptionService;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, ts, retryCount, ctx) =>
                {
                    _logger.LogWarning(ex, "Retry {RetryCount} after {Delay}s", retryCount, ts.TotalSeconds);
                });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.DequeueAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            // Fire-and-forget job execution with concurrency control
            _ = ProcessJobAsync(job, stoppingToken)
                .ContinueWith(_ => _concurrencyLimiter.Release(), TaskScheduler.Default);
        }
    }

    private async Task ProcessJobAsync(EncounterTranscriptionJob job, CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var encounterWorkerService = scope.ServiceProvider.GetRequiredService<IEncounterWorkerService>();
        var blobStorageService = scope.ServiceProvider.GetRequiredService<IAzureBlobStorageService>();

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Starting transcription for blob {BlobName}", job.rowKey);

                var encounter = await encounterWorkerService.GetAsync(job.partitionKey, job.rowKey);
                if (encounter == null)
                {
                    _logger.LogWarning("Encounter not found for {BlobName}", job.rowKey);
                    return;
                }

                encounter.Status = "Processing";
                await encounterWorkerService.UpdateAsync(encounter);

                string? audioFileUrl = null;
                if (!string.IsNullOrWhiteSpace(encounter.Data))
                {
                    try
                    {
                        using var json = JsonDocument.Parse(encounter.Data);
                        if (json.RootElement.TryGetProperty("audioFileUrl", out var value))
                            audioFileUrl = value.GetString();
                    }
                    catch
                    {
                        // Best effort parse; invalid data will fail below.
                    }
                }

                if (string.IsNullOrWhiteSpace(audioFileUrl))
                    throw new InvalidOperationException("Encounter audio file reference is missing.");

                // Download audio from blob
                var audioBytes = await blobStorageService.Get("encounters", audioFileUrl);

                // Temp files
                var webmPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():D}.webm");
                var wavPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():D}.wav");

                try
                {
                    await File.WriteAllBytesAsync(webmPath, audioBytes, token);

                    // Convert to WAV
                    await Xabe.FFmpeg.FFmpeg.Conversions.New()
                        .AddParameter($"-i \"{webmPath}\" -ac 1 -ar 16000 -sample_fmt s16 \"{wavPath}\"")
                        .Start(token);

                    // Load WAV into memory
                    byte[] wavBytes = await File.ReadAllBytesAsync(wavPath, token);
                    using var wavStream = new MemoryStream(wavBytes);

                    // Transcribe
                    string transcript = await _transcriptionService.TranscribeConversationAsync(
                        wavStream, "en-US", token);

                    // Update encounter
                    encounter.EncounterBody = transcript;
                    encounter.Status = "Completed";
                    await encounterWorkerService.UpdateAsync(encounter);

                    _logger.LogInformation("Completed transcription for {BlobName}", job.rowKey);
                }
                finally
                {
                    if (File.Exists(webmPath)) File.Delete(webmPath);
                    if (File.Exists(wavPath)) File.Delete(wavPath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed transcription for blob {BlobName}", job.rowKey);

            try
            {
                var encounter = await encounterWorkerService.GetAsync(job.partitionKey, job.rowKey);
                if (encounter != null)
                {
                    await encounterWorkerService.MarkFailedAsync(encounter, $"Error: {ex.Message}");
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to mark encounter as failed for {BlobName}", job.rowKey);
            }
        }
    }
}
