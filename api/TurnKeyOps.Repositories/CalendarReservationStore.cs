using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories;

// The guard and event share a tenant partition. Every Calendar writer changes the
// guard in the same atomic batch as the event, fencing the availability snapshot.
public sealed class CalendarReservationStore
{
    internal const string GuardRow = "!schedule-version";
    private readonly TableClient table;
    private readonly bool createTables;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public CalendarReservationStore(IConfiguration configuration)
    {
        table = new TableServiceClient(configuration["IBeam:Repositories:AzureTables:ConnectionString"]
            ?? throw new InvalidOperationException("Calendar storage is not configured."))
            .GetTableClient((configuration["IBeam:Repositories:AzureTables:TableNamePrefix"] ?? "") + "CalendarEvents");
        createTables = configuration["IBeam:Repositories:AzureTables:CreateTablesIfNotExists"] != "false";
    }
    private static TableEntity Envelope(CalendarEvent value) => new(value.PartitionKey, value.RowKey)
    {
        ["Type"] = typeof(CalendarEvent).FullName,
        ["Data"] = JsonSerializer.Serialize(value, Json)
    };
    public async Task<CalendarEvent> SaveAsync(CalendarEvent value, CancellationToken ct)
    {
        if (value.RowKey == GuardRow || string.IsNullOrWhiteSpace(value.PartitionKey) || value.Id == Guid.Empty)
            throw new ArgumentException("A tenant Calendar event identity is required.");
        if (value.ETag == ETag.All) throw new ArgumentException("A current Calendar event version is required.");
        if (createTables) await table.CreateIfNotExistsAsync(ct);
        var guardValue = new CalendarEvent { PartitionKey = value.PartitionKey, RowKey = GuardRow, IsDeleted = true };
        try { await table.AddEntityAsync(Envelope(guardValue), ct); }
        catch (RequestFailedException ex) when (ex.Status == 409) { }
        var create = string.IsNullOrEmpty(value.ETag.ToString());
        for (var attempt = 0; attempt < 8; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var guard = (await table.GetEntityAsync<TableEntity>(value.PartitionKey, GuardRow, cancellationToken: ct)).Value;
            var current = await table.GetEntityIfExistsAsync<TableEntity>(value.PartitionKey, value.RowKey, cancellationToken: ct);
            if (create ? current.HasValue : !current.HasValue || current.Value.ETag != value.ETag)
                throw new InvalidOperationException("The Calendar event changed. Reload before saving.");
            if (!value.IsDeleted && value.EventStatus == "scheduled")
            {
                if (value.EndUtc <= value.StartUtc) throw new ArgumentException("The event end must follow its start.");
                await foreach (var row in table.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {value.PartitionKey}"), cancellationToken: ct))
                {
                    if (row.RowKey == GuardRow || row.RowKey == value.RowKey) continue;
                    var other = JsonSerializer.Deserialize<CalendarEvent>(row.GetString("Data")!, Json)
                        ?? throw new InvalidOperationException("Invalid Calendar event.");
                    if (Conflicts(value, other)) throw new ArgumentException("People or equipment already have an overlapping Calendar assignment.");
                }
            }
            guard["Revision"] = Guid.NewGuid().ToString("N");
            try
            {
                var result = await table.SubmitTransactionAsync(new[] {
                    new TableTransactionAction(TableTransactionActionType.UpdateReplace, guard, guard.ETag),
                    new TableTransactionAction(create ? TableTransactionActionType.Add : TableTransactionActionType.UpdateReplace, Envelope(value), value.ETag)
                }, ct);
                value.ETag = result.Value[1].Headers.ETag ?? throw new InvalidOperationException("Calendar did not return a record version.");
                return value;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                // Re-read the guard and events; keep the caller's original event ETag.
                // A concurrent edit to this event must still fail, never overwrite it.
            }
        }
        throw new InvalidOperationException("Calendar changed repeatedly. Refresh and retry.");
    }
    private static bool Conflicts(CalendarEvent value, CalendarEvent other)
    {
        if (other.IsDeleted || other.EventStatus != "scheduled" || other.StartUtc >= value.EndUtc || other.EndUtc <= value.StartUtc) return false;
        static IEnumerable<Guid> People(CalendarEvent e) => e.MembershipIds.Concat(e.AssignedTechnicianMembershipId is Guid id ? new[] { id } : []);
        return People(value).Intersect(People(other)).Any() || value.ResourceIds.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).Intersect(other.ResourceIds.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase).Any();
    }
}
