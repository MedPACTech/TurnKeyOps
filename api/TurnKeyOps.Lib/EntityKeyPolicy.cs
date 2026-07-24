using MedInsights.Lib.Utils;

namespace MedInsights.Lib;

public static class EntityKeyPolicy
{
    public static string TenantPartition(Guid? tenantId)
        => tenantId.HasValue ? RepositoryKeyHelper.ToPartitionKey(tenantId.Value) : string.Empty;

    public static string TenantUserPartition(Guid? tenantId, Guid userId)
        => tenantId.HasValue ? RepositoryKeyHelper.ToTenantUserPartitionKey(tenantId.Value, userId) : string.Empty;

    public static string Row(Guid id) => RepositoryKeyHelper.ToRowKey(id);
}
