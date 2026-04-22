using System;
using MedInsights.Lib.Entities.Interfaces;

namespace MedInsights.Lib.Dtos
{
    public class PatientContextDto : IAllowHardDelete
    {
        public Guid Id { get; set; } = default!;        // ContextId (from RowKey)
        public Guid PatientId { get; set; } = default!;

        // Patient-specific fields
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateOnly DateOfBirth { get; set; }
        public string Gender { get; set; } = "";

        // Context metadata
        public DateTime DateActivated { get; set; }
    }
}
