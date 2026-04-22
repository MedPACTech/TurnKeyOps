namespace MedInsights.Lib.Dtos
{
    public sealed class TenantRoleDto
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsAssignable { get; set; }
        public bool GrantsOwnership { get; set; }
        public bool GrantsBillingAdmin { get; set; }
        public List<PermissionDefinitionDto> Permissions { get; set; } = new();
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
