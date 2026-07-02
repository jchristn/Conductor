@echo off
setlocal

if "%~1"=="" (
    echo Error: Image tag is required.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)

if not "%~2"=="" (
    echo Error: build-server accepts a single image tag argument.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)

set "IMAGE_TAG=%~1"
set "IMAGE_NAME=jchristn77/conductor-server"
set "ROOT_DIR=%~dp0"

pushd "%ROOT_DIR%" || exit /b 1

echo Building %IMAGE_NAME%:%IMAGE_TAG% and %IMAGE_NAME%:latest...
docker build -f src\Conductor.Server\Dockerfile -t "%IMAGE_NAME%:%IMAGE_TAG%" -t "%IMAGE_NAME%:latest" .
if errorlevel 1 goto :fail

echo Pushing %IMAGE_NAME%:%IMAGE_TAG%...
docker push "%IMAGE_NAME%:%IMAGE_TAG%"
if errorlevel 1 goto :fail

echo Pushing %IMAGE_NAME%:latest...
docker push "%IMAGE_NAME%:latest"
if errorlevel 1 goto :fail

popd
echo Server image build and push completed.
exit /b 0

:fail
set "EXITCODE=%errorlevel%"
popd
exit /b %EXITCODE%
