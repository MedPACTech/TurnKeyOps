using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.VisualBasic.FileIO;

return await LoaderApp.RunAsync(args);

internal static class LoaderApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = LoaderOptions.Parse(args);
        if (options.ShowHelp)
        {
            LoaderOptions.PrintHelp();
            return 0;
        }

        if (string.Equals(options.Mode, "demo", StringComparison.OrdinalIgnoreCase))
            return await DemoDataRunner.RunAsync(options);

        return await RunReferenceImportAsync(options);
    }

    private static async Task<int> RunReferenceImportAsync(LoaderOptions options)
    {
        var manifestPath = Path.GetFullPath(options.ManifestPath);
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Manifest not found: {manifestPath}");
            return 1;
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<ReferenceDataManifest>(
            manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest?.Datasets is null || manifest.Datasets.Count == 0)
        {
            Console.Error.WriteLine("No datasets found in manifest.");
            return 1;
        }

        var dataset = manifest.Datasets.FirstOrDefault(d =>
            string.Equals(d.Name, options.DatasetName, StringComparison.OrdinalIgnoreCase));

        if (dataset is null)
        {
            Console.Error.WriteLine($"Dataset '{options.DatasetName}' was not found in manifest.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(dataset.ActiveFile))
        {
            Console.Error.WriteLine($"Dataset '{dataset.Name}' does not define activeFile.");
            return 1;
        }

        var referenceDataDirectory = Path.GetDirectoryName(manifestPath) ?? Directory.GetCurrentDirectory();
        var sourceFilePath = Path.GetFullPath(Path.Combine(referenceDataDirectory, dataset.ActiveFile));

        if (!File.Exists(sourceFilePath))
        {
            Console.Error.WriteLine($"Dataset file not found: {sourceFilePath}");
            return 1;
        }

        if (!options.SkipValidation)
        {
            Console.WriteLine("Validating source file checksum and row count...");

            if (!string.IsNullOrWhiteSpace(dataset.Validation?.ChecksumSha256))
            {
                var checksum = ComputeSha256(sourceFilePath);
                if (!string.Equals(checksum, dataset.Validation.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"Checksum mismatch. Expected {dataset.Validation.ChecksumSha256}, got {checksum}.");
                    return 1;
                }
            }

            if (dataset.Validation?.RowCount is > -1)
            {
                var actualRowCount = CountDataRows(sourceFilePath);
                if (actualRowCount != dataset.Validation.RowCount)
                {
                    Console.Error.WriteLine($"Row count mismatch. Expected {dataset.Validation.RowCount}, got {actualRowCount}.");
                    return 1;
                }
            }
        }

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

        var tableName = options.TableNameOverride
            ?? dataset.Target?.TableName
            ?? "ICD10Records";

        var partitionKeyColumn = dataset.Columns?.PartitionKey ?? "PartitionKey";
        var rowKeyColumn = dataset.Columns?.RowKey ?? "RowKey";

        Console.WriteLine($"Dataset: {dataset.Name}");
        Console.WriteLine($"File: {sourceFilePath}");
        Console.WriteLine($"Table: {tableName}");
        Console.WriteLine(options.WhatIf ? "Mode: WHAT-IF (no writes)" : "Mode: IMPORT");

        var client = new TableClient(connectionString, tableName);
        if (!options.WhatIf)
            await client.CreateIfNotExistsAsync();

        var imported = await ImportDatasetAsync(
            client,
            sourceFilePath,
            partitionKeyColumn,
            rowKeyColumn,
            options.WhatIf,
            options.MaxRows);

        Console.WriteLine($"Completed. Rows processed: {imported}");
        return 0;
    }

    private static async Task<int> ImportDatasetAsync(
        TableClient client,
        string csvPath,
        string partitionKeyColumn,
        string rowKeyColumn,
        bool whatIf,
        int? maxRows)
    {
        using var parser = CreateCsvParser(csvPath);
        var headers = parser.ReadFields() ?? Array.Empty<string>();
        if (headers.Length == 0)
            throw new InvalidOperationException("CSV has no header row.");

        var columnIndex = headers
            .Select((name, idx) => (name, idx))
            .ToDictionary(x => x.name, x => x.idx, StringComparer.OrdinalIgnoreCase);

        if (!columnIndex.ContainsKey(partitionKeyColumn) || !columnIndex.ContainsKey(rowKeyColumn))
            throw new InvalidOperationException($"CSV missing required key columns: {partitionKeyColumn}, {rowKeyColumn}");

        var typeColumnIndex = headers
            .Where(h => h.EndsWith("@type", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                h => h[..^5],
                h => columnIndex[h],
                StringComparer.OrdinalIgnoreCase);

        var processed = 0;
        var pendingActions = new List<TableTransactionAction>(capacity: 100);
        string? currentPartitionKey = null;

        while (!parser.EndOfData)
        {
            var values = parser.ReadFields();
            if (values is null || values.Length == 0)
                continue;

            var partitionKey = GetValue(values, columnIndex[partitionKeyColumn]);
            var rowKey = GetValue(values, columnIndex[rowKeyColumn]);
            if (string.IsNullOrWhiteSpace(partitionKey) || string.IsNullOrWhiteSpace(rowKey))
                continue;

            if (currentPartitionKey is not null &&
                !string.Equals(currentPartitionKey, partitionKey, StringComparison.Ordinal) &&
                pendingActions.Count > 0)
            {
                if (!whatIf)
                    await client.SubmitTransactionAsync(pendingActions);
                pendingActions.Clear();
            }

            currentPartitionKey = partitionKey;

            var entity = new TableEntity(partitionKey, rowKey);
            for (var i = 0; i < headers.Length; i++)
            {
                var columnName = headers[i];
                if (columnName.EndsWith("@type", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(columnName, partitionKeyColumn, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columnName, rowKeyColumn, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rawValue = GetValue(values, i);
                if (string.IsNullOrWhiteSpace(rawValue))
                    continue;

                typeColumnIndex.TryGetValue(columnName, out var typeIdx);
                var rawType = typeIdx >= 0 ? GetValue(values, typeIdx) : null;
                entity[columnName] = ConvertByType(rawValue, rawType);
            }

            pendingActions.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity, ETag.All));
            processed++;

            if (pendingActions.Count == 100)
            {
                if (!whatIf)
                    await client.SubmitTransactionAsync(pendingActions);
                pendingActions.Clear();
            }

            if (processed % 5000 == 0)
                Console.WriteLine($"Processed {processed:N0} rows...");

            if (maxRows.HasValue && processed >= maxRows.Value)
                break;
        }

        if (pendingActions.Count > 0)
        {
            if (!whatIf)
                await client.SubmitTransactionAsync(pendingActions);
            pendingActions.Clear();
        }

        return processed;
    }

    private static TextFieldParser CreateCsvParser(string path)
    {
        var parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        return parser;
    }

    private static string? GetValue(string[] row, int index)
    {
        if (index < 0 || index >= row.Length)
            return null;

        return row[index];
    }

    private static object ConvertByType(string value, string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return value;

        return typeName.Trim().ToLowerInvariant() switch
        {
            "boolean" when bool.TryParse(value, out var b) => b,
            "int16" when short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
            "int32" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i32) => i32,
            "int64" when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i64) => i64,
            "double" when double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) => d,
            "datetime" when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto) => dto,
            _ => value
        };
    }

    private static int CountDataRows(string csvPath)
    {
        using var parser = CreateCsvParser(csvPath);
        _ = parser.ReadFields();
        var count = 0;
        while (!parser.EndOfData)
        {
            _ = parser.ReadFields();
            count++;
        }

        return count;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}

internal sealed record ReferenceDataManifest(string ManifestVersion, List<ReferenceDataset> Datasets);

internal sealed record ReferenceDataset(
    string Name,
    string Description,
    string ActiveFile,
    string FileNamePattern,
    string Format,
    string UpdateCadence,
    ReferenceTarget? Target,
    ReferenceColumns? Columns,
    ReferenceValidation? Validation);

internal sealed record ReferenceTarget(string StorageType, string TableName, bool IsTenantScoped);

internal sealed record ReferenceColumns(
    string PartitionKey,
    string RowKey,
    string Code,
    string ShortDescription,
    string LongDescription);

internal sealed record ReferenceValidation(string ChecksumSha256, int RowCount);

internal sealed class LoaderOptions
{
    public string Mode { get; init; } = "reference";
    public string DatasetName { get; init; } = "icd10";
    public string ManifestPath { get; init; } = Path.Combine("ReferenceData", "manifest.json");
    public string? ConnectionString { get; init; }
    public string? TableNameOverride { get; init; }
    public bool SkipValidation { get; init; }
    public bool WhatIf { get; init; }
    public bool ShowHelp { get; init; }
    public int? MaxRows { get; init; }

    public Guid? TenantId { get; init; }
    public int DemoPatientCount { get; init; } = 10;
    public int? Seed { get; init; }
    public string Modules { get; init; } = "all";

    public static LoaderOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = arg[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                map[key] = args[++i];
            }
            else
            {
                map[key] = "true";
            }
        }

        Guid? tenantId = null;
        if (map.TryGetValue("tenant-id", out var tenantIdRaw) && Guid.TryParse(tenantIdRaw, out var parsedTenantId))
            tenantId = parsedTenantId;

        return new LoaderOptions
        {
            Mode = map.TryGetValue("mode", out var mode) && !string.IsNullOrWhiteSpace(mode) ? mode.Trim() : "reference",
            DatasetName = map.TryGetValue("dataset", out var dataset) && !string.IsNullOrWhiteSpace(dataset) ? dataset : "icd10",
            ManifestPath = map.TryGetValue("manifest", out var manifest) && !string.IsNullOrWhiteSpace(manifest) ? manifest : Path.Combine("ReferenceData", "manifest.json"),
            ConnectionString = map.TryGetValue("connection-string", out var conn) ? conn : null,
            TableNameOverride = map.TryGetValue("table", out var table) ? table : null,
            SkipValidation = map.ContainsKey("skip-validation"),
            WhatIf = map.ContainsKey("what-if"),
            ShowHelp = map.ContainsKey("help") || map.ContainsKey("h"),
            MaxRows = map.TryGetValue("max-rows", out var maxRows) && int.TryParse(maxRows, out var parsedMaxRows) ? parsedMaxRows : null,
            TenantId = tenantId,
            DemoPatientCount = map.TryGetValue("patients", out var patientsRaw) && int.TryParse(patientsRaw, out var parsedPatients) && parsedPatients > 0 ? parsedPatients : 10,
            Seed = map.TryGetValue("seed", out var seedRaw) && int.TryParse(seedRaw, out var parsedSeed) ? parsedSeed : null,
            Modules = map.TryGetValue("modules", out var modulesRaw) && !string.IsNullOrWhiteSpace(modulesRaw) ? modulesRaw : "all"
        };
    }

    public static void PrintHelp()
    {
        Console.WriteLine("ReferenceDataLoader");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Tools/ReferenceDataLoader -- --mode reference [options]");
        Console.WriteLine("  dotnet run --project Tools/ReferenceDataLoader -- --mode demo --tenant-id <guid> [options]");
        Console.WriteLine();
        Console.WriteLine("Reference Mode Options:");
        Console.WriteLine("  --dataset <name>             Dataset name in manifest (default: icd10)");
        Console.WriteLine("  --manifest <path>            Manifest path (default: ReferenceData/manifest.json)");
        Console.WriteLine("  --connection-string <value>  Azure Storage connection string");
        Console.WriteLine("  --table <name>               Override target table name");
        Console.WriteLine("  --skip-validation            Skip checksum and row-count validation");
        Console.WriteLine("  --max-rows <n>               Limit rows processed (useful for tests)");
        Console.WriteLine();
        Console.WriteLine("Demo Mode Options:");
        Console.WriteLine("  --tenant-id <guid>           Tenant to seed (required in demo mode)");
        Console.WriteLine("  --patients <n>               Number of demo patients to seed (default: 10)");
        Console.WriteLine("  --modules <list>             Comma-separated modules or 'all' (default: all)");
        Console.WriteLine("                               Available: patients,contacts,allergies,family-history,vitals,");
        Console.WriteLine("                                          marital-history,military-history,environmental-history");
        Console.WriteLine("  --seed <n>                   Optional deterministic random seed");
        Console.WriteLine();
        Console.WriteLine("Common Options:");
        Console.WriteLine("  --mode <reference|demo>      Loader mode (default: reference)");
        Console.WriteLine("  --connection-string <value>  Azure Storage connection string");
        Console.WriteLine("  --what-if                    Parse/generate only; no writes");
        Console.WriteLine("  --help                       Show this help");
    }
}

internal static class ConnectionStringResolver
{
    public static string? FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TurnKeyOps.API.sln")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    public static string? TryReadConnectionStringFromApiSettings(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            return null;

        var appSettingsPath = Path.Combine(repoRoot, "TurnKeyOps.API", "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            try
            {
                using var stream = File.OpenRead(appSettingsPath);
                using var doc = JsonDocument.Parse(stream);

                if (doc.RootElement.TryGetProperty("IBeam", out var ibeam) &&
                    ibeam.TryGetProperty("Repositories", out var repositories) &&
                    repositories.TryGetProperty("AzureTables", out var azureTables) &&
                    azureTables.TryGetProperty("ConnectionString", out var repoConnection) &&
                    !string.IsNullOrWhiteSpace(repoConnection.GetString()))
                {
                    return repoConnection.GetString();
                }

                if (doc.RootElement.TryGetProperty("AzureStorageSettings", out var azureStorage) &&
                    azureStorage.TryGetProperty("ConnectionString", out var connection) &&
                    !string.IsNullOrWhiteSpace(connection.GetString()))
                {
                    return connection.GetString();
                }
            }
            catch
            {
                // Ignore and continue to local settings fallback.
            }
        }

        var localSettingsPath = Path.Combine(repoRoot, ".local", "appservice-dev-settings.json");
        if (!File.Exists(localSettingsPath))
            return null;

        try
        {
            using var stream = File.OpenRead(localSettingsPath);
            using var doc = JsonDocument.Parse(stream);

            if (doc.RootElement.TryGetProperty("IBeam__Repositories__AzureTables__ConnectionString", out var repoConnection) &&
                !string.IsNullOrWhiteSpace(repoConnection.GetString()))
            {
                return repoConnection.GetString();
            }

            if (doc.RootElement.TryGetProperty("AzureStorageSettings__ConnectionString", out var connection) &&
                !string.IsNullOrWhiteSpace(connection.GetString()))
            {
                return connection.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static string ResolveGuidFormat(string? repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            return "D";

        var appSettingsPath = Path.Combine(repoRoot, "TurnKeyOps.API", "appsettings.json");
        if (!File.Exists(appSettingsPath))
            return "D";

        try
        {
            using var stream = File.OpenRead(appSettingsPath);
            using var doc = JsonDocument.Parse(stream);

            if (doc.RootElement.TryGetProperty("IBeam", out var ibeam) &&
                ibeam.TryGetProperty("Repositories", out var repositories) &&
                repositories.TryGetProperty("AzureTables", out var azureTables) &&
                azureTables.TryGetProperty("GuidKeyFormat", out var guidFormatValue))
            {
                var format = guidFormatValue.GetString();
                if (string.Equals(format, "N", StringComparison.OrdinalIgnoreCase))
                    return "N";
            }
        }
        catch
        {
            return "D";
        }

        return "D";
    }
}
