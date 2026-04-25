$ErrorActionPreference = 'Stop'

Write-Host 'Testing unit test projects'
dotnet test --no-build "$PSScriptRoot/validate-unit-tests.proj"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Testing E2E test projects'
dotnet test --no-build "$PSScriptRoot/validate-e2e-tests.proj"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
