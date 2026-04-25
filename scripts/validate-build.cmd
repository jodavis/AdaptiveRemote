@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0validate-build.ps1"
exit /b %ERRORLEVEL%
