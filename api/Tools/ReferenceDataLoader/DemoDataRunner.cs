using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Data.Tables;

internal static class DemoDataRunner
{
    private static readonly string[] OrderedModules =
    [
        "patients",
        "contacts",
        "allergies",
        "family-history",
        "vitals",
        "marital-history",
        "military-history",
        "environmental-history"
    ];

    public static async Task<int> RunAsync(LoaderOptions options)
    {
        if (!options.TenantId.HasValue || options.TenantId.Value == Guid.Empty)
        {
            Console.Error.WriteLine("Demo mode requires --tenant-id <guid>.");
            return 1;
        }

        try
        {
            var repoRoot = ConnectionStringResolver.FindRepoRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
            var connectionString =
                options.ConnectionString
                ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
                ?? ConnectionStringResolver.TryReadConnectionStringFromApiSettings(repoRoot);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("No storage connection string found. Use --connection-string or set AZURE_STORAGE_CONNECTION_STRING.");
                return 1;
            }

            var moduleMap = CreateModules();
            var selectedModules = ParseRequestedModules(options.Modules, moduleMap.Keys);
            var guidFormat = ConnectionStringResolver.ResolveGuidFormat(repoRoot);
            var seed = options.Seed ?? ComputeDefaultSeed(options.TenantId.Value);

            Console.WriteLine("Demo Data Seed");
            Console.WriteLine($"Tenant: {options.TenantId:D}");
            Console.WriteLine($"Guid Key Format: {guidFormat}");
            Console.WriteLine($"Seed: {seed}");
            Console.WriteLine($"Patients Target: {options.DemoPatientCount}");
            Console.WriteLine($"Modules: {string.Join(", ", OrderedModules.Where(selectedModules.Contains))}");
            Console.WriteLine(options.WhatIf ? "Mode: WHAT-IF (no writes)" : "Mode: APPLY");

            var context = new DemoSeedContext(
                options.TenantId.Value,
                options.DemoPatientCount,
                guidFormat,
                options.WhatIf,
                seed,
                new TableServiceClient(connectionString));

            if (!selectedModules.Contains("patients"))
            {
                await context.LoadExistingPatientsAsync(CancellationToken.None);
                if (context.Patients.Count == 0)
                {
                    Console.Error.WriteLine("No existing patients were found for this tenant. Include the 'patients' module or seed patients first.");
                    return 1;
                }
            }

            foreach (var moduleName in OrderedModules)
            {
                if (!selectedModules.Contains(moduleName))
                    continue;

                await moduleMap[moduleName].SeedAsync(context, CancellationToken.None);
            }

            Console.WriteLine();
            Console.WriteLine("Module Summary:");
            foreach (var moduleName in OrderedModules)
            {
                if (context.ModuleCounts.TryGetValue(moduleName, out var count))
                    Console.WriteLine($"  {moduleName}: {count} upserts");
            }

            Console.WriteLine("Table Summary:");
            foreach (var table in context.TableCounts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  {table.Key}: {table.Value} upserts");

            Console.WriteLine("Completed demo data seed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Demo seed failed: {ex.Message}");
            return 1;
        }
    }

    private static Dictionary<string, IDemoSeedModule> CreateModules()
    {
        return new Dictionary<string, IDemoSeedModule>(StringComparer.OrdinalIgnoreCase)
        {
            ["patients"] = new PatientsSeedModule(),
            ["contacts"] = new ContactsSeedModule(),
            ["allergies"] = new AllergiesSeedModule(),
            ["family-history"] = new FamilyHistorySeedModule(),
            ["vitals"] = new VitalsSeedModule(),
            ["marital-history"] = new MaritalHistorySeedModule(),
            ["military-history"] = new MilitaryHistorySeedModule(),
            ["environmental-history"] = new EnvironmentalHistorySeedModule()
        };
    }

    private static HashSet<string> ParseRequestedModules(string rawModules, IEnumerable<string> availableModules)
    {
        var available = new HashSet<string>(availableModules, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawModules) || string.Equals(rawModules.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            return new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);

        var requested = rawModules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = requested.Where(x => !available.Contains(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException($"Unknown module(s): {string.Join(", ", unknown)}");

        return requested;
    }

    private static int ComputeDefaultSeed(Guid tenantId)
    {
        var bytes = tenantId.ToByteArray();
        var value = BitConverter.ToInt32(bytes, 0);
        return value == int.MinValue ? 1 : Math.Abs(value);
    }
}

internal interface IDemoSeedModule
{
    string Name { get; }
    Task SeedAsync(DemoSeedContext context, CancellationToken ct);
}

internal sealed class DemoSeedContext
{
    private readonly TableServiceClient _serviceClient;
    private readonly Dictionary<string, TableClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public DemoSeedContext(
        Guid tenantId,
        int targetPatientCount,
        string guidKeyFormat,
        bool whatIf,
        int seed,
        TableServiceClient serviceClient)
    {
        TenantId = tenantId;
        TargetPatientCount = targetPatientCount;
        GuidKeyFormat = string.Equals(guidKeyFormat, "N", StringComparison.OrdinalIgnoreCase) ? "N" : "D";
        WhatIf = whatIf;
        Seed = seed;
        Random = new Random(seed);
        _serviceClient = serviceClient;
    }

    public Guid TenantId { get; }
    public int TargetPatientCount { get; }
    public string GuidKeyFormat { get; }
    public bool WhatIf { get; }
    public int Seed { get; }
    public Random Random { get; }
    public List<DemoPatient> Patients { get; } = new();
    public Dictionary<string, int> TableCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ModuleCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string TenantPartitionKey => FormatGuid(TenantId);

    public string RowKey(Guid id) => FormatGuid(id);

    public string TenantPatientPartitionKey(Guid patientId)
        => $"TENANT={FormatGuid(TenantId)}|PATIENT={FormatGuid(patientId)}";

    public Guid DeterministicGuid(string scope)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes($"{TenantId:D}|{scope}"));
        return new Guid(bytes);
    }

    public void AddOrUpdatePatient(DemoPatient patient)
    {
        var existingIndex = Patients.FindIndex(x => x.Id == patient.Id);
        if (existingIndex >= 0)
            Patients[existingIndex] = patient;
        else
            Patients.Add(patient);
    }

    public async Task UpsertAsync(string moduleName, string tableName, TableEntity entity, CancellationToken ct)
    {
        TableCounts[tableName] = TableCounts.TryGetValue(tableName, out var tableCount) ? tableCount + 1 : 1;
        ModuleCounts[moduleName] = ModuleCounts.TryGetValue(moduleName, out var moduleCount) ? moduleCount + 1 : 1;

        if (WhatIf)
            return;

        var client = GetTableClient(tableName);
        await client.CreateIfNotExistsAsync(ct);
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task LoadExistingPatientsAsync(CancellationToken ct)
    {
        Patients.Clear();
        var client = GetTableClient("Patients");
        var safePartition = TenantPartitionKey.Replace("'", "''", StringComparison.Ordinal);
        var filter = $"PartitionKey eq '{safePartition}'";

        var index = 0;
        await foreach (var row in client.QueryAsync<TableEntity>(filter: filter, maxPerPage: 200, cancellationToken: ct))
        {
            if (Patients.Count >= TargetPatientCount)
                break;

            if (!Guid.TryParse(row.RowKey, out var patientId))
                continue;

            var firstName = row.TryGetValue("FirstName", out var firstNameObj)
                ? firstNameObj?.ToString() ?? "Demo"
                : "Demo";
            var lastName = row.TryGetValue("LastName", out var lastNameObj)
                ? lastNameObj?.ToString() ?? "Patient"
                : "Patient";

            var dob = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddYears(-40), DateTimeKind.Utc);
            var gender = "Unknown";

            if (row.TryGetValue("Data", out var dataObj) && dataObj is string dataJson && !string.IsNullOrWhiteSpace(dataJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(dataJson);
                    if (doc.RootElement.TryGetProperty("dateOfBirth", out var dobElement) &&
                        DateTime.TryParse(dobElement.GetString(), out var parsedDob))
                    {
                        dob = DateTime.SpecifyKind(parsedDob, DateTimeKind.Utc);
                    }

                    if (doc.RootElement.TryGetProperty("gender", out var genderElement))
                        gender = genderElement.GetString() ?? gender;
                }
                catch
                {
                    // Ignore malformed envelope and continue with defaults.
                }
            }

            Patients.Add(new DemoPatient(patientId, firstName, lastName, dob, gender, index++));
        }
    }

    private string FormatGuid(Guid value) => value.ToString(GuidKeyFormat);

    private TableClient GetTableClient(string tableName)
    {
        if (_clients.TryGetValue(tableName, out var client))
            return client;

        client = _serviceClient.GetTableClient(tableName);
        _clients[tableName] = client;
        return client;
    }
}

internal sealed record DemoPatient(
    Guid Id,
    string FirstName,
    string LastName,
    DateTime DateOfBirthUtc,
    string Gender,
    int Index);

internal sealed class PatientsSeedModule : IDemoSeedModule
{
    private static readonly string[] FirstNames =
    [
        "Emma", "Liam", "Olivia", "Noah", "Ava", "Ethan", "Sophia", "Mason", "Isabella", "Lucas",
        "Mia", "Logan", "Charlotte", "Elijah", "Amelia", "James", "Harper", "Benjamin", "Evelyn", "Henry"
    ];

    private static readonly string[] LastNames =
    [
        "Carter", "Nguyen", "Patel", "Rivera", "Thompson", "Reed", "Howard", "Bailey", "Cooper", "Powell",
        "Diaz", "Stewart", "Morris", "Murphy", "Bell", "Flores", "Butler", "Hughes", "Simmons", "Foster"
    ];

    public string Name => "patients";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        for (var i = 0; i < context.TargetPatientCount; i++)
        {
            var id = context.DeterministicGuid($"patient:{i}");
            var firstName = FirstNames[i % FirstNames.Length];
            var lastName = LastNames[(i * 7) % LastNames.Length];
            var gender = i % 2 == 0 ? "Female" : "Male";
            var dob = DateTime.SpecifyKind(new DateTime(1942, 1, 1).AddDays((i * 1337) % 22000), DateTimeKind.Utc);
            var created = now.AddDays(-60 + i);

            var rowKey = context.RowKey(id);
            var partitionKey = context.TenantPartitionKey;

            var envelope = new
            {
                partitionKey,
                rowKey,
                id,
                firstName,
                lastName,
                dateOfBirth = dob,
                gender,
                patientStatus = "Active",
                currentFacilityId = (Guid?)null,
                currentFacilityName = (string?)null,
                currentFacilityAdmitDate = (DateTime?)null,
                currentFacilityStatus = (string?)null,
                dateCreated = created,
                dateUpdated = created,
                isDeleted = false,
                eTag = (string?)null,
                timestamp = (DateTimeOffset?)null
            };

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["Type"] = "MedInsights.Lib.Entities.Patient",
                ["Data"] = JsonSerializer.Serialize(
                    envelope,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }),
                ["FirstName"] = firstName,
                ["LastName"] = lastName,
                ["PatientStatus"] = "Active"
            };

            await context.UpsertAsync(Name, "Patients", entity, ct);
            context.AddOrUpdatePatient(new DemoPatient(id, firstName, lastName, dob, gender, i));
        }
    }
}

internal sealed class ContactsSeedModule : IDemoSeedModule
{
    public string Name => "contacts";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            var selfId = context.DeterministicGuid($"contact:{patient.Id:D}:self");
            var selfEntity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(selfId))
            {
                ["Type"] = "MedInsights.Lib.Entities.PatientContact",
                ["Id"] = selfId,
                ["PatientId"] = patient.Id,
                ["ContactType"] = "Self",
                ["Relationship"] = 1,
                ["IsPrimary"] = true,
                ["IsSecondary"] = false,
                ["FirstName"] = patient.FirstName,
                ["LastName"] = patient.LastName,
                ["PrimaryPhone"] = BuildPhone(patient.Index, 0),
                ["Email"] = $"{patient.FirstName.ToLowerInvariant()}.{patient.LastName.ToLowerInvariant()}{patient.Index}@demo.local",
                ["HasHIPAAPermission"] = true,
                ["HasBillingPermission"] = true,
                ["HasDurablePowerOfAttorney"] = false,
                ["HasMedicalPowerOfAttorney"] = false,
                ["HasFinancialPowerOfAttorney"] = false,
                ["IsDeleted"] = false
            };

            await context.UpsertAsync(Name, "PatientContacts", selfEntity, ct);

            var emergencyId = context.DeterministicGuid($"contact:{patient.Id:D}:emergency");
            var emergencyEntity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(emergencyId))
            {
                ["Type"] = "MedInsights.Lib.Entities.PatientContact",
                ["Id"] = emergencyId,
                ["PatientId"] = patient.Id,
                ["ContactType"] = "Emergency",
                ["Relationship"] = patient.Index % 2 == 0 ? 2 : 3,
                ["IsPrimary"] = false,
                ["IsSecondary"] = true,
                ["FirstName"] = patient.Index % 2 == 0 ? "Alex" : "Jordan",
                ["LastName"] = patient.LastName,
                ["PrimaryPhone"] = BuildPhone(patient.Index, 1),
                ["HasHIPAAPermission"] = true,
                ["HasBillingPermission"] = true,
                ["HasDurablePowerOfAttorney"] = false,
                ["HasMedicalPowerOfAttorney"] = false,
                ["HasFinancialPowerOfAttorney"] = false,
                ["IsDeleted"] = false
            };

            await context.UpsertAsync(Name, "PatientContacts", emergencyEntity, ct);
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Contacts module requires patients in context.");
    }

    private static string BuildPhone(int index, int offset)
    {
        var prefix = 200 + ((index + offset) % 700);
        var line = 1000 + ((index * 101 + offset * 211) % 9000);
        return $"614-{prefix:D3}-{line:D4}";
    }
}

internal sealed class AllergiesSeedModule : IDemoSeedModule
{
    private static readonly (string Type, string Severity, string Description, string Reaction)[] Templates =
    [
        ("Medication", "Moderate", "Penicillin", "Rash"),
        ("Food", "Severe", "Shellfish", "Anaphylaxis"),
        ("Environmental", "Mild", "Pollen", "Sneezing")
    ];

    public string Name => "allergies";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            var count = 1 + (patient.Index % 2);
            for (var i = 0; i < count; i++)
            {
                var template = Templates[(patient.Index + i) % Templates.Length];
                var id = context.DeterministicGuid($"allergy:{patient.Id:D}:{i}");

                var entity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(id))
                {
                    ["Type"] = "MedInsights.Lib.Entities.PatientAllergy",
                    ["Id"] = id,
                    ["PatientId"] = patient.Id,
                    ["AllergyType"] = template.Type,
                    ["Severity"] = template.Severity,
                    ["Description"] = template.Description,
                    ["Reaction"] = template.Reaction,
                    ["DateNoted"] = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(patient.Index * 11 + i * 4)), DateTimeKind.Utc),
                    ["IsDeleted"] = false
                };

                await context.UpsertAsync(Name, "PatientAllergies", entity, ct);
            }
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Allergies module requires patients in context.");
    }
}

internal sealed class FamilyHistorySeedModule : IDemoSeedModule
{
    private static readonly (string Description, string Relationship)[] Templates =
    [
        ("Hypertension", "Father"),
        ("Type 2 Diabetes", "Mother"),
        ("Coronary artery disease", "Grandfather")
    ];

    public string Name => "family-history";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            for (var i = 0; i < 2; i++)
            {
                var template = Templates[(patient.Index + i) % Templates.Length];
                var id = context.DeterministicGuid($"family-history:{patient.Id:D}:{i}");

                var entity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(id))
                {
                    ["Type"] = "MedInsights.Lib.Entities.PatientFamilyMedicalHistory",
                    ["Id"] = id,
                    ["PatientId"] = patient.Id,
                    ["DateNoted"] = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(patient.Index * 15 + i * 8)), DateTimeKind.Utc),
                    ["Description"] = template.Description,
                    ["Relationship"] = template.Relationship,
                    ["IsDeleted"] = false
                };

                await context.UpsertAsync(Name, "PatientFamilyMedicalHistories", entity, ct);
            }
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Family history module requires patients in context.");
    }
}

internal sealed class VitalsSeedModule : IDemoSeedModule
{
    public string Name => "vitals";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            var id = context.DeterministicGuid($"vitals:{patient.Id:D}:0");
            var dateRead = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-patient.Index), DateTimeKind.Utc);
            var heightInches = 62 + (patient.Index % 14);
            var weightPounds = 125 + ((patient.Index * 9) % 120);
            var bmi = Math.Round((weightPounds / (heightInches * heightInches)) * 703.0, 2);
            var tempF = Math.Round(98.0 + ((patient.Index % 7) * 0.2), 1);
            var tempC = Math.Round((tempF - 32.0) * 5.0 / 9.0, 2);

            var entity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(id))
            {
                ["Type"] = "MedInsights.Lib.Entities.PatientVitals",
                ["Id"] = id,
                ["PatientId"] = patient.Id,
                ["TemperatureFahrenheit"] = tempF,
                ["TemperatureCelsius"] = tempC,
                ["TmaxFahrenheit"] = tempF + 0.4,
                ["TmaxCelsius"] = Math.Round(((tempF + 0.4) - 32.0) * 5.0 / 9.0, 2),
                ["Temperature"] = tempF,
                ["Tmax"] = tempF + 0.4,
                ["SystolicBloodPressure"] = 110 + (patient.Index % 20),
                ["DiastolicBloodPressure"] = 70 + (patient.Index % 12),
                ["RespitoryRate"] = 14 + (patient.Index % 6),
                ["HeartRate"] = 60 + (patient.Index % 25),
                ["HeartRateQuality"] = "Regular",
                ["PulseOximetry"] = "98",
                ["HeightInches"] = heightInches,
                ["HeightCentimeters"] = Math.Round(heightInches * 2.54, 2),
                ["Height"] = heightInches,
                ["WeightPounds"] = weightPounds,
                ["WeightKilograms"] = Math.Round(weightPounds * 0.45359237, 2),
                ["Weight"] = weightPounds,
                ["BMI"] = bmi,
                ["DateRead"] = dateRead,
                ["IsDeleted"] = false
            };

            await context.UpsertAsync(Name, "PatientVitals", entity, ct);
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Vitals module requires patients in context.");
    }
}

internal sealed class MaritalHistorySeedModule : IDemoSeedModule
{
    private static readonly string[] Statuses = ["Single", "Married", "Divorced", "Widowed"];

    public string Name => "marital-history";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            var id = context.DeterministicGuid($"marital-history:{patient.Id:D}");
            var status = Statuses[patient.Index % Statuses.Length];
            var childrenCount = status == "Married" ? (patient.Index % 4) : 0;
            var hasChildren = childrenCount > 0 ? "Yes" : "No";

            var entity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(id))
            {
                ["Type"] = "MedInsights.Lib.Entities.PatientMaritalHistory",
                ["Id"] = id,
                ["PatientId"] = patient.Id,
                ["DateNoted"] = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-patient.Index), DateTimeKind.Utc),
                ["MaritalStatus"] = status,
                ["SpouseName"] = status == "Married" ? $"Taylor {patient.LastName}" : string.Empty,
                ["HasChildren"] = hasChildren,
                ["NumberOfChildren"] = childrenCount,
                ["Notes"] = "Demo seeded marital history",
                ["IsDeleted"] = false
            };

            await context.UpsertAsync(Name, "PatientMaritalHistories", entity, ct);
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Marital history module requires patients in context.");
    }
}

internal sealed class MilitaryHistorySeedModule : IDemoSeedModule
{
    public string Name => "military-history";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            var id = context.DeterministicGuid($"military-history:{patient.Id:D}");
            var militaryVeteran = patient.Index % 3 == 0 ? "Yes" : "No";
            var activeMilitary = patient.Index % 5 == 0 ? "Yes" : "Unknown";
            var firstResponder = patient.Index % 4 == 0 ? "Yes" : "Unknown";
            var lawEnforcement = patient.Index % 6 == 0 ? "Yes" : "Unknown";

            var entity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(id))
            {
                ["Type"] = "MedInsights.Lib.Entities.PatientMilitaryFirstResponder",
                ["Id"] = id,
                ["PatientId"] = patient.Id,
                ["MilitaryVeteran"] = militaryVeteran,
                ["MilitaryVeteranBranch"] = militaryVeteran == "Yes" ? "Army" : string.Empty,
                ["ActiveMilitary"] = activeMilitary,
                ["ActiveMilitaryBranch"] = activeMilitary == "Yes" ? "Air Force" : string.Empty,
                ["MilitaryId"] = activeMilitary == "Yes" ? $"MIL-{patient.Index + 1000:D6}" : string.Empty,
                ["FirstResponder"] = firstResponder,
                ["FirstResponderType"] = firstResponder == "Yes" ? "EMS" : string.Empty,
                ["FirstResponderDepartment"] = firstResponder == "Yes" ? "Franklin County EMS" : string.Empty,
                ["FirstResponderStation"] = firstResponder == "Yes" ? "Station 4" : string.Empty,
                ["LawEnforcement"] = lawEnforcement,
                ["LawEnforcementType"] = lawEnforcement == "Yes" ? "Police" : string.Empty,
                ["LawEnforcementAgency"] = lawEnforcement == "Yes" ? "City Police" : string.Empty,
                ["LawEnforcementId"] = lawEnforcement == "Yes" ? $"LE-{patient.Index + 500:D6}" : string.Empty,
                ["IsDeleted"] = false
            };

            await context.UpsertAsync(Name, "PatientMilitaryFirstResponders", entity, ct);
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Military history module requires patients in context.");
    }
}

internal sealed class EnvironmentalHistorySeedModule : IDemoSeedModule
{
    private static readonly string[] Occupations =
    [
        "Teacher", "Construction", "Nurse", "Office Administration", "Warehouse", "Retail", "Driver", "Hospitality"
    ];

    public string Name => "environmental-history";

    public async Task SeedAsync(DemoSeedContext context, CancellationToken ct)
    {
        EnsurePatients(context);

        foreach (var patient in context.Patients)
        {
            var id = context.DeterministicGuid($"environmental-history:{patient.Id:D}");
            var occupationRisk = patient.Index % 3 == 0 ? "Yes" : "No";
            var exposureRisk = patient.Index % 4 == 0 ? "Yes" : "No";
            var recentTravel = patient.Index % 5 == 0 ? "Yes" : "No";

            var entity = new TableEntity(context.TenantPatientPartitionKey(patient.Id), context.RowKey(id))
            {
                ["Type"] = "MedInsights.Lib.Entities.PatientEnvironmentalHistory",
                ["Id"] = id,
                ["PatientId"] = patient.Id,
                ["DateNoted"] = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(patient.Index + 2)), DateTimeKind.Utc),
                ["Occupation"] = Occupations[patient.Index % Occupations.Length],
                ["OccupationRisk"] = occupationRisk,
                ["YearsInOccupation"] = 2 + (patient.Index % 28),
                ["ExposureRisk"] = exposureRisk,
                ["ExposureDetails"] = exposureRisk == "Yes" ? "Periodic chemical exposure at worksite" : string.Empty,
                ["RecentTravel"] = recentTravel,
                ["Destination"] = recentTravel == "Yes" ? "Mexico" : string.Empty,
                ["DaysAbroad"] = recentTravel == "Yes" ? 6 : 0,
                ["IsLocked"] = false,
                ["IsDeleted"] = false
            };

            await context.UpsertAsync(Name, "PatientEnvironmentalHistories", entity, ct);
        }
    }

    private static void EnsurePatients(DemoSeedContext context)
    {
        if (context.Patients.Count == 0)
            throw new InvalidOperationException("Environmental history module requires patients in context.");
    }
}
