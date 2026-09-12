@echo off
setlocal EnableExtensions
set "SCRIPT_DIR=%~dp0"
where python >nul 2>nul || (echo Python is required to create a release. 1>&2 & exit /b 1)
python "%SCRIPT_DIR%scripts\release.py" %*
exit /b %ERRORLEVEL%
