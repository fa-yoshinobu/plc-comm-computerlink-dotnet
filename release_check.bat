@echo off
setlocal

echo ===================================================
echo [RELEASE] Toyopuc .NET release check
echo ===================================================

echo [1/7] Checking registry version...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_registry_duplicate.ps1 -Registry nuget -Package PlcComm.Toyopuc -VersionSource csproj -ManifestPath Directory.Build.props
if %errorlevel% neq 0 (
    echo [ERROR] Release version check failed.
    exit /b %errorlevel%
)

echo [2/7] Checking canonical ComputerLink profile fixtures...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\update_computerlink_profile_jsons.ps1 -FailIfChanged
if %errorlevel% neq 0 (
    echo [ERROR] Canonical ComputerLink profile JSON check failed.
    exit /b %errorlevel%
)

echo [3/7] Checking GitHub source archive build and tests...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_source_archive.ps1
if %errorlevel% neq 0 (
    echo [ERROR] Source archive build/test check failed.
    exit /b %errorlevel%
)

echo [4/7] Running CI...
call run_ci.bat
if %errorlevel% neq 0 (
    echo [ERROR] CI failed.
    exit /b %errorlevel%
)

echo [5/7] Checking exact three-TFM API classifications...
dotnet build tools\api-diff\PlcComm.Toyopuc.ApiSurfaceExporter\PlcComm.Toyopuc.ApiSurfaceExporter.csproj -c Debug
if %errorlevel% neq 0 (
    echo [ERROR] TFM-matched API exporter build failed.
    exit /b %errorlevel%
)
pwsh -NoProfile -File scripts\check_documented_api_diff.ps1 -CandidateAssemblyRoot src\Toyopuc\bin\Debug -Configuration Debug -ReviewOutput build\documented-api-diff-review.json
if %errorlevel% neq 0 (
    echo [ERROR] Exact API classification check failed.
    exit /b %errorlevel%
)

echo [6/7] Checking API next-major release disposition...
pwsh -NoProfile -File scripts\check_documented_api_diff.ps1 -Mode ReleasePolicy -ReleaseVersionFile Directory.Build.props
if %errorlevel% neq 0 (
    echo [ERROR] API release-major policy failed.
    exit /b %errorlevel%
)

echo [7/7] Packing NuGet package...
dotnet pack src\Toyopuc\PlcComm.Toyopuc.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Pack failed.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] Release check passed.
echo ===================================================
endlocal
