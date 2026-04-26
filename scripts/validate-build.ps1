$ErrorActionPreference = 'Stop'

Write-Host 'Staging untracked files...'
git add -A
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Cleaning src projects...'
cd "$PSScriptRoot/../src"
git clean -xdf
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Cleaning test projects...'
cd "$PSScriptRoot/../test"
git clean -xdf
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Building solution...'
cd "$PSScriptRoot/.."
dotnet build /warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
