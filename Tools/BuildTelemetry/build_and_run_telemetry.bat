@echo off
setlocal EnableExtensions

if "%~1"=="" goto :usage

for %%I in ("%~dp0..\..") do set "PROJECT_ROOT=%%~fI"
set "BUILD_EXE=%~f1"
set "QUALITY=%~2"
if not defined QUALITY set "QUALITY=PC"

if not exist "%BUILD_EXE%" (
    echo [ERROR] No se encuentra el ejecutable:
    echo %BUILD_EXE%
    if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
    exit /b 2
)

set "WIDTH=%TELEMETRY_WIDTH%"
if not defined WIDTH set "WIDTH=1920"
set "HEIGHT=%TELEMETRY_HEIGHT%"
if not defined HEIGHT set "HEIGHT=1080"

for /f %%A in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HH-mm-ss"') do set "STAMP=%%A"
for /f %%A in ('git -C "%PROJECT_ROOT%" rev-parse HEAD 2^>nul') do set "GIT_COMMIT=%%A"
for /f "delims=" %%A in ('git -C "%PROJECT_ROOT%" branch --show-current 2^>nul') do set "GIT_BRANCH=%%A"
if not defined GIT_COMMIT set "GIT_COMMIT=unknown"
if not defined GIT_BRANCH set "GIT_BRANCH=detached"
if not "%GIT_COMMIT%"=="unknown" set "GIT_COMMIT=%GIT_COMMIT:~0,12%"

set "GIT_DIRTY=false"
for /f "delims=" %%A in ('git -C "%PROJECT_ROOT%" status --porcelain 2^>nul') do set "GIT_DIRTY=true"

set "RAW_DIR=%PROJECT_ROOT%\BuildTelemetryReports\raw"
set "LOG_DIR=%PROJECT_ROOT%\BuildTelemetryReports\player-logs"
mkdir "%RAW_DIR%" 2>nul
mkdir "%LOG_DIR%" 2>nul

echo.
echo ============================================================
echo EJECUCION DE TELEMETRIA
echo Ejecutable: %BUILD_EXE%
echo Commit:     %GIT_COMMIT%  ^(dirty: %GIT_DIRTY%^)
echo Rama:       %GIT_BRANCH%
echo Calidad:    %QUALITY%
echo Resolucion: %WIDTH%x%HEIGHT%
echo ============================================================
echo.
echo Juega el recorrido que quieras medir y cierra el juego al terminar.
echo Los datos crudos se guardaran en:
echo %RAW_DIR%
echo.

start "Fatto Prizzerva - Telemetria" /wait "%BUILD_EXE%" -buildTelemetry -telemetryOutput "%RAW_DIR%" -telemetryCommit "%GIT_COMMIT%" -telemetryBranch "%GIT_BRANCH%" -telemetryGitDirty "%GIT_DIRTY%" -telemetryQuality "%QUALITY%" -screen-fullscreen 0 -screen-width "%WIDTH%" -screen-height "%HEIGHT%" -logFile "%LOG_DIR%\player_%STAMP%.log"
set "PLAYER_EXIT=%ERRORLEVEL%"

echo.
if "%PLAYER_EXIT%"=="0" (
    echo Captura terminada correctamente.
) else (
    echo [WARNING] El juego termino con el codigo %PLAYER_EXIT%.
)
echo Datos crudos: %RAW_DIR%
echo Para calcular estadisticas ejecuta:
echo %~dp0generate_telemetry_report.bat
if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
exit /b %PLAYER_EXIT%

:usage
echo Uso:
echo   %~nx0 "ruta\a\FattoPrizzerva.exe" [calidad]
echo.
echo Ejemplo:
echo   %~nx0 "D:\GuimGames\FattoPrizzervaLightning\Builds\ManualTelemetry\FattoPrizzerva.exe" PC
if /I not "%TELEMETRY_NO_PAUSE%"=="1" pause
exit /b 1
