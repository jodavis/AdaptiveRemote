@echo off
pushd %~dp0..\ml
mypy --strict pipeline test
if %ERRORLEVEL% neq 0 ( popd & exit /b %ERRORLEVEL% )
popd
