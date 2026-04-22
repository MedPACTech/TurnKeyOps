using System;
using System.Collections.Generic;
using MedInsights.Lib.Entities.Interfaces;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
public class PatientAppointmentDto
    {
        public Guid Id { get; set; }                  // Primary key (RowKey)
        public Guid PatientId { get; set; }           // Link to Patient
        public string PatientFirstName { get; set; } = string.Empty;
        public string PatientLastName { get; set; } = string.Empty;
        public Guid AppointmentTypeId { get; set; }   // FK to appointment type definition
        public string AppointmentType { get; set; } = string.Empty; // Snapshot of appointment type name
        public AppointmentLocation AppointmentLocation { get; set; } // Location of appointment
        public AppointmentStatus AppointmentStatus { get; set; } = AppointmentStatus.Scheduled; // e.g. Scheduled, Completed, Cancelled
        public DateTime AppointmentStartTime { get; set; } // UTC
        public DateTime AppointmentEndTime { get; set; } // UTC
        public Guid? PrimaryContactId { get; set; }
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
        public List<string> ValidationWarnings { get; set; } = new();

        // Miscellaneous
        public string Reason { get; set; } = string.Empty; // Reason for visit

        // User details
        public Guid UserId { get; set; } // FK to user
        public string UserName { get; set; } = string.Empty;
        // public string UserFirstName { get; set; } = string.Empty;
        // public string UserLastName { get; set; } = string.Empty;

        // Metadata
        public DateTime DateCreated { get; set; }     // Set by server
        public DateTime? DateUpdated { get; set; }     // Set by server
    }
}
