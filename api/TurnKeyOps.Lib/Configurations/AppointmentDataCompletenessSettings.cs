namespace MedInsights.Lib.Configurations
{
    public enum AppointmentValidationMode
    {
        Soft = 1,
        Strict = 2
    }

    public sealed class AppointmentDataCompletenessSettings
    {
        public AppointmentValidationMode ValidationMode { get; set; } = AppointmentValidationMode.Soft;
    }
}
