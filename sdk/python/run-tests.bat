@echo off
REM Run the Conductor Python SDK test harness (unittest).
REM Usage: run-tests.bat
REM Set PYTHON to choose an interpreter; otherwise python is used.
REM When the interpreter cannot import requests, a local virtual environment is
REM created in .venv and requests is installed into it.
setlocal
cd /d "%~dp0"

if "%PYTHON%"=="" set "PYTHON=python"

"%PYTHON%" -c "import sys" >nul 2>&1
if errorlevel 1 (
  echo Python was not found on PATH. Install Python 3.10 or later. 1>&2
  exit /b 1
)

"%PYTHON%" -c "import requests" >nul 2>&1
if errorlevel 1 (
  if not exist .venv (
    "%PYTHON%" -m venv .venv
    if errorlevel 1 exit /b 1
  )
  set "PYTHON=%~dp0.venv\Scripts\python.exe"
)

"%PYTHON%" -c "import requests" >nul 2>&1
if errorlevel 1 (
  "%PYTHON%" -m pip install --quiet --disable-pip-version-check "requests>=2.31.0"
  if errorlevel 1 exit /b 1
)

set "PYTHONPATH=src;%PYTHONPATH%"
"%PYTHON%" -m unittest discover -s tests -v
exit /b %ERRORLEVEL%
