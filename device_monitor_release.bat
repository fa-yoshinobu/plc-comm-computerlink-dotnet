@echo off
setlocal EnableExtensions

set "ROOT_DIR=%~dp0"
pushd "%ROOT_DIR%" >nul || exit /b 1

set "PROJECT=examples\Toyopuc.DeviceMonitor\Toyopuc.DeviceMonitor.csproj"
set "CONFIG=Release"
set "PUBLISH_PROFILE=win-x64-single-file"
set "OUTPUT_DIR=%ROOT_DIR%artifacts\publish\Toyopuc.DeviceMonitor"
set "SKIP_BUILD=0"

:parse_args
if "%~1"=="" goto after_args
if /I "%~1"=="--skip-build" (
    set "SKIP_BUILD=1"
    shift
    goto parse_args
)
if /I "%~1"=="--output" (
    if "%~2"=="" goto usage_error
    set "OUTPUT_DIR=%~f2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--help" goto usage
echo Unknown option: %~1
goto usage_error

:after_args
if "%SKIP_BUILD%"=="0" (
    echo [1/2] Building %PROJECT%
    dotnet build "%PROJECT%" -c %CONFIG% || goto fail
) else (
    echo [1/2] Skipping build
)

echo [2/2] Publishing single-file executable
dotnet publish "%PROJECT%" -c %CONFIG% -p:PublishProfile=%PUBLISH_PROFILE% -o "%OUTPUT_DIR%" || goto fail

echo.
echo DeviceMonitor publish completed.
echo Output: %OUTPUT_DIR%
dir /b "%OUTPUT_DIR%"
popd >nul
exit /b 0

:usage
echo Usage: device_monitor_release.bat [--skip-build] [--output PATH]
echo.
echo   --skip-build   Skip dotnet build.
echo   --output PATH  Override publish output. Default is artifacts\publish\Toyopuc.DeviceMonitor.
popd >nul
exit /b 0

:usage_error
echo Usage: device_monitor_release.bat [--skip-build] [--output PATH]
popd >nul
exit /b 2

:fail
echo.
echo DeviceMonitor publish failed.
popd >nul
exit /b 1
