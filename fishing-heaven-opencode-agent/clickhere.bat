@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Fishing Heaven - OpenCode Agent

rem ============================================================
rem Fishing Heaven OpenCode Agent Launcher
rem
rem Place this file in the repository root and double-click it.
rem It ONLY starts OpenCode CLI in this repository.
rem It does NOT create/rebuild the Tuanjie project.
rem It does NOT run the old FH11 one-click installer.
rem It does NOT automatically execute /fh-apply.
rem ============================================================

cd /d "%~dp0"

echo.
echo ============================================================
echo   Fishing Heaven OpenCode Agent
echo ============================================================
echo.
echo Repository:
echo   %CD%
echo.

rem --- Basic repository checks ---
if not exist ".opencode\" (
    echo [ERROR] .opencode folder was not found.
    echo.
    echo Put clickhere.bat in the ROOT of the agent repository.
    echo Expected:
    echo   .opencode\
    echo   CSharp_Upload\
    echo   FH_Agent\
    echo.
    pause
    exit /b 10
)

if not exist "CSharp_Upload\FH_simple.cs" (
    echo [ERROR] CSharp_Upload\FH_simple.cs was not found.
    echo.
    echo Add the current approved C# file here:
    echo   CSharp_Upload\FH_simple.cs
    echo.
    pause
    exit /b 11
)

rem --- Find OpenCode CLI ---
set "OPENCODE="

for /f "delims=" %%I in ('where opencode.cmd 2^>nul') do (
    if not defined OPENCODE set "OPENCODE=%%I"
)

if not defined OPENCODE (
    for /f "delims=" %%I in ('where opencode.exe 2^>nul') do (
        if not defined OPENCODE set "OPENCODE=%%I"
    )
)

if not defined OPENCODE (
    for /f "delims=" %%I in ('where opencode 2^>nul') do (
        if not defined OPENCODE set "OPENCODE=%%I"
    )
)

if not defined OPENCODE if exist "%APPDATA%\npm\opencode.cmd" (
    set "OPENCODE=%APPDATA%\npm\opencode.cmd"
)

if not defined OPENCODE (
    echo [ERROR] OpenCode CLI was not found.
    echo.
    echo Checked:
    echo   PATH: opencode.cmd / opencode.exe / opencode
    echo   %%APPDATA%%\npm\opencode.cmd
    echo.
    echo Install or repair OpenCode CLI, then run this file again.
    echo This launcher will NOT download or install anything automatically.
    echo.
    pause
    exit /b 20
)

echo OpenCode:
echo   %OPENCODE%
echo.
echo C# source:
echo   %CD%\CSharp_Upload\FH_simple.cs
echo.
echo ------------------------------------------------------------
echo OpenCode will now start in BLACK TERMINAL CLI mode.
echo.
echo Recommended commands after OpenCode starts:
echo   /fh-status
echo   /fh-verify
echo   /fh-apply
echo.
echo IMPORTANT:
echo   /fh-apply may deploy the approved C# file to the game project.
echo   This launcher itself does not modify C# or rebuild the project.
echo ------------------------------------------------------------
echo.

call "%OPENCODE%"
set "EXITCODE=%ERRORLEVEL%"

echo.
echo OpenCode exited with code: %EXITCODE%
echo.
pause
exit /b %EXITCODE%
