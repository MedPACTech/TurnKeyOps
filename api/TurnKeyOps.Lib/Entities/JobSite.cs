using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class JobSite : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    // --- Location ---
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Weather.gov grid endpoint cached after first geocode.</summary>
    public string? WeatherGridUrl { get; set; }

    public string? Notes { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
