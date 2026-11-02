[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName
$dotnet = Join-Path $repositoryRoot ".tools\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$project = Join-Path $repositoryRoot "tests\AdPulse.Api.Tests\AdPulse.Api.Tests.csproj"
$filter = "FullyQualifiedName~AdPulse.Api.Tests.ReleaseGates"
$env:ADPULSE_REPOSITORY_ROOT = $repositoryRoot

Write-Host "Running local Phase 2 release gates (SQL Server and Docker are not required)."
& $dotnet test $project `
    --configuration $Configuration `
    --no-restore `
    --filter $filter `
    --verbosity minimal
$testExitCode = $LASTEXITCODE

Write-Host "SKIP [environment] SQL Server migration execution and SQL-specific constraint checks."
Write-Host "SKIP [environment] External OIDC discovery, signing-key rotation, and provider-bound bearer flow."
Write-Host "SKIP [environment] Browser/UI smoke and accessibility checks until the Vue screens exist."

exit $testExitCode
