namespace MedInsights.Lib.Enums

{
    public enum AppointmentStatus
    {
        Scheduled = 1,     // Future appointment that is booked
        Confirmed = 2,     // Patient confirmed (optional if you track confirmations)
        CheckedIn = 3,     // Patient has arrived
        In_Progress = 4,    // Appointment has started
        Completed = 5,     // Appointment finished
        Cancelled = 6,     // Cancelled before it occurred
        No_Show = 7,        // Patient did not arrive
        Rescheduled = 8    // Appointment was rescheduled
    }
}