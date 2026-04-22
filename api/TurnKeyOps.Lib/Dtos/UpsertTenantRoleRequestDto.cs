namespace MedInsights.Lib.Dtos
{
    public sealed class UpsertTenantRoleRequestDto
    {
        public Guid? Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAssignable { get; set; } = true;
        public List<string> PermissionKeys { get; set; } = new();
        public List<Guid> PermissionIds { get; set; } = new();
    }
}
