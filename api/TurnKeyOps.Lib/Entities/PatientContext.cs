using System.Runtime.ExceptionServices;
using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities.Interfaces;

namespace MedInsights.Lib.Entities
{
  public class PatientContext : IEntity, ITableEntity, MedInsights.Lib.Entities.Interfaces.IAllowHardDelete
  {
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }

    public string PatientId { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = default!;
    public DateTime DateActivated { get; set; }
  }
}

