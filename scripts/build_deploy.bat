@echo off
echo ==========================================
echo   Compilando y Exportando StardewDiscordRPC
echo ==========================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_deploy.ps1"
pause
