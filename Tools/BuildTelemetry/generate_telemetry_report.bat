@echo off
setlocal EnableExtensions

for %%I in ("%~dp0..\..") do set "PROJECT_ROOT=%%~fI"
set "VENV_DIR=%~dp0.venv"
set "VENV_PYTHON=%VENV_DIR%\Scripts\python.exe"

if exist "%VENV_PYTHON%" goto :run_report

echo Creando el entorno virtual local de telemetria...
python -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 9) else 1)" >nul 2>nul
if not errorlevel 1 goto :create_with_python

py -3 -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 9) else 1)" >nul 2>nul
if not errorlevel 1 goto :create_with_py

echo [ERROR] No se ha encontrado Python 3.9 o superior.
if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
exit /b 2

:create_with_python
python -m venv "%VENV_DIR%"
goto :verify_environment

:create_with_py
py -3 -m venv "%VENV_DIR%"

:verify_environment
if errorlevel 1 (
    echo [ERROR] No se ha podido crear el entorno virtual local.
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 2
)
if not exist "%VENV_PYTHON%" (
    echo [ERROR] El entorno virtual no contiene un Python valido.
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 2
)

:run_report

set "WARMUP=%TELEMETRY_WARMUP_SECONDS%"
if not defined WARMUP set "WARMUP=3"

"%VENV_PYTHON%" "%~dp0generate_telemetry_report.py" --input "%PROJECT_ROOT%\BuildTelemetryReports\raw" --output "%PROJECT_ROOT%\BuildTelemetryReports\reports" --warmup-seconds "%WARMUP%" %*
if errorlevel 1 (
    echo [ERROR] No se pudo generar el informe.
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 3
)

if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
exit /b 0
