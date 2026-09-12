@echo off
setlocal EnableExtensions
where python >nul 2>nul || (echo Python is required for support setup. 1>&2 & exit /b 1)
python scripts\setup.py
