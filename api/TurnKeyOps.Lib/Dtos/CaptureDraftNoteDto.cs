using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
    public class CaptureDraftNoteDto
    {
        public Guid Id { get; set; }
        public Guid? ProviderId { get; set; }
        public Guid? PatientId { get; set; }
        
        public string CaptureSourceType { get; set; } = string.Empty; //Narrative, Audio, etc
        public Guid? CaptureSourceId { get; set; }
        public string CaptureSourceText { get; set; } = string.Empty;
        public string CaptureSourceAddendum { get; set; } = string.Empty; // additional info appended to source
        public string CaptureStatus { get; set; } = string.Empty;
        public string? NoteType { get; set; }
        public string NoteTitle { get; set; } = string.Empty;
        public string NoteBody { get; set; } = string.Empty;
        public string BillingBody { get; set; } = string.Empty;
        public string CommunicationBody { get; set; } = string.Empty; // this may go away later
                         
        
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public Guid CreatedBy { get; set; }
        public string Tags { get; set; } = string.Empty; // optional metadata
    }
}
