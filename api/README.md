# TurnKeyOps API

Primary onboarding guide:
- [docs/developer-onboarding.md](docs/developer-onboarding.md)

## Environment model
- `appsettings.json` is the baseline shared config.
- `appsettings.Local.json` is for local developer defaults (non-secret).
- `appsettings.Development.json` is for deployed dev environment deltas.
- `appsettings.Test.json` is for deployed test environment deltas.
- `appsettings.Production.json` is for deployed production environment deltas.

Secrets are not stored in source control. Use `dotnet user-secrets` locally and Azure App Service settings in deployed environments.

## Prerequisites
- .NET SDK 10.x
- PowerShell 7+ (or Windows PowerShell)
- Access to required service credentials (OpenAI, Azure Storage/IBeam, Stripe, PayPal, Speech, Service Bus)

## Local setup
1. Create your local secrets file from the example:
```powershell
Copy-Item .\.local\user-secrets.example.json .\.local\user-secrets.json
```
2. Fill `.local/user-secrets.json` with your real values.
3. Optional: apply these values into the .NET user-secrets store too:
```powershell
.\scripts\setup-local-secrets.ps1
```
4. Verify user-secrets store values (if step 3 was used):
```powershell
dotnet user-secrets list --project .\TurnKeyOps.API\TurnKeyOps.API.csproj
```

In `Local`/`Development`, the API now reads both sources:
- `dotnet user-secrets` store
- `.local/user-secrets.json` (loaded last, so it overrides duplicate keys)

## Start the API locally
Launch profile is configured to run as `ASPNETCORE_ENVIRONMENT=Local`.

```powershell
dotnet run --project .\TurnKeyOps.API\TurnKeyOps.API.csproj --launch-profile http
```

Swagger: `http://localhost:5178/swagger`

## Basic developer workflow
```powershell
dotnet restore .\TurnKeyOps.API.sln
dotnet build .\TurnKeyOps.API.sln
dotnet test .\TurnKeyOps.API.sln
```

## Deployment configuration notes
- Development API URL: `https://dev-medinsights-api.azurewebsites.net`
- Test API URL: `https://test-medinsights-api.azurewebsites.net`
- Production API URL: `https://prod-medinsights-api.azurewebsites.net`
- Storage account naming convention by env:
  - Dev: `devmedinsights`
  - Test: `testmedinsights`
  - Prod: `prodmedinsights`

Do not commit real keys to `appsettings.*.json`.
Use Azure App Service Configuration for deployed secrets.

## Azure dev App Service settings
Configure the deployed dev API in Azure App Service (`dev-medinsights-api`) with environment variable style keys (`Section__Key`).

### One-time setup flow
1. Copy example settings file:
```powershell
Copy-Item .\.local\appservice-dev-settings.example.json .\.local\appservice-dev-settings.json
```
2. Fill real values in `.local/appservice-dev-settings.json`.
3. Login to Azure CLI:
```powershell
az login
```
4. Apply settings:
```powershell
.\scripts\set-appservice-settings.ps1 `
  -ResourceGroup development `
  -WebAppName dev-medinsights-api `
  -SettingsFile .\.local\appservice-dev-settings.json `
  -SubscriptionId 442097e4-9be8-4f9b-ba4b-b31d8f503ce6
```
5. Restart app (optional but recommended):
```powershell
az webapp restart --resource-group development --name dev-medinsights-api
```

### Script notes
- Script path: `scripts/set-appservice-settings.ps1`
- Input JSON must be a flat object of `\"Key\": \"Value\"` pairs.
- Use `__` in keys for nested config sections.
- Keep real values only in local files / secure vaults, not in source control.
