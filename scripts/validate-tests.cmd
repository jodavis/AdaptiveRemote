@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0validate-tests.ps1"
exit /b %ERRORLEVEL%
