# ReferenceDataLoader

`ReferenceDataLoader` supports two modes:

1. `reference` mode imports CSV datasets from `ReferenceData/manifest.json`.
2. `demo` mode seeds modular demo patient data for a tenant.

## Demo Seed Quick Start

```powershell
./Tools/ReferenceDataLoader/seed-demo-data.ps1 -TenantId 44b54b3b-83f6-41f3-9e28-a6d8afc60e9e
```

Or run directly:

```powershell
dotnet run --project Tools/ReferenceDataLoader -- --mode demo --tenant-id <tenant-guid> --patients 10
```

### Demo Options

- `--tenant-id <guid>`: required tenant to seed.
- `--patients <n>`: number of patients to seed (default `10`).
- `--modules <list>`: comma-separated modules or `all`.
- `--seed <n>`: optional deterministic seed.
- `--what-if`: simulate only, no writes.
- `--connection-string <value>`: override storage connection string.

### Demo Modules

- `patients`
- `contacts`
- `allergies`
- `family-history`
- `vitals`
- `marital-history`
- `military-history`
- `environmental-history`

Example partial seed:

```powershell
dotnet run --project Tools/ReferenceDataLoader -- --mode demo --tenant-id <tenant-guid> --modules patients,contacts,allergies
```

## Reference Import Mode

```powershell
./Tools/ReferenceDataLoader/import-reference-data.ps1 -Dataset icd10
```
