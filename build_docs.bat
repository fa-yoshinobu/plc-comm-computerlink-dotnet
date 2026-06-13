@echo off
setlocal
set "DOCS_OUTPUT_DIR=%TEMP%\plc-docs\plc-comm-computerlink-dotnet"
set "TEMP_DOCFX_JSON=.tmp_docfx.json"
echo [DOCS] Building Toyopuc .NET Docs with DocFX...
echo [DOCS] Output: %DOCS_OUTPUT_DIR%

if exist "obj\api" rmdir /s /q "obj\api"
if exist "%DOCS_OUTPUT_DIR%" rmdir /s /q "%DOCS_OUTPUT_DIR%"

powershell -NoProfile -Command "$content = Get-Content 'docfx.json'; $content = $content -replace '\"dest\": \"_site\"', '\"dest\": \".\"'; Set-Content -Path '%TEMP_DOCFX_JSON%' -Value $content"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to prepare temporary DocFX configuration.
    exit /b 1
)

:: 1. Generate Metadata from source
docfx metadata %TEMP_DOCFX_JSON%

:: 2. Build the site
docfx build %TEMP_DOCFX_JSON% --output "%DOCS_OUTPUT_DIR%"

if %errorlevel% neq 0 (
    del /q %TEMP_DOCFX_JSON% >nul 2>&1
    echo [ERROR] DocFX build failed.
    exit /b 1
)

if exist "%DOCS_OUTPUT_DIR%\README.html" copy /y "%DOCS_OUTPUT_DIR%\README.html" "%DOCS_OUTPUT_DIR%\index.html" >nul
echo [SUCCESS] Documentation built to %DOCS_OUTPUT_DIR%/
del /q %TEMP_DOCFX_JSON% >nul 2>&1
endlocal
