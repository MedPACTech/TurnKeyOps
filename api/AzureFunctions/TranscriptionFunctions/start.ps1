Set-Location $PSScriptRoot

$env:FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
func start --project .\TranscriptionFunctions.csproj
