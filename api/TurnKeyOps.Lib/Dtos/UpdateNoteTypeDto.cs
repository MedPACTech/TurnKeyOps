namespace MedInsights.Lib.Dtos
{
    public class UpdateNoteTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Description { get; set; }
        public bool HasParentNote { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool? IsDefault { get; set; }
        public int SortOrder { get; set; }
    }
}
