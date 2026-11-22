@echo off
REM Run the Conductor JavaScript SDK test harness (node --test).
REM Usage: run-tests.bat
setlocal
cd /d "%~dp0"

where node >nul 2>&1
if errorlevel 1 (
  echo node was not found on PATH. Install Node.js 20 or later. 1>&2
  exit /b 1
)

if exist package-lock.json (
  call npm ci --no-audit --no-fund
  if errorlevel 1 exit /b 1
)

call npm test
exit /b %ERRORLEVEL%
