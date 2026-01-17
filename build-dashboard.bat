@echo off
echo Building Conductor Dashboard...

set TAG=%1
if "%TAG%"=="" set TAG=latest

cd /d "%~dp0\dashboard"

echo Building and pushing multi-platform Docker image...
if "%TAG%"=="latest" (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor-ui:latest --push .
) else (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor-ui:%TAG% -t jchristn77/conductor-ui:latest --push .
)

cd ..

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build and push successful!
    if "%TAG%"=="latest" (
        echo Image: jchristn77/conductor-ui:latest
    ) else (
        echo Images: jchristn77/conductor-ui:%TAG%, jchristn77/conductor-ui:latest
    )
    echo Platforms: linux/amd64, linux/arm64/v8
) else (
    echo.
    echo Build failed!
    exit /b 1
)
