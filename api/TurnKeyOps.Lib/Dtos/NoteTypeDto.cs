namespace MedInsights.Lib.Dtos
{
    public class NoteTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool HasParentNote { get; set; }
        public bool IsSystem { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
