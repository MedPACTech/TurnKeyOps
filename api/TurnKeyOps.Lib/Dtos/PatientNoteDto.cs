using System.ComponentModel.DataAnnotations;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
    public class PatientNoteDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? NoteTypeId { get; set; }
        public Guid? NoteTypeProfileId { get; set; }
        public string NoteBody { get; set; } = string.Empty;
        public NoteCategory Category { get; set; } = default!; // e.g., "Social", "PersonalPreference"
        public NoteVisibility Visibility { get; set; } = default!; // Private, Team, AllStaff
        public string Tags { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    }
}
