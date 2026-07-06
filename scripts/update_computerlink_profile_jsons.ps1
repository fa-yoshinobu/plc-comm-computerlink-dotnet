param(
    [string]$Ref = $env:COMPUTERLINK_PROFILES_REF,
    [string]$SourceRoot = $env:COMPUTERLINK_PROFILES_SOURCE_ROOT,
    [switch]$FailIfChanged
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Ref)) {
    $Ref = "v1.0.1"
}

$RawBase = "https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-computerlink-profiles/$Ref"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Changed = New-Object System.Collections.Generic.List[string]
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Get-CanonicalJson {
    param([string]$Path)
    if (-not [string]::IsNullOrWhiteSpace($SourceRoot)) {
        $sourcePath = Join-Path $SourceRoot $Path
        Write-Host "[profiles] reading $sourcePath"
        $content = [System.IO.File]::ReadAllText($sourcePath)
    } else {
        $uri = "$RawBase/$Path"
        Write-Host "[profiles] downloading $uri"
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri
        $content = [string]$response.Content
    }
    $null = $content | ConvertFrom-Json
    return $content
}

function Write-IfChanged {
    param(
        [string]$Destination,
        [string]$Content
    )
    $fullPath = Join-Path $RepoRoot $Destination
    $parent = Split-Path -Parent $fullPath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
    $normalizedContent = $Content.Replace("`r`n", "`n")
    $current = $null
    if (Test-Path -LiteralPath $fullPath) {
        $current = [System.IO.File]::ReadAllText($fullPath).Replace("`r`n", "`n")
    }
    if ($current -ne $normalizedContent) {
        [System.IO.File]::WriteAllText($fullPath, $normalizedContent, $Utf8NoBom)
        $Changed.Add($Destination) | Out-Null
        Write-Host "[profiles] updated $Destination"
    } else {
        Write-Host "[profiles] unchanged $Destination"
    }
}

$profiles = Get-CanonicalJson "capability/toyopuc_profiles.json"

Write-IfChanged "tests/PlcComm.Toyopuc.Tests/fixtures/toyopuc_profiles.json" $profiles

if ($Changed.Count -gt 0) {
    Write-Host "[profiles] changed files:"
    foreach ($path in $Changed) {
        Write-Host "  $path"
    }
    if ($FailIfChanged) {
        Write-Error "Canonical ComputerLink profile JSON changed. Commit the updated files before release."
    }
}
