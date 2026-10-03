using System.Text.Json;

namespace TurnKeyOps.Services;

public static class LocksmithPolicy
{
    public static bool IsConfigured(string? valuesJson)
    {
        if (string.IsNullOrWhiteSpace(valuesJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(valuesJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("locksmith", out var settings) &&
                settings.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }

    public static void Validate(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object) throw new ArgumentException("locksmith settings must be an object.");
        if (!settings.TryGetProperty("techCapabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("techCapabilities must map membership IDs to job types.");
        foreach (var entry in capabilities.EnumerateObject())
        {
            if (!Guid.TryParse(entry.Name, out var id) || id == Guid.Empty || entry.Value.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Tech capabilities require a valid membership ID and job type array.");
            var types = entry.Value.EnumerateArray().ToArray();
            if (types.Any(type => type.ValueKind != JsonValueKind.String || type.GetString() is not ("residential" or "commercial")) ||
                types.Select(type => type.GetString()).Distinct().Count() != types.Length)
                throw new ArgumentException("Tech capabilities must contain unique residential or commercial job types.");
        }
        if (!settings.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Locksmith pricing policy is required.");
        foreach (var key in new[] { "laborRatePerHour", "approvalAboveTotal", "maxDiscountPercent" })
        {
            if (!pricing.TryGetProperty(key, out var value)) throw new ArgumentException($"{key} is required.");
            if (key != "maxDiscountPercent" && value.ValueKind == JsonValueKind.Null) continue;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number) || number < 0 || (key == "maxDiscountPercent" && number > 100))
                throw new ArgumentException($"{key} must be a valid non-negative amount (discount no higher than 100%).");
        }
        if (pricing.TryGetProperty("taxPercent", out var tax) && tax.ValueKind != JsonValueKind.Null &&
            (tax.ValueKind != JsonValueKind.Number || !tax.TryGetDecimal(out var taxPercent) || taxPercent is < 0 or > 100))
            throw new ArgumentException("taxPercent must be between 0 and 100, or unset.");
        if (pricing.TryGetProperty("laborHoursByJobType", out var laborDefaults))
        {
            if (laborDefaults.ValueKind != JsonValueKind.Object) throw new ArgumentException("Labor standards must be grouped by job type.");
            foreach (var group in laborDefaults.EnumerateObject())
            {
                if (group.Name is not ("residential" or "commercial") || group.Value.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Labor standards require residential or commercial job types.");
                foreach (var service in group.Value.EnumerateObject())
                    if (service.Name is not ("Door and frame replacement" or "Lock and hardware installation" or "Repairs" or "Rekeying") ||
                        service.Value.ValueKind != JsonValueKind.Number || !service.Value.TryGetDecimal(out var hours) || hours is <= 0 or > 100)
                        throw new ArgumentException("Labor standards require supported services and hours above 0 through 100.");
            }
        }
        if (settings.TryGetProperty("catalog", out var catalog))
        {
            if (catalog.ValueKind != JsonValueKind.Array || catalog.GetArrayLength() > 1000) throw new ArgumentException("Catalog must contain at most 1,000 items.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in catalog.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 100 || !ids.Add(id.GetString()!) ||
                    !item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()) || name.GetString()!.Length > 200 ||
                    !item.TryGetProperty("unitPrice", out var price) || price.ValueKind != JsonValueKind.Number || !price.TryGetDecimal(out var amount) || amount is < 0 or > 1_000_000 ||
                    !item.TryGetProperty("sample", out var sample) || sample.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !item.TryGetProperty("jobTypes", out var types) || types.ValueKind != JsonValueKind.Array || types.GetArrayLength() == 0 || types.GetArrayLength() > 2 ||
                    types.EnumerateArray().Any(type => type.ValueKind != JsonValueKind.String || type.GetString() is not ("residential" or "commercial")))
                    throw new ArgumentException("Catalog entries need unique IDs, names, bounded prices, an explicit sample flag, and supported job types.");
            }
        }
        if (!pricing.TryGetProperty("requireOfficeApproval", out var approval) || approval.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException("requireOfficeApproval must be a boolean.");
        if (!settings.TryGetProperty("selfBookingEnabled", out var booking) || booking.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException("selfBookingEnabled must be a boolean.");
    }

    public static string[] CapabilitiesFor(string? valuesJson, Guid membershipId)
    {
        if (string.IsNullOrWhiteSpace(valuesJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(valuesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("locksmith", out var settings) || settings.ValueKind != JsonValueKind.Object ||
                !settings.TryGetProperty("techCapabilities", out var mappings) || mappings.ValueKind != JsonValueKind.Object) return [];
            var matching = mappings.EnumerateObject().FirstOrDefault(entry => Guid.TryParse(entry.Name, out var id) && id == membershipId);
            if (matching.Value.ValueKind != JsonValueKind.Array) return [];
            return matching.Value.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String && value.GetString() is "residential" or "commercial")
                .Select(value => value.GetString()!).Distinct().ToArray();
        }
        catch (JsonException) { return []; }
    }
}
