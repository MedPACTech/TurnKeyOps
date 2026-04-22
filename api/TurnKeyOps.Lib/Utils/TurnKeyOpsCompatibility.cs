using AppTimeZone = MedInsights.Lib.Utils.AppTimeZone;

namespace TurnKeyOps.Lib.Utils;

public interface IUserContext
{
    bool IsAuthenticated { get; }
    Guid TenantId { get; }
    Guid UserId { get; }
    AppTimeZone Timezone { get; }
    string FirstName { get; }
    string LastName { get; }
}

public sealed class UserContextAdapter : IUserContext
{
    private readonly MedInsights.Lib.Utils.IUserContext _inner;

    public UserContextAdapter(MedInsights.Lib.Utils.IUserContext inner)
    {
        _inner = inner;
    }

    public bool IsAuthenticated => _inner.IsAuthenticated;
    public Guid TenantId => _inner.TenantId;
    public Guid UserId => _inner.UserId;
    public AppTimeZone Timezone => _inner.Timezone;
    public string FirstName => _inner.FirstName;
    public string LastName => _inner.LastName;
}

public static class RepositoryKeyHelper
{
    public static string KeyGuidFormat => MedInsights.Lib.Utils.RepositoryKeyHelper.KeyGuidFormat;

    public static void ConfigureGuidKeyFormat(string? guidFormat) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.ConfigureGuidKeyFormat(guidFormat);

    public static string ToPartitionKey(Guid partitionKey) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.ToPartitionKey(partitionKey);

    public static string ToTenantPartitionKey(Guid tenantId) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.ToPartitionKey(tenantId);

    public static string ToTenantJobPartitionKey(Guid tenantId, Guid jobId) =>
        $"TENANT={tenantId.ToString(KeyGuidFormat)}|JOB={jobId.ToString(KeyGuidFormat)}";

    public static string ToTenantCustomerPartitionKey(Guid tenantId, Guid customerId) =>
        $"TENANT={tenantId.ToString(KeyGuidFormat)}|CUSTOMER={customerId.ToString(KeyGuidFormat)}";

    public static string ToTenantUserPartitionKey(Guid tenantId, Guid userId) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.ToTenantUserPartitionKey(tenantId, userId);

    public static string ToTenantEstimatePartitionKey(Guid tenantId, Guid estimateId) =>
        $"TENANT={tenantId.ToString(KeyGuidFormat)}|ESTIMATE={estimateId.ToString(KeyGuidFormat)}";

    public static string ToTenantInvoicePartitionKey(Guid tenantId, Guid invoiceId) =>
        $"TENANT={tenantId.ToString(KeyGuidFormat)}|INVOICE={invoiceId.ToString(KeyGuidFormat)}";

    public static string ToRowKey(Guid rowKey) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.ToRowKey(rowKey);

    public static string ToOrderedRowKey(Guid id, DateTime? utcTimestamp = null) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.ToOrderedRowKey(id, utcTimestamp);

    public static string GetOrderedRowKeyPrefix(Guid rowKey) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.GetOrderedRowKeyPrefix(rowKey);

    public static (Guid TenantId, Guid UserId) FromTenantUserPartitionKey(string partitionKey) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.FromTenantUserPartitionKey(partitionKey);

    public static Guid FromPartitionKey(string partitionKey) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.FromPartitionKey(partitionKey);

    public static Guid FromRowKey(string rowKey) =>
        MedInsights.Lib.Utils.RepositoryKeyHelper.FromRowKey(rowKey);
}
