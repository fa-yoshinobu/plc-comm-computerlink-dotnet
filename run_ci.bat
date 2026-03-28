@echo off
setlocal
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Build, Test, Format, and Publish
echo ===================================================

echo [1/4] Building...
dotnet build PlcComm.Toyopuc.sln
if %errorlevel% neq 0 (echo [ERROR] Build failed. & exit /b %errorlevel%)

echo [2/4] Testing...
dotnet test PlcComm.Toyopuc.sln --no-build
if %errorlevel% neq 0 (echo [ERROR] Tests failed. & exit /b %errorlevel%)

echo [3/4] Format check...
dotnet format PlcComm.Toyopuc.sln --verify-no-changes
if %errorlevel% neq 0 (echo [ERROR] Format violations found. & exit /b %errorlevel%)

echo [4/4] Publishing HighLevelSample...
dotnet publish examples\PlcComm.Toyopuc.HighLevelSample\PlcComm.Toyopuc.HighLevelSample.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false -o "%PUBLISH_DIR%\HighLevelSample"
if %errorlevel% neq 0 (echo [ERROR] Publish failed. & exit /b %errorlevel%)

echo ===================================================
echo [SUCCESS] CI passed.
echo ===================================================
endlocal
