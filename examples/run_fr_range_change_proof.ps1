param(
    [string]$TargetHost = '192.168.250.100',
    [int]$Port = 1025,
    [ValidateSet('tcp', 'udp')]
    [string]$Protocol = 'tcp',
    [string]$Profile = 'Nano 10GX:Compatible mode',
    [string]$Hops = 'P1-L2:N4,P1-L2:N6,P1-L2:N2',
    [string]$StartDevice = 'FR000000',
    [string]$Count = '0x200000',
    [ValidateSet('ramp16', 'xor16', 'fill16')]
    [string]$Pattern = 'ramp16',
    [string]$Seed = '0x55AB',
    [string]$OutputPrefix = 'logs\relay_fr_change_proof'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

function Ensure-ParentDirectory {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
    $directory = Split-Path $fullPath -Parent
    if ($directory) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    return $fullPath
}

function Invoke-SmokeTest {
    param([string[]]$ExtraArgs)

    $arguments = @(
        'run',
        '--project', 'examples/PlcComm.Toyopuc.SmokeTest',
        '--no-build',
        '--',
        '--host', $TargetHost,
        '--port', $Port.ToString(),
        '--protocol', $Protocol,
        '--profile', $Profile,
        '--fr-range-device', $StartDevice,
        '--fr-range-count', $Count
    )

    if (-not [string]::IsNullOrWhiteSpace($Hops)) {
        $arguments += @('--hops', $Hops)
    }

    $arguments += $ExtraArgs

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "SmokeTest failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    $beforeCsv = Ensure-ParentDirectory "$OutputPrefix.before.csv"
    $afterCsv = Ensure-ParentDirectory "$OutputPrefix.after.csv"
    $diffCsv = Ensure-ParentDirectory "$OutputPrefix.diff.csv"
    $beforeLog = Ensure-ParentDirectory "$OutputPrefix.before.log"
    $writeLog = Ensure-ParentDirectory "$OutputPrefix.write.log"
    $afterLog = Ensure-ParentDirectory "$OutputPrefix.after.log"

    foreach ($path in @($beforeCsv, $afterCsv, $diffCsv, $beforeLog, $writeLog, $afterLog)) {
        if (Test-Path $path) {
            Remove-Item $path -Force
        }
    }

    Invoke-SmokeTest @('--fr-range-dump-csv', $beforeCsv, '--log', $beforeLog)
    Invoke-SmokeTest @('--fr-range-pattern', $Pattern, '--fr-range-seed', $Seed, '--fr-range-verify', '--log', $writeLog)
    Invoke-SmokeTest @('--fr-range-dump-csv', $afterCsv, '--log', $afterLog)

    $beforeRows = Import-Csv $beforeCsv
    $afterRows = Import-Csv $afterCsv

    if ($beforeRows.Count -ne $afterRows.Count) {
        throw "CSV row count mismatch: before=$($beforeRows.Count) after=$($afterRows.Count)"
    }

    $diffRows = for ($index = 0; $index -lt $beforeRows.Count; $index++) {
        $before = $beforeRows[$index]
        $after = $afterRows[$index]

        if ($before.device -ne $after.device) {
            throw "CSV device mismatch at row ${index}: before=$($before.device) after=$($after.device)"
        }

        [PSCustomObject]@{
            index_hex  = $before.index_hex
            device     = $before.device
            before_hex = $before.value_hex
            after_hex  = $after.value_hex
            changed    = ($before.value_hex -ne $after.value_hex)
        }
    }

    $diffRows | Export-Csv -Path $diffCsv -NoTypeInformation -Encoding utf8

    $changedCount = ($diffRows | Where-Object { $_.changed }).Count
    $sameCount = $diffRows.Count - $changedCount

    Write-Host "before_csv : $beforeCsv"
    Write-Host "after_csv  : $afterCsv"
    Write-Host "diff_csv   : $diffCsv"
    Write-Host "write_log  : $writeLog"
    Write-Host "summary    : total=$($diffRows.Count) changed=$changedCount same=$sameCount pattern=$Pattern seed=$Seed"
}
finally {
    Pop-Location
}
