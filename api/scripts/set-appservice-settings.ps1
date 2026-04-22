param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $true)]
    [string]$SettingsFile,

    [string]$SubscriptionId = ""
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI ('az') is required but was not found in PATH."
}

if (-not (Test-Path $SettingsFile)) {
    throw "Settings file not found: $SettingsFile"
}

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    az account set --subscription $SubscriptionId | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set Azure subscription to $SubscriptionId"
    }
}

$settingsJson = Get-Content $SettingsFile -Raw | ConvertFrom-Json

if (-not ($settingsJson -is [System.Collections.IDictionary] -or $settingsJson -is [pscustomobject])) {
    throw 'Settings file must be a flat JSON object of { "Key": "Value" } entries.'
}

$entries = @()
foreach ($prop in $settingsJson.PSObject.Properties) {
    if ($prop.Value -is [System.Collections.IEnumerable] -and -not ($prop.Value -is [string])) {
        throw "Nested objects/arrays are not supported in $SettingsFile. Use flat App Service keys (e.g. Section__Key)."
    }

    $value = if ($null -eq $prop.Value) { "" } else { [string]$prop.Value }
    $entries += [pscustomobject]@{
        Name = [string]$prop.Name
        Value = $value
    }
}

if ($entries.Count -eq 0) {
    throw "No settings found in $SettingsFile"
}

Write-Host "Applying $($entries.Count) App Settings to $WebAppName in $ResourceGroup..."

foreach ($entry in $entries) {
    Write-Host "Setting $($entry.Name)..."
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings "$($entry.Name)=$($entry.Value)" `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed while applying App Setting key: $($entry.Name)"
    }
}

Write-Host "Done."
