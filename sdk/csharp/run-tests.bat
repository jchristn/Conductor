@echo off
REM Run the Conductor C# SDK test harness.
REM Usage: run-tests.bat [additional dotnet test arguments]
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
  echo dotnet was not found on PATH. Install the .NET 10 SDK. 1>&2
  exit /b 1
)

dotnet test Conductor.Sdk.slnx %*
exit /b %ERRORLEVEL%
