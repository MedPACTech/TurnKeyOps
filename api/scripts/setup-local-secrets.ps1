param(
    [string]$ProjectPath = "./TurnKeyOps.API/TurnKeyOps.API.csproj",
    [string]$SecretsFile = "./.local/user-secrets.json"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required but was not found in PATH."
}

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

if (-not (Test-Path $SecretsFile)) {
    throw "Secrets file not found: $SecretsFile"
}

function Flatten-JsonObject {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [string]$Prefix = ""
    )

    $flat = @{}

    if ($InputObject -is [System.Collections.IDictionary]) {
        foreach ($key in $InputObject.Keys) {
            $fullKey = if ([string]::IsNullOrEmpty($Prefix)) { "$key" } else { "${Prefix}:$key" }
            $child = Flatten-JsonObject -InputObject $InputObject[$key] -Prefix $fullKey
            foreach ($childKey in $child.Keys) { $flat[$childKey] = $child[$childKey] }
        }
        return $flat
    }

    if ($InputObject -is [pscustomobject]) {
        foreach ($prop in $InputObject.PSObject.Properties) {
            $fullKey = if ([string]::IsNullOrEmpty($Prefix)) { $prop.Name } else { "${Prefix}:$($prop.Name)" }
            $child = Flatten-JsonObject -InputObject $prop.Value -Prefix $fullKey
            foreach ($childKey in $child.Keys) { $flat[$childKey] = $child[$childKey] }
        }
        return $flat
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        throw "Arrays are not supported in user secrets bootstrap file. Key: $Prefix"
    }

    if ([string]::IsNullOrEmpty($Prefix)) {
        throw "Invalid JSON shape. Expected object with key/value pairs."
    }

    if ($null -eq $InputObject) {
        $flat[$Prefix] = ""
    } else {
        $flat[$Prefix] = [string]$InputObject
    }

    return $flat
}

$json = Get-Content $SecretsFile -Raw | ConvertFrom-Json
$entries = Flatten-JsonObject -InputObject $json

if ($entries.Count -eq 0) {
    throw "No keys found in $SecretsFile"
}

Write-Host "Applying $($entries.Count) user-secrets entries to $ProjectPath..."

foreach ($key in $entries.Keys | Sort-Object) {
    $value = $entries[$key]
    if ($null -eq $value -or $value -eq "") {
        Write-Host "Skip $key (empty value)"
        continue
    }

    dotnet user-secrets set "$key" "$value" --project $ProjectPath | Out-Null
    Write-Host "Set $key"
}

Write-Host "Done. Current key count:"
dotnet user-secrets list --project $ProjectPath | Measure-Object | ForEach-Object { Write-Host $_.Count }
