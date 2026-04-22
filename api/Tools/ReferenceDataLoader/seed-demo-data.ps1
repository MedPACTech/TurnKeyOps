param(
    [Parameter(Mandatory=$true)]
    [string]$TenantId,
    [int]$Patients = 10,
    [string]$ConnectionString = "",
    [string]$Modules = "all",
    [int]$Seed = 0,
    [switch]$WhatIf
)

$argsList = @(
    "--mode", "demo",
    "--tenant-id", $TenantId,
    "--patients", "$Patients",
    "--modules", $Modules
)

if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    $argsList += @("--connection-string", $ConnectionString)
}

if ($Seed -ne 0) {
    $argsList += @("--seed", "$Seed")
}

if ($WhatIf) {
    $argsList += "--what-if"
}

dotnet run --project Tools/ReferenceDataLoader -- @argsList
