param(
    [string]$Dataset = "icd10",
    [string]$Manifest = "ReferenceData/manifest.json",
    [string]$ConnectionString = "",
    [string]$Table = "",
    [switch]$SkipValidation,
    [switch]$WhatIf,
    [int]$MaxRows = 0
)

$argsList = @(
    "--dataset", $Dataset,
    "--manifest", $Manifest
)

if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    $argsList += @("--connection-string", $ConnectionString)
}

if (-not [string]::IsNullOrWhiteSpace($Table)) {
    $argsList += @("--table", $Table)
}

if ($SkipValidation) {
    $argsList += "--skip-validation"
}

if ($WhatIf) {
    $argsList += "--what-if"
}

if ($MaxRows -gt 0) {
    $argsList += @("--max-rows", "$MaxRows")
}

dotnet run --project Tools/ReferenceDataLoader -- @argsList
