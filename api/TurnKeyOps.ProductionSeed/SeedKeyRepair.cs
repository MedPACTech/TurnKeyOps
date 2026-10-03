using Azure.Data.Tables;
using System.Text.Json.Nodes;

static class SeedKeyRepair
{
    // Explicit one-time repair for the original bootstrap's undashed application keys.
    public static async Task RunAsync(TableServiceClient service, Guid[] tenants)
    {
        foreach (var name in new[] { "TenantMemberships", "TenantProfiles" })
        {
            var table = service.GetTableClient(name);
            foreach (var tenant in tenants)
            {
                var pk = tenant.ToString("N");
                var rows = new List<TableEntity>();
                await foreach (var row in table.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {pk}"))) rows.Add(row);
                foreach (var row in rows)
                {
                    var data = JsonNode.Parse(row.GetString("Data")!)!.AsObject();
                    if (data["isDeleted"]?.GetValue<bool>() == true) throw new InvalidOperationException("Refusing deleted seed row repair.");
                    var id = Guid.Parse(data["id"]!.GetValue<string>());
                    var newPk = tenant.ToString("D");
                    var newRk = id.ToString("D");
                    data["partitionKey"] = newPk; data["rowKey"] = newRk;
                    var replacement = new TableEntity(row.ToDictionary(x => x.Key, x => x.Value));
                    replacement.PartitionKey = newPk; replacement.RowKey = newRk;
                    replacement["Data"] = data.ToJsonString();
                    var current = await table.GetEntityIfExistsAsync<TableEntity>(newPk, newRk);
                    if (current.HasValue)
                    {
                        if (!JsonNode.DeepEquals(JsonNode.Parse(current.Value!.GetString("Data")!), data))
                            throw new InvalidOperationException("Conflicting canonical row; manual reconciliation required.");
                    }
                    else await table.AddEntityAsync(replacement);
                    var verified = await table.GetEntityAsync<TableEntity>(newPk, newRk);
                    if (!JsonNode.DeepEquals(JsonNode.Parse(verified.Value.GetString("Data")!), data)) throw new InvalidOperationException("Repair verification failed.");
                    await table.DeleteEntityAsync(row.PartitionKey, row.RowKey, row.ETag);
                    Console.WriteLine($"REPAIRED {name}: canonical key format.");
                }
            }
        }
    }
}
