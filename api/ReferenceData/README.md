# Reference Data Loader

Use the loader to import static datasets defined in `manifest.json`.

## Quick commands

Validate only (no writes):

```powershell
dotnet run --project Tools/ReferenceDataLoader -- --dataset icd10 --what-if
```

Import using `AZURE_STORAGE_CONNECTION_STRING` or `MedInsights.API/appsettings.json`:

```powershell
dotnet run --project Tools/ReferenceDataLoader -- --dataset icd10
```

Import with explicit connection string:

```powershell
dotnet run --project Tools/ReferenceDataLoader -- --dataset icd10 --connection-string "UseDevelopmentStorage=true"
```

PowerShell wrapper:

```powershell
.\Tools\ReferenceDataLoader\import-reference-data.ps1 -Dataset icd10 -WhatIf
```

## Updating data

1. Add a new dated file (example: `icd10-2026-06-30.csv`).
2. Update `manifest.json`:
   - `activeFile`
   - `validation.checksumSha256`
   - `validation.rowCount`
3. Run loader in `--what-if`, then run import.
