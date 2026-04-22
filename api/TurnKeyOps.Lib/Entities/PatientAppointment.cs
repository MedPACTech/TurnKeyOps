using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities.Interfaces;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Entities
{
    public class PatientAppointment : IEntity, ITableEntity, MedInsights.Lib.Entities.Interfaces.IAllowHardDelete
    {
        // Azure Table Storage required keys
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        // IEntity fields
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        // Core fields
        public string PatientId { get; set; } = default!;
        public string PatientFirstName { get; set; } = default!;
        public string PatientLastName { get; set; } = default!;
        public Guid AppointmentTypeId { get; set; }
        public string AppointmentTypeName { get; set; } = string.Empty;
        // Legacy enum column retained for Azure Table compatibility with existing records.
        public AppointmentType AppointmentType { get; set; } = default!;
        public AppointmentStatus AppointmentStatus { get; set; } = default!;
        public AppointmentLocation AppointmentLocation { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public DateTime AppointmentStartTime { get; set; }
        public DateTime AppointmentEndTime { get; set; }
        public string? PrimaryContactId { get; set; }
        public string PrimaryContactFirstName { get; set; } = string.Empty;
        public string PrimaryContactLastName { get; set; } = string.Empty;
        public string PrimaryContactRelationship { get; set; } = string.Empty;
        public string PrimaryContactPhone { get; set; } = string.Empty;
        public string PrimaryContactEmail { get; set; } = string.Empty;
        public string VisitAddressLine1 { get; set; } = string.Empty;
        public string VisitAddressLine2 { get; set; } = string.Empty;
        public string VisitCity { get; set; } = string.Empty;
        public string VisitState { get; set; } = string.Empty;
        public string VisitPostalCode { get; set; } = string.Empty;
        public string VisitCountry { get; set; } = string.Empty;
        public string Reason { get; set; } = default!;

        // Metadata
        public string CreatedBy { get; set; } = default!;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime? DateUpdated { get; set; }
    }
}

