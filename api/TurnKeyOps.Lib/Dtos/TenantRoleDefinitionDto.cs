namespace MedInsights.Lib.Dtos
{
    public sealed class TenantRoleDefinitionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAssignable { get; set; }
        public bool GrantsOwnership { get; set; }
        public bool GrantsBillingAdmin { get; set; }
    }
}
