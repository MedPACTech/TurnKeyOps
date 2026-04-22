using Azure.Data.Tables;
using System.Text.Json.Nodes;

var userId = Guid.Parse("74e1b1e4-5abb-498e-b573-8cc61133bd36");
var client = new TableClient("UseDevelopmentStorage=true", "TenantMemberships");

await foreach (var entity in client.QueryAsync<TableEntity>(maxPerPage: 50))
{
    var data = entity.GetString("Data");
    if (string.IsNullOrWhiteSpace(data))
        continue;

    var node = JsonNode.Parse(data)?.AsObject();
    var entityUserId = node?["userId"]?.GetValue<string>();
    if (!Guid.TryParse(entityUserId, out var parsedUserId) || parsedUserId != userId)
        continue;

    Console.WriteLine($"ProjectedRole={entity.GetString("Role")}");
    Console.WriteLine($"Data.role={node?["role"]}");
    Console.WriteLine($"Data.isOwner={node?["isOwner"]}");
    Console.WriteLine($"Data.isBillingAdmin={node?["isBillingAdmin"]}");
    Console.WriteLine($"TenantId={node?["tenantId"]}");
}
