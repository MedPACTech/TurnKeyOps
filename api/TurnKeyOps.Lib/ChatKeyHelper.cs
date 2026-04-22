using System;

namespace MedInsights.Lib
{
    //TODO: Can this be deleted?  There is one reference to BuildRowKey but we have a similar helper in RepositoryKeyHelper.cs

    public static class ChatKeyHelper
    {
        /// <summary>
        /// Builds a PartitionKey by combining TenantId and UserId.
        /// Format: TENANT={tenantId}|USER={userId}
        /// </summary>
        public static string BuildPartitionKey(Guid tenantId, Guid userId)
        {
            return $"TENANT={tenantId}|USER={userId}";
        }

        /// <summary>
        /// Builds a RowKey using TopicId + Ticks + MessageId.
        /// Ensures lexicographic order by topic and timestamp while guaranteeing uniqueness.
        /// Example: fa7e5f41bf334e3d9640f55c8c1a2131_000000638624149235678901_9e5f84da2b254d1e902fefbb2df5431d
        /// </summary>
        public static string BuildRowKey(Guid topicId, Guid messageId, DateTime? timestamp = null)
        {
            var dt = timestamp ?? DateTime.UtcNow;
            long ticks = dt.Ticks;
            return $"{topicId:D}_{ticks:D19}_{messageId:D}";
        }

        /// <summary>
        /// Parses a PartitionKey back into its component TenantId and UserId.
        /// Handles keys of format TENANT={tenantId}|USER={userId}.
        /// </summary>
        public static (Guid TenantId, Guid UserId) ParsePartitionKey(string partitionKey)
        {
            var parts = partitionKey.Split('|');
            var tenantId = parts[0].Replace("TENANT=", "");
            var userId = parts[1].Replace("USER=", "");
            return (Guid.Parse(tenantId), Guid.Parse(userId));
        }

        /// <summary>
        /// Parses a RowKey back into TopicId, Ticks, and MessageId.
        /// </summary>
        public static (Guid TopicId, long Ticks) ParseRowKey(string rowKey)
        {
            var parts = rowKey.Split('_');
            var topicId = Guid.Parse(parts[0]);
            var ticks = long.Parse(parts[1]);
            //var messageId = Guid.Parse(parts[2]);
            return (topicId, ticks);
        }

        /// <summary>
        /// Generates both PartitionKey and RowKey for a chat message.
        /// </summary>
        public static (string PartitionKey, string RowKey) GenerateKeys(
            Guid tenantId,
            Guid userId,
            Guid topicId,
            Guid messageId,
            DateTime? timestamp = null)
        {
            var pk = BuildPartitionKey(tenantId, userId);
            var rk = BuildRowKey(topicId, messageId, timestamp);
            return (pk, rk);
        }
    }
}
