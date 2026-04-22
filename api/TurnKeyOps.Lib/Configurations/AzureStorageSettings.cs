using System.ComponentModel.DataAnnotations;

namespace MedInsights.Lib.Configurations
{

    public sealed class AzureStorageSettings
    {
        [Required]
        public required string ConnectionString { get; init; }

        [Required]
        public required string QueueName { get; init; }

        [Required]
        public required string EncounterTranscriptionQueueName { get; init; }

        [Required]
        public required string AudioTranscriptionQueueName { get; init; }
    }
}
