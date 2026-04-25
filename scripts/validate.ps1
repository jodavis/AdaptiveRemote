$ErrorActionPreference = 'Stop'

& "$PSScriptRoot/validate-build.ps1"
& "$PSScriptRoot/validate-tests.ps1"
