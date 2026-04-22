using System.Text.Json;
using Azure.Storage.Queues;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Models;
using MedInsights.Services.BackgroundServices.Interfaces;
using Microsoft.Extensions.Options;

public class AudioCaptureTranscriptionQueue : IAudioCaptureTranscriptionQueue
{
    private readonly QueueClient _queueClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AudioCaptureTranscriptionQueue(IOptions<AzureStorageSettings> storageOptions)
    {
        var settings   = storageOptions.Value;
        var connString = settings.ConnectionString;
        var queueName  = settings.AudioTranscriptionQueueName;
        var options    = BuildQueueClientOptions(connString);

        options.MessageEncoding = QueueMessageEncoding.Base64;
        _queueClient = new QueueClient(connString, queueName, options);

        _queueClient.CreateIfNotExists();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task QueueJobAsync(AudioCaptureTranscriptionJob job, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(job, _jsonOptions);
        await _queueClient.SendMessageAsync(payload, cancellationToken: ct);
    }

    private static QueueClientOptions BuildQueueClientOptions(string connectionString)
    {
        // Azurite can lag behind latest SDK API versions; pin local emulator calls to a supported version.
        if (connectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueClientOptions(QueueClientOptions.ServiceVersion.V2025_11_05);
        }

        return new QueueClientOptions();
    }
}
