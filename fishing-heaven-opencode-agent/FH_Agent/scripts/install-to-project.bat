@echo off
setlocal
chcp 65001 >nul
title Fishing Heaven OpenCode Agent Installer

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-to-project.ps1" %*

echo.
pause
