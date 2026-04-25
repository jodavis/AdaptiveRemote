@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0validate.ps1"
exit /b %ERRORLEVEL%
