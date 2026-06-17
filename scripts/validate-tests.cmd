@echo off
pushd %~dp0..
dotnet test --no-build "%~dp0validate-unit-tests.proj"
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
pushd ml
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
python -m pytest
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
popd
dotnet test --no-build "%~dp0validate-e2e-tests.proj"
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
popd
