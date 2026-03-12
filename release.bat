@echo off
setlocal EnableExtensions

set "ROOT_DIR=%~dp0"
pushd "%ROOT_DIR%" >nul || exit /b 1

set "SOLUTION=Toyopuc.sln"
set "PROJECT=src\Toyopuc\Toyopuc.csproj"
set "CONFIG=Release"
set "FRAMEWORK=net9.0"
set "ARTIFACT_ROOT=%ROOT_DIR%artifacts\release"
set "SKIP_TESTS=0"

:parse_args
if "%~1"=="" goto after_args
if /I "%~1"=="--skip-tests" (
    set "SKIP_TESTS=1"
    shift
    goto parse_args
)
if /I "%~1"=="--output" (
    if "%~2"=="" goto usage_error
    set "ARTIFACT_ROOT=%~f2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--help" goto usage
echo Unknown option: %~1
goto usage_error

:after_args
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "$xml = [xml](Get-Content 'src/Toyopuc/Toyopuc.csproj'); ($xml.Project.PropertyGroup.Version | Select-Object -First 1)"`) do set "VERSION=%%I"
if not defined VERSION set "VERSION=unknown"

set "RELEASE_DIR=%ARTIFACT_ROOT%\%VERSION%"
if not exist "%RELEASE_DIR%" mkdir "%RELEASE_DIR%" || goto fail
set "ZIP_STAGING_DIR=%RELEASE_DIR%\Toyopuc-%VERSION%"
set "DLL_ZIP=%RELEASE_DIR%\Toyopuc.Net.%VERSION%-dll.zip"
set "DEVICE_MONITOR_STAGE_DIR=%RELEASE_DIR%\Toyopuc.DeviceMonitor"
set "DEVICE_MONITOR_EXE=%RELEASE_DIR%\Toyopuc.DeviceMonitor.exe"
for %%F in (
    "%RELEASE_DIR%\README.md"
    "%RELEASE_DIR%\LICENSE"
    "%RELEASE_DIR%\Toyopuc.dll"
    "%RELEASE_DIR%\Toyopuc.xml"
    "%RELEASE_DIR%\Toyopuc.pdb"
    "%RELEASE_DIR%\Toyopuc.deps.json"
    "%RELEASE_DIR%\Toyopuc.runtimeconfig.json"
    "%RELEASE_DIR%\Toyopuc.Net.%VERSION%-dll.zip"
    "%RELEASE_DIR%\Toyopuc.Net.%VERSION%.nupkg"
    "%RELEASE_DIR%\Toyopuc.Net.%VERSION%.snupkg"
    "%RELEASE_DIR%\Toyopuc.DeviceMonitor.exe"
) do (
    if exist %%~F del /Q %%~F
)
if exist "%ZIP_STAGING_DIR%" rmdir /S /Q "%ZIP_STAGING_DIR%"
if exist "%DEVICE_MONITOR_STAGE_DIR%" rmdir /S /Q "%DEVICE_MONITOR_STAGE_DIR%"

echo [1/5] Restoring %SOLUTION%
dotnet restore "%SOLUTION%" || goto fail

echo [2/5] Building %SOLUTION% (%CONFIG%)
dotnet build "%SOLUTION%" -c %CONFIG% || goto fail

if "%SKIP_TESTS%"=="0" (
    echo [3/5] Running tests
    dotnet test "%SOLUTION%" -c %CONFIG% --no-build || goto fail
) else (
    echo [3/5] Skipping tests
)

echo [4/5] Packing %PROJECT%
dotnet pack "%PROJECT%" -c %CONFIG% -o "%RELEASE_DIR%" || goto fail

set "BUILD_DIR=%ROOT_DIR%src\Toyopuc\bin\%CONFIG%\%FRAMEWORK%"
mkdir "%ZIP_STAGING_DIR%" || goto fail
copy /Y "%BUILD_DIR%\Toyopuc.dll" "%ZIP_STAGING_DIR%\" >nul || goto fail
if exist "%BUILD_DIR%\Toyopuc.xml" copy /Y "%BUILD_DIR%\Toyopuc.xml" "%ZIP_STAGING_DIR%\" >nul
copy /Y "%ROOT_DIR%README.md" "%ZIP_STAGING_DIR%\" >nul || goto fail
copy /Y "%ROOT_DIR%LICENSE" "%ZIP_STAGING_DIR%\" >nul || goto fail
if exist "%DLL_ZIP%" del /Q "%DLL_ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%ZIP_STAGING_DIR%\\*' -DestinationPath '%DLL_ZIP%' -CompressionLevel Optimal" || goto fail
if exist "%ZIP_STAGING_DIR%" rmdir /S /Q "%ZIP_STAGING_DIR%"

echo [5/5] Publishing DeviceMonitor single-file executable
call "%ROOT_DIR%device_monitor_release.bat" --skip-build --output "%DEVICE_MONITOR_STAGE_DIR%" || goto fail
if not exist "%DEVICE_MONITOR_STAGE_DIR%\Toyopuc.DeviceMonitor.exe" (
    echo DeviceMonitor executable not found.
    goto fail
)
copy /Y "%DEVICE_MONITOR_STAGE_DIR%\Toyopuc.DeviceMonitor.exe" "%DEVICE_MONITOR_EXE%" >nul || goto fail
if exist "%DEVICE_MONITOR_STAGE_DIR%" rmdir /S /Q "%DEVICE_MONITOR_STAGE_DIR%"

echo.
echo Release completed.
echo Output: %RELEASE_DIR%
dir /b "%RELEASE_DIR%"
popd >nul
exit /b 0

:usage
echo Usage: release.bat [--skip-tests] [--output PATH]
echo.
echo   --skip-tests   Skip dotnet test.
echo   --output PATH  Override output root. Default is artifacts\release.
popd >nul
exit /b 0

:usage_error
echo Usage: release.bat [--skip-tests] [--output PATH]
popd >nul
exit /b 2

:fail
echo.
echo Release failed.
popd >nul
exit /b 1
