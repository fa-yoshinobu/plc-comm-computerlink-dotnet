@echo off
setlocal EnableExtensions

set "ROOT_DIR=%~dp0"
pushd "%ROOT_DIR%" >nul || exit /b 1

set "PROJECT=examples\Toyopuc.SoakMonitor\Toyopuc.SoakMonitor.csproj"
set "HOST=192.168.250.101"
set "PORT=1025"
set "PROTOCOL=tcp"
set "PROFILE=Nano 10GX:Compatible mode"
set "HOPS=P1-L2:N4,P1-L2:N6,P1-L2:N2"
set "DEVICES=P1-D0000,P1-M0000,U08000,EB00000,FR000000"
set "INTERVAL=5s"
set "DURATION=30m"
set "RETRIES=3"
set "RECONNECT_DELAY=5s"
set "MAX_CONSECUTIVE_FAILURES=5"
set "SUCCESS_LOG_INTERVAL=12"
set "LOG_DIR=%ROOT_DIR%logs"
set "PREFIX=soak_10gx_core"
set "SKIP_BUILD=0"
set "EXTRA_ARGS="

:parse_args
if "%~1"=="" goto after_args
if /I "%~1"=="--skip-build" (
    set "SKIP_BUILD=1"
    shift
    goto parse_args
)
if /I "%~1"=="--duration" (
    if "%~2"=="" goto usage_error
    set "DURATION=%~2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--interval" (
    if "%~2"=="" goto usage_error
    set "INTERVAL=%~2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--log-dir" (
    if "%~2"=="" goto usage_error
    set "LOG_DIR=%~f2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--prefix" (
    if "%~2"=="" goto usage_error
    set "PREFIX=%~2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--2h" (
    set "DURATION=2h"
    shift
    goto parse_args
)
if /I "%~1"=="--help" goto usage
set "EXTRA_ARGS=%EXTRA_ARGS% %1"
shift
goto parse_args

:after_args
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>nul

set "LOG_PATH=%LOG_DIR%\%PREFIX%.log"
set "CSV_PATH=%LOG_DIR%\%PREFIX%.csv"
set "JSON_PATH=%LOG_DIR%\%PREFIX%.json"

if "%SKIP_BUILD%"=="0" (
    echo [1/2] Building %PROJECT%
    dotnet build "%PROJECT%" -c Debug || goto fail
) else (
    echo [1/2] Skipping build
)

echo [2/2] Starting 10GX core soak monitor
echo Log   : %LOG_PATH%
echo CSV   : %CSV_PATH%
echo JSON  : %JSON_PATH%
echo.

set "RUN_ARGS=run --project %PROJECT%"
if "%SKIP_BUILD%"=="1" (
    set "RUN_ARGS=%RUN_ARGS% --no-build"
)

dotnet %RUN_ARGS% -- ^
  --host "%HOST%" ^
  --port "%PORT%" ^
  --protocol "%PROTOCOL%" ^
  --profile "%PROFILE%" ^
  --hops "%HOPS%" ^
  --devices "%DEVICES%" ^
  --interval "%INTERVAL%" ^
  --duration "%DURATION%" ^
  --retries "%RETRIES%" ^
  --reconnect-delay "%RECONNECT_DELAY%" ^
  --max-consecutive-failures "%MAX_CONSECUTIVE_FAILURES%" ^
  --success-log-interval "%SUCCESS_LOG_INTERVAL%" ^
  --log "%LOG_PATH%" ^
  --poll-csv "%CSV_PATH%" ^
  --summary-json "%JSON_PATH%" ^
  %EXTRA_ARGS%
if errorlevel 1 goto fail

echo.
echo Soak monitor completed.
popd >nul
exit /b 0

:usage
echo Usage: soak_monitor_10gx_core.bat [--skip-build] [--duration TIME] [--interval TIME] [--log-dir PATH] [--prefix NAME] [--2h] [extra soak args]
echo.
echo Default target:
echo   host    = 192.168.250.101
echo   profile = Nano 10GX:Compatible mode
echo   hops    = P1-L2:N4,P1-L2:N6,P1-L2:N2
echo   devices = P1-D0000,P1-M0000,U08000,EB00000,FR000000
echo.
echo Examples:
echo   soak_monitor_10gx_core.bat
echo   soak_monitor_10gx_core.bat --2h
echo   soak_monitor_10gx_core.bat --duration 45m --interval 2s --prefix soak_10gx_fast
echo   soak_monitor_10gx_core.bat --skip-build --verbose
popd >nul
exit /b 0

:usage_error
echo Usage: soak_monitor_10gx_core.bat [--skip-build] [--duration TIME] [--interval TIME] [--log-dir PATH] [--prefix NAME] [--2h] [extra soak args]
popd >nul
exit /b 2

:fail
echo.
echo Soak monitor run failed.
popd >nul
exit /b 1
