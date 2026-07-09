@echo off
setlocal

echo ===================================================
echo [RELEASE] Toyopuc .NET release check
echo ===================================================

echo [1/4] Checking registry version...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_registry_duplicate.ps1 -Registry nuget -Package PlcComm.Toyopuc -VersionSource csproj -ManifestPath Directory.Build.props
if %errorlevel% neq 0 (
    echo [ERROR] Release version check failed.
    exit /b %errorlevel%
)

echo [2/4] Checking canonical ComputerLink profile fixtures...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\update_computerlink_profile_jsons.ps1 -FailIfChanged
if %errorlevel% neq 0 (
    echo [ERROR] Canonical ComputerLink profile JSON check failed.
    exit /b %errorlevel%
)

echo [3/4] Running CI...
call run_ci.bat
if %errorlevel% neq 0 (
    echo [ERROR] CI failed.
    exit /b %errorlevel%
)

echo [4/4] Packing NuGet package...
dotnet pack src\Toyopuc\PlcComm.Toyopuc.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Pack failed.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] Release check passed.
echo ===================================================
endlocal
