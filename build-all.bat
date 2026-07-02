@echo off
setlocal

if "%~1"=="" (
    echo Error: Image tag is required.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)

if not "%~2"=="" (
    echo Error: build-all accepts a single image tag argument.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)

set "IMAGE_TAG=%~1"
set "ROOT_DIR=%~dp0"

call "%ROOT_DIR%build-server.bat" "%IMAGE_TAG%"
if errorlevel 1 exit /b %errorlevel%

call "%ROOT_DIR%build-dashboard.bat" "%IMAGE_TAG%"
if errorlevel 1 exit /b %errorlevel%

echo All Docker images built and pushed with tag %IMAGE_TAG% and latest.
exit /b 0
