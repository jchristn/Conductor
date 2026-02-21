@echo off
setlocal enabledelayedexpansion

REM
REM Conductor Docker Factory Reset
REM
REM Resets the Docker deployment to its default configuration by:
REM - Stopping all containers
REM - Restoring the factory conductor.json configuration
REM - Rebuilding a clean database with default records
REM - Removing all log files and request history
REM

set "SCRIPT_DIR=%~dp0"
set "DOCKER_DIR=%SCRIPT_DIR%.."

echo ========================================================================
echo   CONDUCTOR FACTORY RESET
echo ========================================================================
echo.
echo   This will completely reset your Conductor Docker deployment to the
echo   factory default state. ALL data will be lost, including:
echo.
echo     - All tenants, users, and credentials
echo     - All model runner endpoints, definitions, and configurations
echo     - All virtual model runners
echo     - All request history
echo     - All log files
echo     - Any custom configuration
echo.
echo   Default credentials after reset:
echo     Admin Email:    admin@conductor
echo     Admin Password: password
echo     Admin API Key:  conductoradmin
echo     API Bearer Token: factory_default_bearer_token_0000000000000000000000000000000000
echo.
echo ========================================================================
echo.
set /p CONFIRM="  Type RESET to proceed: "
echo.

if not "%CONFIRM%"=="RESET" (
    echo   Reset cancelled.
    exit /b 1
)

echo   [1/5] Stopping Docker containers...
pushd "%DOCKER_DIR%"
docker compose down 2>nul || docker-compose down 2>nul
popd

echo   [2/5] Restoring factory configuration...
copy /y "%SCRIPT_DIR%conductor.json" "%DOCKER_DIR%\conductor.json" >nul

echo   [3/5] Removing database and request history...
if exist "%DOCKER_DIR%\data\conductor.db" del /f /q "%DOCKER_DIR%\data\conductor.db"
if exist "%DOCKER_DIR%\data\conductor.db-wal" del /f /q "%DOCKER_DIR%\data\conductor.db-wal"
if exist "%DOCKER_DIR%\data\conductor.db-shm" del /f /q "%DOCKER_DIR%\data\conductor.db-shm"
if exist "%DOCKER_DIR%\data\request-history" (
    rd /s /q "%DOCKER_DIR%\data\request-history"
)
mkdir "%DOCKER_DIR%\data\request-history" 2>nul

echo   [4/5] Removing log files...
for /r "%DOCKER_DIR%\logs" %%f in (*) do (
    if not "%%~nxf"==".gitkeep" del /f /q "%%f" 2>nul
)

echo   [5/5] Rebuilding factory database...
where sqlite3 >nul 2>&1
if %errorlevel% equ 0 (
    sqlite3 "%DOCKER_DIR%\data\conductor.db" < "%SCRIPT_DIR%schema.sql"
    echo          Factory database created with default records.
) else (
    echo          sqlite3 not found. The database will be created
    echo          automatically with new default credentials on next startup.
    echo          Note: The auto-generated credentials will differ from the
    echo          factory defaults listed above.
)

echo.
echo ========================================================================
echo   RESET COMPLETE
echo.
echo   Start the deployment with:
echo     cd %DOCKER_DIR%
echo     docker compose up -d
echo.
echo   Dashboard: http://localhost:9100
echo   API:       http://localhost:9000
echo ========================================================================

endlocal
