@echo off
setlocal
chcp 65001 >nul
title Fishing Heaven - OpenCode CLI

cd /d "%~dp0..\.."

echo ============================================================
echo  Fishing Heaven OpenCode Agent
echo ============================================================
echo.
echo CLI/TUI only.
echo No opencode web.
echo.

where opencode.cmd >nul 2>nul
if "%ERRORLEVEL%"=="0" (
    call opencode.cmd
    goto :end
)

where opencode.exe >nul 2>nul
if "%ERRORLEVEL%"=="0" (
    opencode.exe
    goto :end
)

echo [ERROR] OpenCode CLI not found in PATH.

:end
echo.
pause
