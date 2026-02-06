#!/bin/bash
echo "Cleaning Conductor data files..."

if [ -f "conductor.json" ]; then
    rm -f conductor.json
    echo "Deleted conductor.json"
fi

if [ -f "conductor.db" ]; then
    rm -f conductor.db
    echo "Deleted conductor.db"
fi

if [ -d "logs" ]; then
    rm -rf logs
    echo "Deleted logs directory"
fi

echo "Clean complete."
