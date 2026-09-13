@echo off
setlocal EnableExtensions
set "SCRIPT_DIR=%~dp0"
dotnet run --no-build --project "%SCRIPT_DIR%src\StateKeep\StateKeep.csproj" -- %*
exit /b %ERRORLEVEL%
