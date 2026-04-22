namespace MedInsights.Lib.Dtos
{
    public sealed class UpdateRolePermissionsRequestDto
    {
        public List<string> PermissionKeys { get; set; } = new();
        public List<Guid> PermissionIds { get; set; } = new();
    }
}
