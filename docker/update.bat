@echo off
docker compose down && docker compose pull && docker compose up -d && docker ps -a
