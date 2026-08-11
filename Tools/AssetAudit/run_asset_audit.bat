@echo off
setlocal EnableExtensions EnableDelayedExpansion

for %%I in ("%~dp0..\..") do set "PROJECT_ROOT=%%~fI"
set "AUDITOR=%~dp0asset_audit.py"
set "VENV_DIR=%~dp0.venv"
set "VENV_PYTHON=%~dp0.venv\Scripts\python.exe"
set "EXIT_CODE=9009"

echo.
echo ============================================================
echo  Auditoria estatica de assets - Fatto Prizzerva Lightning
echo ============================================================
echo.

if exist "%VENV_PYTHON%" goto :run_audit

echo Preparando el entorno virtual local de Python...
set "BOOTSTRAP_PYTHON="

where python >nul 2>nul
if not errorlevel 1 (
    python -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 8) else 1)" >nul 2>nul
    if not errorlevel 1 set "BOOTSTRAP_PYTHON=python"
)

if not defined BOOTSTRAP_PYTHON (
    where py >nul 2>nul
    if not errorlevel 1 (
        call py -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 8) else 1)" >nul 2>nul
        if not errorlevel 1 set "BOOTSTRAP_PYTHON=py"
    )
)

if not defined BOOTSTRAP_PYTHON (
    where python3 >nul 2>nul
    if not errorlevel 1 (
        python3 -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 8) else 1)" >nul 2>nul
        if not errorlevel 1 set "BOOTSTRAP_PYTHON=python3"
    )
)

if not defined BOOTSTRAP_PYTHON (
    echo ERROR: No se ha encontrado Python 3.8 o superior.
    echo Instala Python 3 o anadelo al PATH y vuelve a ejecutar este archivo.
    goto :finish
)

call !BOOTSTRAP_PYTHON! -m venv "%VENV_DIR%"
if errorlevel 1 (
    echo ERROR: No se ha podido crear el entorno virtual en "%VENV_DIR%".
    goto :finish
)

if not exist "%VENV_PYTHON%" (
    echo ERROR: El entorno virtual no contiene un ejecutable de Python valido.
    goto :finish
)

echo Entorno virtual creado en "%VENV_DIR%".
echo.

:run_audit
"%VENV_PYTHON%" "%AUDITOR%" --project "%PROJECT_ROOT%" %*
set "EXIT_CODE=!ERRORLEVEL!"

:finish
echo.
if "!EXIT_CODE!"=="0" (
    echo Auditoria terminada correctamente.
) else (
    echo La auditoria termino con el codigo de error !EXIT_CODE!.
)

if /I not "%ASSET_AUDIT_NO_PAUSE%"=="1" pause
exit /b !EXIT_CODE!
