namespace MedInsights.Lib.Dtos
{
    public class ActivityLogItemDto
    {
        public string Type { get; set; } = default!;  // "hours" | "count"
        public string Key { get; set; } = default!;   // "training_room", "surgery", "tkr", etc.
        public double Value { get; set; }
        public string? Unit { get; set; }            // "hours", "count", "$", etc.
        public string? UserFirstName { get; set; } = default!;
        public string? UserLastName { get; set;} = default!;
    }
}
