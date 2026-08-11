@echo off
setlocal EnableExtensions

for %%I in ("%~dp0..\..") do set "PROJECT_ROOT=%%~fI"
set "QUALITY=%~1"
if not defined QUALITY set "QUALITY=PC"

set "WIDTH=%TELEMETRY_WIDTH%"
if not defined WIDTH set "WIDTH=1920"
set "HEIGHT=%TELEMETRY_HEIGHT%"
if not defined HEIGHT set "HEIGHT=1080"

if exist "%PROJECT_ROOT%\Temp\UnityLockfile" (
    powershell -NoProfile -Command "$lockStream=$null; try { $lockStream=[System.IO.File]::Open('%PROJECT_ROOT%\Temp\UnityLockfile',[System.IO.FileMode]::Open,[System.IO.FileAccess]::ReadWrite,[System.IO.FileShare]::None); exit 0 } catch { exit 1 } finally { if ($null -ne $lockStream) { $lockStream.Dispose() } }" >nul 2>nul
    if errorlevel 1 (
        echo [ERROR] Unity tiene abierto este proyecto.
        echo Cierra el editor antes de crear la build de telemetria y vuelve a ejecutar este BAT.
        if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
        exit /b 2
    )
)

for /f "tokens=2 delims=:" %%A in ('findstr /B /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do set "UNITY_VERSION=%%A"
set "UNITY_VERSION=%UNITY_VERSION: =%"

set "UNITY_EDITOR=%TELEMETRY_UNITY_EXE%"
if not defined UNITY_EDITOR set "UNITY_EDITOR=C:\Program Files\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"
if not exist "%UNITY_EDITOR%" (
    echo [ERROR] No se ha encontrado Unity %UNITY_VERSION% en:
    echo %UNITY_EDITOR%
    echo Define TELEMETRY_UNITY_EXE con la ruta correcta y repite el proceso.
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 3
)

for /f %%A in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HH-mm-ss"') do set "STAMP=%%A"
for /f %%A in ('git -C "%PROJECT_ROOT%" rev-parse HEAD') do set "GIT_COMMIT=%%A"
for /f "delims=" %%A in ('git -C "%PROJECT_ROOT%" branch --show-current') do set "GIT_BRANCH=%%A"
if not defined GIT_COMMIT set "GIT_COMMIT=unknown"
if not defined GIT_BRANCH set "GIT_BRANCH=detached"
if not "%GIT_COMMIT%"=="unknown" set "GIT_COMMIT=%GIT_COMMIT:~0,12%"

set "GIT_DIRTY=false"
for /f "delims=" %%A in ('git -C "%PROJECT_ROOT%" status --porcelain') do set "GIT_DIRTY=true"

set "QUALITY_SAFE=%QUALITY: =_%"
set "BUILD_DIR=%PROJECT_ROOT%\Builds\Telemetry\%STAMP%_%GIT_COMMIT%_%QUALITY_SAFE%"
set "BUILD_EXE=%BUILD_DIR%\FattoPrizzervaTelemetry.exe"
set "RAW_DIR=%PROJECT_ROOT%\BuildTelemetryReports\raw"
set "LOG_DIR=%PROJECT_ROOT%\BuildTelemetryReports\player-logs"

mkdir "%BUILD_DIR%" 2>nul
mkdir "%RAW_DIR%" 2>nul
mkdir "%LOG_DIR%" 2>nul

set "DEVELOPMENT_VALUE=0"
set "BUILD_TYPE=Release"
if /I "%TELEMETRY_DEVELOPMENT%"=="1" (
    set "DEVELOPMENT_VALUE=1"
    set "BUILD_TYPE=Development"
)

echo.
echo ============================================================
echo BUILD DE TELEMETRIA
echo Unity:      %UNITY_VERSION%
echo Commit:     %GIT_COMMIT%  ^(dirty: %GIT_DIRTY%^)
echo Rama:       %GIT_BRANCH%
echo Calidad:    %QUALITY%
echo Resolucion: %WIDTH%x%HEIGHT%
echo Tipo:       %BUILD_TYPE% ^(TELEMETRY_DEVELOPMENT=1 para Development^)
echo ============================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_unity_build.ps1" -UnityEditor "%UNITY_EDITOR%" -ProjectRoot "%PROJECT_ROOT%" -BuildExe "%BUILD_EXE%" -LogFile "%BUILD_DIR%\unity-build.log" -Development "%DEVELOPMENT_VALUE%"
if errorlevel 1 (
    echo [ERROR] La build ha fallado. Revisa:
    echo %BUILD_DIR%\unity-build.log
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 4
)

if not exist "%BUILD_EXE%" (
    echo [ERROR] Unity termino sin crear el ejecutable esperado:
    echo %BUILD_EXE%
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 5
)

echo.
echo Iniciando la build. Juega el recorrido que quieras medir y cierra el juego al terminar.
echo Durante la partida solo se guardan muestras y eventos crudos.
echo.

start "Fatto Prizzerva - Telemetria" /wait "%BUILD_EXE%" -buildTelemetry -telemetryOutput "%RAW_DIR%" -telemetryCommit "%GIT_COMMIT%" -telemetryBranch "%GIT_BRANCH%" -telemetryGitDirty "%GIT_DIRTY%" -telemetryQuality "%QUALITY%" -screen-fullscreen 0 -screen-width "%WIDTH%" -screen-height "%HEIGHT%" -logFile "%LOG_DIR%\player_%STAMP%.log"

echo.
echo Captura terminada. Los datos crudos estan en:
echo %RAW_DIR%
echo.
echo Para calcular estadisticas ejecuta:
echo %~dp0generate_telemetry_report.bat
if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
exit /b 0
