
namespace MedInsights.Lib.Utils
{
    public static class RepositoryKeyHelper
    {
        private static string _keyGuidFormat = "N";

        public static string KeyGuidFormat => _keyGuidFormat;

        public static void ConfigureGuidKeyFormat(string? guidFormat)
        {
            _keyGuidFormat = NormalizeGuidFormat(guidFormat);
        }

        private static string NormalizeGuidFormat(string? guidFormat)
        {
            if (string.IsNullOrWhiteSpace(guidFormat))
                return "N";

            var normalized = guidFormat.Trim().ToUpperInvariant();
            return normalized is "N" or "D" ? normalized : "N";
        }

        private static string FormatGuid(Guid value) => value.ToString(_keyGuidFormat);

        /// <summary>
        /// Converts a Guid to a String PartitionKey.
        /// </summary>
        public static string ToPartitionKey(Guid partitionKey)
        {
            return FormatGuid(partitionKey);
        }

        /// <summary>
        /// Builds a PartitionKey by combining TenantId and UserId.
        /// Format: TENANT={tenantId}|USER={userId}
        /// </summary>
        public static string ToTenantUserPartitionKey(Guid tenantId, Guid userId)
        {
            return $"TENANT={FormatGuid(tenantId)}|USER={FormatGuid(userId)}";
        }

        /// <summary>
        /// Builds a PartitionKey by combining TenantId and UserId.
        /// Format: TENANT={tenantId}|USER={userId}
        /// </summary>
        public static string ToTenantPatientPartitionKey(Guid tenantId, Guid patientId)
        {
            return $"TENANT={FormatGuid(tenantId)}|PATIENT={FormatGuid(patientId)}";
        }

        /// <summary>
        /// Converts a Guid to a String RowKey.
        /// </summary>
        public static string ToRowKey(Guid rowKey)
        {
            return FormatGuid(rowKey);
        }

        /// <summary>
        /// Builds a RowKey using Id + UTC DateTime.
        /// Ensures lexicographic order by Id and timestamp while guaranteeing uniqueness.
        /// If you do not pass a UTC timestamp, the current time will be used.
        /// Example: ecbb9dd5-4e87-465e-9af7-231ec3ae437a|20251014T192200Z
        /// </summary>
        public static string ToOrderedRowKey(Guid id, DateTime? utcTimestamp = null)
        {
            var timeStamp = DateTimeHelper.GetUniversalTimeStamp(utcTimestamp);
            return $"{FormatGuid(id)}|{timeStamp}";
        }

        /// <summary>
        /// Parses a PartitionKey back into its component Guids.
        /// Handles keys of format TENANT={tenantId}|USER={userId}.
        /// </summary>
        public static (Guid TenantId, Guid UserId) FromTenantUserPartitionKey(string partitionKey)
        {
            var parts = partitionKey.Split('|');
            var tenantId = parts[0].Replace("TENANT=", "");
            var userId = parts[1].Replace("USER=", "");
            return (Guid.Parse(tenantId), Guid.Parse(userId));
        }

        /// <summary>
        /// Parses a PartitionKey back into its component Guid.
        /// Handles keys of format {tenantId}.
        /// </summary>
        public static Guid FromPartitionKey(string partitionKey)
        {
            return Guid.Parse(partitionKey);
        }

        /// <summary>
        /// Parses a RowKey back into its component Guid.
        /// Handles keys of format {rowKey}.
        /// </summary>
        public static Guid FromRowKey(string rowKey)
        {
            return Guid.Parse(rowKey);
        }


        /// <summary>
        /// Parses the Guid as string back from an Ordered RowKey.
        /// Handles keys of format {rowKey|timestamp} and return prefix {rowKey|}.
        /// </summary>
        public static string GetOrderedRowKeyPrefix(Guid rowKey)
        {
            return $"{FormatGuid(rowKey)}|";
        }  
        
        public static string BuildPartitionKey(Guid tenantId, DateTime date)
            => $"TENANT|{tenantId}|MONTH|{date:yyyyMM}";

        public static string BuildRowKey(DateTime date, Guid userId)
            => $"DATE|{date:yyyyMMdd}|USER|{userId}";

        public static string BuildItemRowKey(DateTime date, Guid userId, Guid? facilityId)
            => facilityId.HasValue
                ? $"DATE|{date:yyyyMMdd}|FAC|{facilityId.Value}|USER|{userId}|ITEM|{Guid.NewGuid()}"
                : $"DATE|{date:yyyyMMdd}|USER|{userId}|ITEM|{Guid.NewGuid()}";

    }
}
