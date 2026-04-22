using System.Net.Http.Json;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Xabe.FFmpeg;

public class AudioTranscriptionWorker
{
    private readonly IAzureBlobStorageService _blobStorage;
    private readonly IAzureSpeechService _speech;
    private readonly HttpClient _http;
    private readonly ILogger<AudioTranscriptionWorker> _logger;

    public AudioTranscriptionWorker(
        IAzureBlobStorageService blobStorage,
        IAzureSpeechService speech,
        IHttpClientFactory httpClientFactory,
        ILogger<AudioTranscriptionWorker> logger)
    {
        _blobStorage = blobStorage;
        _speech      = speech;
        _http        = httpClientFactory.CreateClient("TranscriptionAPI");
        _logger      = logger;
    }

   [Function("audio-transcription-worker")]
        public async Task RunAsync(
            [QueueTrigger("%AzureStorageSettings:AudioTranscriptionQueueName%", Connection = "AzureStorageSettings:ConnectionString")]
            AudioCaptureTranscriptionJob job,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "🎧 AudioTranscriptionWorker received job. PartitionKey={PartitionKey}, RowKey={RowKey}, Blob={Container}/{Blob}",
                job.PartitionKey,
                job.RowKey,
                job.AudioBlobContainer,
                job.AudioBlobName);


        // 1. Download audio
        var audioBytes = await _blobStorage.Get(job.AudioBlobContainer, job.AudioBlobName);
        if (audioBytes == null || audioBytes.Length == 0)
        {
            _logger.LogWarning("Audio blob not found or empty for PartitionKey={PartitionKey}, RowKey={RowKey}", job.PartitionKey, job.RowKey);
            throw new InvalidOperationException("Audio blob missing"); // let queue retry/poison
        }

        // 2. Convert WebM -> WAV
        var webmPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.webm");
        var wavPath  = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        try
        {
            await File.WriteAllBytesAsync(webmPath, audioBytes, cancellationToken);

            await FFmpeg.Conversions.New()
                .AddParameter($"-i \"{webmPath}\" -ac 1 -ar 16000 -sample_fmt s16 \"{wavPath}\"")
                .Start(cancellationToken);

            var wavBytes = await File.ReadAllBytesAsync(wavPath, cancellationToken);

            // 3. Transcribe with Azure Speech
            using var wavStream = new MemoryStream(wavBytes);
            var transcript = await _speech.TranscribeDictationAsync(
                wavStream,
                "en-US",
                cancellationToken);

            // 4. Callback to API
            var resultPayload = new AudioCaptureDto
            {
                Id   = Guid.Parse(job.RowKey),
                Status          = "Completed",
                ProcessingStage = "COmpleted",
                TranscribedText = transcript,
                JobKey         = job.PartitionKey,
                JobToken       = job.JobToken

            };

            var request = new HttpRequestMessage(HttpMethod.Post, job.CallbackPath)
            {
                Content = JsonContent.Create(resultPayload)
            };

            if (!string.IsNullOrEmpty(job.JobToken))
            {
                request.Headers.Add("X-Audio-Job-Token", job.JobToken);
            }

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "❌ API callback failed for RowKey={RowKey}. Status={StatusCode}, Body={Body}",
                    job.RowKey,
                    response.StatusCode,
                    body);

                // Let queue retry / go to poison
                throw new InvalidOperationException(
                    $"API callback failed: {response.StatusCode}");
            }

            _logger.LogInformation(
                "✅ Completed transcription and RowKey for CorrelationId={RowKey}",
                job.RowKey);
        }
        finally
        {
            if (File.Exists(webmPath)) File.Delete(webmPath);
            if (File.Exists(wavPath))  File.Delete(wavPath);
        }
    }
}
