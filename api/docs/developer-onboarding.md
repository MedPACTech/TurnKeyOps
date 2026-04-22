# TurnKeyOps Developer Onboarding

This guide explains how to get the TurnKeyOps API running on a local machine.

Use this guide for:
- new developers
- AI coding agents
- anyone setting up a fresh local environment

## 1. Install the required tools

Install these tools before doing anything else:

1. Git
2. .NET SDK 10.x
3. Node.js LTS
4. Azurite
5. PowerShell 7+ (recommended)

Notes:
- The API uses Azure Table/Queue/Blob local emulation through Azurite.
- If you do not want PowerShell, you can still run the API. PowerShell is only needed for helper scripts.

## 2. Install Azurite

Install Azurite globally with npm:

```bash
npm install -g azurite
```

Verify it is installed:

```bash
azurite --version
```

## 3. Get the source code

Clone the repository and move into it:

```bash
git clone <REPO_URL>
cd TurnKeyOps/api
```

If you already have the repository, pull the latest changes:

```bash
git pull
```

## 4. Restore NuGet packages

Run:

```bash
dotnet restore TurnKeyOps.API.sln
```

Important:
- This repository restores packages from `nuget.org`.

## 5. Start Azurite

The local API configuration uses `UseDevelopmentStorage=true` in several places. That means Azurite must be running.

From the repository root, start Azurite in a dedicated folder:

```bash
mkdir -p .azurite
azurite --location ./.azurite --debug ./.azurite/debug.log
```

Keep Azurite running in its own terminal window while you work.

If you want to confirm it started correctly, you should see Azurite listening locally for blob, queue, and table storage.

## 6. Create your local secrets file

The API reads local secrets from:

- `.local/user-secrets.json`
- optional .NET user-secrets store

Create the local file from the example:

```powershell
Copy-Item .\.local\user-secrets.example.json .\.local\user-secrets.json
```

Or with `cp`:

```bash
cp ./.local/user-secrets.example.json ./.local/user-secrets.json
```

## 7. Fill in required secrets

Open `.local/user-secrets.json` and add the real values you need.

At minimum, review these keys:

1. `ConnectionStrings:AzureServiceBus`
2. `IBeam:Communications:Email:Providers:AzureCommunications:ConnectionString`
3. `IBeam:Communications:Sms:Providers:AzureCommunications:ConnectionString`
4. `IBeam:Identity:Jwt:SigningKey`
5. `IBeam:Identity:Otp:HashSalt`
6. `IBeam:Identity:Otp:VerificationTokenSecret`
7. `OpenAISettings:Key`
8. `AzureSpeechSettings:Key`
9. `StripeSettings:SecretKey`
10. `StripeSettings:PublicKey`
11. `StripeSettings:WebhookSecret`
12. `PayPalSettings:*` if your work requires PayPal

Local storage settings can stay on Azurite defaults:

- `AzureStorageSettings:ConnectionString = UseDevelopmentStorage=true`
- `IBeam:Identity:AzureTable:StorageConnectionString = UseDevelopmentStorage=true`
- `IBeam:Repositories:AzureTables:ConnectionString = UseDevelopmentStorage=true`

## 8. Optional: sync local secrets into .NET user-secrets

This is optional because the API already reads `.local/user-secrets.json` directly in `Local` and `Development`.

If you also want the values in the standard .NET user-secrets store, run:

```powershell
.\scripts\setup-local-secrets.ps1
```

Then verify:

```powershell
dotnet user-secrets list --project .\TurnKeyOps.API\TurnKeyOps.API.csproj
```

## 9. Build the solution

Run:

```bash
dotnet build TurnKeyOps.API.sln
```

This confirms:
- package restore worked
- secrets/config can be resolved
- the code compiles on your machine

## 10. Run the API locally

Start the API with the local launch profile:

```bash
dotnet run --project ./TurnKeyOps.API/TurnKeyOps.API.csproj --launch-profile http
```

What this does:

1. Runs the API at `http://localhost:5178`
2. Sets `ASPNETCORE_ENVIRONMENT=Local`
3. Loads `appsettings.json`
4. Loads `appsettings.Local.json`
5. Loads `.local/user-secrets.json`
6. Loads optional .NET user-secrets
7. Runs startup seed contributors

Swagger should be available at:

```text
http://localhost:5178/swagger
```

## 11. Understand what is seeded automatically

On startup, the API runs `IStartupSeeder`.

This currently seeds:

1. System note types and note type profiles
2. Authorization roles
3. Role-permission mappings

This automatic seeding does not create demo patients or full sample tenant data.

## 12. Choose a local data preload option

There are two main preload paths.

### Option A: reference data import

Use this for static datasets such as ICD-10.

Validate only:

```bash
dotnet run --project Tools/ReferenceDataLoader -- --dataset icd10 --what-if
```

Import:

```bash
dotnet run --project Tools/ReferenceDataLoader -- --dataset icd10
```

Or explicitly target Azurite:

```bash
dotnet run --project Tools/ReferenceDataLoader -- --dataset icd10 --connection-string "UseDevelopmentStorage=true"
```

### Option B: demo patient data seed

Use this for realistic demo records for a tenant.

Important:
- demo seed requires a tenant ID
- it does not create the tenant for you

Quick start:

```powershell
.\Tools\ReferenceDataLoader\seed-demo-data.ps1 -TenantId <tenant-guid>
```

Direct command:

```bash
dotnet run --project Tools/ReferenceDataLoader -- --mode demo --tenant-id <tenant-guid> --patients 10
```

Useful options:

1. `--patients <n>`
2. `--modules <list>`
3. `--seed <n>`
4. `--what-if`
5. `--connection-string <value>`

Available demo modules:

1. `patients`
2. `contacts`
3. `allergies`
4. `family-history`
5. `vitals`
6. `marital-history`
7. `military-history`
8. `environmental-history`

Example partial seed:

```bash
dotnet run --project Tools/ReferenceDataLoader -- --mode demo --tenant-id <tenant-guid> --modules patients,contacts,allergies
```

## 13. Verify the API is healthy

Use these checks:

1. Open Swagger:

```text
http://localhost:5178/swagger
```

2. Open the public auth diagnostic endpoint:

```text
http://localhost:5178/api/test-auth/public
```

Expected result:
- a simple success payload showing the API is reachable

## 14. Common local problems

### Problem: storage errors on startup

Cause:
- Azurite is not running

Fix:
- start Azurite
- keep it running while using the API

### Problem: package restore fails

Cause:
- general NuGet connectivity or package source issues

Fix:
- verify access to `https://api.nuget.org/v3/index.json`
- then run `dotnet restore` again

### Problem: app starts but external features fail

Cause:
- missing real secrets in `.local/user-secrets.json`

Fix:
- fill the required secret values
- restart the API

### Problem: OTP or auth errors

Cause:
- invalid JWT/OTP secrets
- missing Azure Communication settings
- stale local storage rows from previous runs

Fix:
- verify `IBeam:Identity:*` settings
- verify communication provider connection strings
- clear or recreate local Azurite data if needed

## 15. Day-to-day commands

Restore:

```bash
dotnet restore TurnKeyOps.API.sln
```

Build:

```bash
dotnet build TurnKeyOps.API.sln
```

Test:

```bash
dotnet test TurnKeyOps.API.sln
```

Run API:

```bash
dotnet run --project ./TurnKeyOps.API/TurnKeyOps.API.csproj --launch-profile http
```

## 16. Rules for local configuration

Follow these rules:

1. Never commit real secrets into `appsettings.*.json`
2. Never commit `.local/user-secrets.json`
3. Use `appsettings.Local.json` only for non-secret local defaults
4. Use Azure App Service configuration for deployed environments
5. Keep Azurite running when local storage is set to `UseDevelopmentStorage=true`

## 17. Minimum successful setup checklist

A developer is fully set up when all of these are true:

1. repository cloned
2. .NET SDK installed
3. Azurite installed and running
4. `.local/user-secrets.json` created and filled
5. `dotnet restore` succeeds
6. `dotnet build` succeeds
7. API runs on `http://localhost:5178`
8. Swagger loads
