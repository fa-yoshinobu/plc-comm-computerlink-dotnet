@echo off
setlocal
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Build, Test, Format, and Publish
echo ===================================================

echo [1/5] Building...
dotnet build PlcComm.Toyopuc.sln
if %errorlevel% neq 0 (echo [ERROR] Build failed. & exit /b %errorlevel%)

echo [2/5] Validating API reference...
python scripts\test_generate_api_reference.py
if %errorlevel% neq 0 (echo [ERROR] API reference generator tests failed. & exit /b %errorlevel%)
python scripts\test_documentation_examples.py
if %errorlevel% neq 0 (echo [ERROR] Documentation example tests failed. & exit /b %errorlevel%)
python scripts\generate_api_reference.py --assembly src\Toyopuc\bin\Debug\net8.0\PlcComm.Toyopuc.dll --xml src\Toyopuc\bin\Debug\net8.0\PlcComm.Toyopuc.xml --output docsrc\user\API_REFERENCE.md --title "TOYOPUC Computerlink .NET API Reference" --package PlcComm.Toyopuc --check
if %errorlevel% neq 0 (echo [ERROR] API reference is out of date. & exit /b %errorlevel%)

echo [3/5] Testing...
dotnet test PlcComm.Toyopuc.sln --no-build
if %errorlevel% neq 0 (echo [ERROR] Tests failed. & exit /b %errorlevel%)

echo [4/5] Format check...
dotnet format PlcComm.Toyopuc.sln --verify-no-changes
if %errorlevel% neq 0 (echo [ERROR] Format violations found. & exit /b %errorlevel%)

echo [5/5] Publishing HighLevelSample...
dotnet publish examples\PlcComm.Toyopuc.HighLevelSample\PlcComm.Toyopuc.HighLevelSample.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false -o "%PUBLISH_DIR%\HighLevelSample"
if %errorlevel% neq 0 (echo [ERROR] Publish failed. & exit /b %errorlevel%)

echo ===================================================
echo [SUCCESS] CI passed.
echo ===================================================
endlocal
