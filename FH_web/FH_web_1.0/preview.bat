@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
title Fishing Heaven Official Website
where python >nul 2>nul
if %errorlevel%==0 (
  start "" "http://localhost:8080"
  python -m http.server 8080
  goto :eof
)
where py >nul 2>nul
if %errorlevel%==0 (
  start "" "http://localhost:8080"
  py -m http.server 8080
  goto :eof
)
start "" "%~dp0index.html"
