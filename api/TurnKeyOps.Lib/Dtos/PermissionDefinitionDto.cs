namespace MedInsights.Lib.Dtos
{
    public sealed class PermissionDefinitionDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
