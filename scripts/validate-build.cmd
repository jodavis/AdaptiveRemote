@echo off
pushd %~dp0..
git add -A
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
git clean -xdf src test
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
dotnet build /warnaserror
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
popd
