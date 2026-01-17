@echo off
echo Cleaning Conductor data files...

if exist conductor.json (
    del /f conductor.json
    echo Deleted conductor.json
)

if exist conductor.db (
    del /f conductor.db
    echo Deleted conductor.db
)

if exist logs (
    rmdir /s /q logs
    echo Deleted logs directory
)

echo Clean complete.
