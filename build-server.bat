@echo off
echo Building Conductor Server...

set TAG=%1
if "%TAG%"=="" set TAG=latest

cd /d "%~dp0"

echo Building and pushing multi-platform Docker image...
if "%TAG%"=="latest" (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor:latest -f src/Conductor.Server/Dockerfile --push .
) else (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor:%TAG% -t jchristn77/conductor:latest -f src/Conductor.Server/Dockerfile --push .
)

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build and push successful!
    if "%TAG%"=="latest" (
        echo Image: jchristn77/conductor:latest
    ) else (
        echo Images: jchristn77/conductor:%TAG%, jchristn77/conductor:latest
    )
    echo Platforms: linux/amd64, linux/arm64/v8
) else (
    echo.
    echo Build failed!
    exit /b 1
)
