param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repositoryRoot
try {
    dotnet run --project tests/TakenLi.Core.Checks --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    dotnet publish src/TakenLi.Windows/TakenLi.Windows.csproj --configuration Release --runtime $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false --output "artifacts/$Runtime"
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    Get-FileHash "artifacts/$Runtime/TakenLi.exe" -Algorithm SHA256
} finally {
    Pop-Location
}
