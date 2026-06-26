@echo off
docker compose down && docker compose build && docker compose up -d && docker ps -a
