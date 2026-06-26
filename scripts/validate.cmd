@echo off
echo Checking required tools...
for %%T in (dotnet pwsh node python3 claude) do (
    where %%T >nul 2>&1 || (
        echo ERROR: Required tool '%%T' is not installed or not on PATH.
        exit /b 1
    )
)
call "%~dp0validate-build.cmd"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
call "%~dp0validate-tests.cmd"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
