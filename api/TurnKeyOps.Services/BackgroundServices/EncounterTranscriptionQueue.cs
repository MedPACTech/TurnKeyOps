using System.Text.Json;
using Azure.Storage.Queues;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Models;
using MedInsights.Services.BackgroundServices.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services.BackgroundServices
{
    public class EncounterTranscriptionQueue : IEncounterTranscriptionQueue
    {
        private readonly QueueClient _queueClient;

        public EncounterTranscriptionQueue(IOptions<AzureStorageSettings> storageOptions)
        {
            var settings = storageOptions.Value;
            var options = BuildQueueClientOptions(settings.ConnectionString);

            _queueClient = new QueueClient(
                settings.ConnectionString,
                settings.EncounterTranscriptionQueueName ?? "encounter-jobs",
                options);

            _queueClient.CreateIfNotExists();
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

        public async ValueTask QueueJobAsync(EncounterTranscriptionJob job)
        {
            var json = JsonSerializer.Serialize(job);
            await _queueClient.SendMessageAsync(json);
        }

        public IAsyncEnumerable<EncounterTranscriptionJob> DequeueAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException("Dequeue is handled by Azure Functions now.");
    }
}
