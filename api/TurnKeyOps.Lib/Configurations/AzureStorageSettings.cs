using System.ComponentModel.DataAnnotations;

namespace MedInsights.Lib.Configurations
{

    public sealed class AzureStorageSettings
    {
        [Required]
        public required string ConnectionString { get; init; }
    }
}
