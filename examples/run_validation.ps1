param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("plus", "relay-10gx", "pc10g-direct")]
    [string]$Target,

    [string]$PlcHost = "192.168.250.101",
    [int]$Port = 1025,
    [ValidateSet("tcp", "udp")]
    [string]$Protocol = "tcp",
    [string]$Hops = "P1-L2:N4,P1-L2:N6,P1-L2:N2",
    [string]$LogDir = "logs",
    [switch]$FullSuite,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SmokeTestProject = Join-Path $RepoRoot "examples\Toyopuc.SmokeTest"
$ResolvedLogDir = Join-Path $RepoRoot $LogDir

function Invoke-SmokeStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string[]]$SmokeArgs
    )

    $command = @("run", "--project", $SmokeTestProject, "--") + $SmokeArgs
    Write-Host ""
    Write-Host "[$Label]"
    Write-Host ("dotnet " + ($command -join " "))

    if ($DryRun) {
        return
    }

    & dotnet @command
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Label"
    }
}

function Invoke-SmokeExpectedFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string[]]$SmokeArgs,
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessageContains
    )

    $command = @("run", "--project", $SmokeTestProject, "--") + $SmokeArgs
    Write-Host ""
    Write-Host "[$Label]"
    Write-Host ("dotnet " + ($command -join " "))

    if ($DryRun) {
        return
    }

    if (Test-Path $LogPath) {
        Remove-Item $LogPath -Force
    }

    & dotnet @command
    if ($LASTEXITCODE -eq 0) {
        throw "Step unexpectedly succeeded: $Label"
    }

    if (-not (Test-Path $LogPath)) {
        throw "Expected failure log not found: $LogPath"
    }

    if (-not (Select-String -Path $LogPath -Pattern ([regex]::Escape($ExpectedMessageContains)) -Quiet)) {
        throw "Expected error text not found in log for ${Label}: $ExpectedMessageContains"
    }
}

function Invoke-ProfileWriteRestore {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$Profile,
        [Parameter(Mandatory = $true)]
        [string]$Device,
        [Parameter(Mandatory = $true)]
        [string]$LogName,
        [string]$WriteValue,
        [switch]$ToggleBitWrite,
        [int]$Count = 1,
        [ValidateSet("fill", "ramp")]
        [string]$WritePattern = "fill",
        [string]$RelayHops
    )

    if (-not $ToggleBitWrite -and [string]::IsNullOrWhiteSpace($WriteValue)) {
        throw "WriteValue is required unless ToggleBitWrite is used."
    }

    $smokeArgs = @(
        "--host", $PlcHost,
        "--port", $Port,
        "--protocol", $Protocol,
        "--profile", $Profile,
        "--device", $Device
    )

    if (-not [string]::IsNullOrWhiteSpace($RelayHops)) {
        $smokeArgs += @("--hops", $RelayHops)
    }

    if ($Count -gt 1) {
        $smokeArgs += @("--count", ("0x{0:X}" -f $Count))
    }

    if ($ToggleBitWrite) {
        $smokeArgs += "--toggle-bit-write"
    }
    else {
        $smokeArgs += @("--write-value", $WriteValue)
    }

    if ($Count -gt 1 -and $WritePattern -ne "fill") {
        $smokeArgs += @("--write-pattern", $WritePattern)
    }

    $smokeArgs += @(
        "--restore-after-write",
        "--verbose",
        "--log", (Join-Path $ResolvedLogDir $LogName)
    )

    Invoke-SmokeStep -Label $Label -SmokeArgs $smokeArgs
}

function Invoke-ProfileManyWriteRestore {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$Profile,
        [Parameter(Mandatory = $true)]
        [string[]]$Devices,
        [Parameter(Mandatory = $true)]
        [string[]]$WriteValues,
        [Parameter(Mandatory = $true)]
        [string]$LogName,
        [string]$RelayHops
    )

    if ($Devices.Count -ne $WriteValues.Count) {
        throw "Devices and WriteValues must have the same number of items."
    }

    $smokeArgs = @(
        "--host", $PlcHost,
        "--port", $Port,
        "--protocol", $Protocol,
        "--profile", $Profile
    )

    if (-not [string]::IsNullOrWhiteSpace($RelayHops)) {
        $smokeArgs += @("--hops", $RelayHops)
    }

    $smokeArgs += @(
        "--devices", ($Devices -join ","),
        "--write-values", ($WriteValues -join ","),
        "--restore-after-write",
        "--verbose",
        "--log", (Join-Path $ResolvedLogDir $LogName)
    )

    Invoke-SmokeStep -Label $Label -SmokeArgs $smokeArgs
}

function Invoke-RelayWriteRestore {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$Device,
        [Parameter(Mandatory = $true)]
        [string]$LogName,
        [string]$WriteValue,
        [switch]$ToggleBitWrite,
        [int]$Count = 1,
        [ValidateSet("fill", "ramp")]
        [string]$WritePattern = "fill"
    )

    Invoke-ProfileWriteRestore `
        -Label $Label `
        -Profile "Nano 10GX:Compatible mode" `
        -RelayHops $Hops `
        -Device $Device `
        -LogName $LogName `
        -WriteValue $WriteValue `
        -ToggleBitWrite:$ToggleBitWrite `
        -Count $Count `
        -WritePattern $WritePattern
}

function Invoke-RelayManyWriteRestore {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string[]]$Devices,
        [Parameter(Mandatory = $true)]
        [string[]]$WriteValues,
        [Parameter(Mandatory = $true)]
        [string]$LogName
    )

    Invoke-ProfileManyWriteRestore `
        -Label $Label `
        -Profile "Nano 10GX:Compatible mode" `
        -RelayHops $Hops `
        -Devices $Devices `
        -WriteValues $WriteValues `
        -LogName $LogName
}

New-Item -ItemType Directory -Force -Path $ResolvedLogDir | Out-Null
Push-Location $RepoRoot

try {
    if ($Target -eq "plus") {
        $suiteName = if ($FullSuite) { "full:TOYOPUC-Plus:Plus Extended mode" } else { "TOYOPUC-Plus:Plus Extended mode" }
        $suiteLog = if ($FullSuite) { "plus_suite_full.log" } else { "plus_suite.log" }

        Invoke-SmokeStep -Label "Plus suite" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "TOYOPUC-Plus:Plus Extended mode",
            "--suite", $suiteName,
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir $suiteLog)
        )

        Invoke-SmokeStep -Label "Plus P1-D0000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "TOYOPUC-Plus:Plus Extended mode",
            "--device", "P1-D0000",
            "--write-value", "0x1234",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "plus_word_restore.log")
        )

        Invoke-SmokeStep -Label "Plus P1-M0000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "TOYOPUC-Plus:Plus Extended mode",
            "--device", "P1-M0000",
            "--toggle-bit-write",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "plus_bit_restore.log")
        )
    }

    if ($Target -eq "pc10g-direct") {
        $profile = "PC10G:PC10 mode"
        $suiteName = if ($FullSuite) { "full:${profile}" } else { $profile }
        $suiteLog = if ($FullSuite) { "pc10g_pc10mode_full.log" } else { "pc10g_pc10mode_suite.log" }

        Invoke-SmokeStep -Label "PC10G direct suite" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--suite", $suiteName,
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir $suiteLog)
        )

        Invoke-ProfileWriteRestore -Label "PC10G P1-D0000 write/restore" -Profile $profile -Device "P1-D0000" -WriteValue "0x1357" -LogName "pc10g_word_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-D2FF0 multi write/restore" -Profile $profile -Device "P1-D2FF0" -WriteValue "0x4100" -Count 0x10 -WritePattern "ramp" -LogName "pc10g_d2ff0_multi_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-M0000 write/restore" -Profile $profile -Device "P1-M0000" -ToggleBitWrite -LogName "pc10g_bit_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-M1000 write/restore" -Profile $profile -Device "P1-M1000" -ToggleBitWrite -LogName "pc10g_m1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G B0000 write/restore" -Profile $profile -Device "B0000" -WriteValue "0x2468" -LogName "pc10g_b0000_restore.log"

        Invoke-ProfileWriteRestore -Label "PC10G P1-P1000 write/restore" -Profile $profile -Device "P1-P1000" -ToggleBitWrite -LogName "pc10g_p1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-V1000 write/restore" -Profile $profile -Device "P1-V1000" -ToggleBitWrite -LogName "pc10g_v1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-T1000 write/restore" -Profile $profile -Device "P1-T1000" -ToggleBitWrite -LogName "pc10g_t1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-C1000 write/restore" -Profile $profile -Device "P1-C1000" -ToggleBitWrite -LogName "pc10g_c1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-L1000 write/restore" -Profile $profile -Device "P1-L1000" -ToggleBitWrite -LogName "pc10g_l1000_restore.log"

        Invoke-ProfileWriteRestore -Label "PC10G P1-S1000 write/restore" -Profile $profile -Device "P1-S1000" -WriteValue "0x4567" -LogName "pc10g_s1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-N1000 write/restore" -Profile $profile -Device "P1-N1000" -WriteValue "0x5678" -LogName "pc10g_n1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-R0000 write/restore" -Profile $profile -Device "P1-R0000" -WriteValue "0x6789" -LogName "pc10g_r0000_restore.log"

        Invoke-ProfileWriteRestore -Label "PC10G U00000 write/restore" -Profile $profile -Device "U00000" -WriteValue "0x1111" -LogName "pc10g_u00000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G U07FFF write/restore" -Profile $profile -Device "U07FFF" -WriteValue "0x2222" -LogName "pc10g_u07fff_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G U08000 write/restore" -Profile $profile -Device "U08000" -WriteValue "0x3333" -LogName "pc10g_u08000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G U1FFFF write/restore" -Profile $profile -Device "U1FFFF" -WriteValue "0x4444" -LogName "pc10g_u1ffff_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G EB00000 write/restore" -Profile $profile -Device "EB00000" -WriteValue "0x5555" -LogName "pc10g_eb00000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G EB3FFFF write/restore" -Profile $profile -Device "EB3FFFF" -WriteValue "0x6666" -LogName "pc10g_eb3ffff_restore.log"

        Invoke-SmokeStep -Label "PC10G FR write/commit/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--fr-device", "FR000000",
            "--fr-write-value", "0x55AB",
            "--fr-commit",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_fr_commit_restore.log")
        )

        Invoke-ProfileWriteRestore -Label "PC10G P1-D0000L write/restore" -Profile $profile -Device "P1-D0000L" -WriteValue "0x12" -LogName "pc10g_d0000l_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-D0000H write/restore" -Profile $profile -Device "P1-D0000H" -WriteValue "0x34" -LogName "pc10g_d0000h_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P1-M000W write/restore" -Profile $profile -Device "P1-M000W" -WriteValue "0xA55A" -LogName "pc10g_m0000w_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G EP000W write/restore" -Profile $profile -Device "EP000W" -WriteValue "0x1001" -LogName "pc10g_ep0000w_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G ET000W write/restore" -Profile $profile -Device "ET000W" -WriteValue "0x1002" -LogName "pc10g_et0000w_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G EX000W write/restore" -Profile $profile -Device "EX000W" -WriteValue "0x1003" -LogName "pc10g_ex0000w_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G GM000W write/restore" -Profile $profile -Device "GM000W" -WriteValue "0x1004" -LogName "pc10g_gm0000w_restore.log"

        Invoke-ProfileWriteRestore -Label "PC10G P2-D0000 write/restore" -Profile $profile -Device "P2-D0000" -WriteValue "0x3579" -LogName "pc10g_p2_d0000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P3-D0000 write/restore" -Profile $profile -Device "P3-D0000" -WriteValue "0x468A" -LogName "pc10g_p3_d0000_restore.log"

        Invoke-ProfileWriteRestore -Label "PC10G P2-V1000 write/restore" -Profile $profile -Device "P2-V1000" -ToggleBitWrite -LogName "pc10g_p2_v1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P3-V1000 write/restore" -Profile $profile -Device "P3-V1000" -ToggleBitWrite -LogName "pc10g_p3_v1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P2-M1000 write/restore" -Profile $profile -Device "P2-M1000" -ToggleBitWrite -LogName "pc10g_p2_m1000_restore.log"
        Invoke-ProfileWriteRestore -Label "PC10G P3-M1000 write/restore" -Profile $profile -Device "P3-M1000" -ToggleBitWrite -LogName "pc10g_p3_m1000_restore.log"

        Invoke-ProfileManyWriteRestore -Label "PC10G mixed many-word write/restore" -Profile $profile -Devices @("P1-D0000", "U08000", "EB00000", "P2-D0002") -WriteValues @("0x7100", "0x7101", "0x7102", "0x7103") -LogName "pc10g_many_words_restore.log"
        Invoke-ProfileManyWriteRestore -Label "PC10G boundary many-word write/restore" -Profile $profile -Devices @("P1-D2FFF", "U07FFF", "U08000", "EB3FFFF", "P2-D2FFE") -WriteValues @("0x7200", "0x7201", "0x7202", "0x7203", "0x7204") -LogName "pc10g_many_boundary_restore.log"
        Invoke-ProfileManyWriteRestore -Label "PC10G prefixed many-word write/restore" -Profile $profile -Devices @("P1-S1000", "P2-S1000", "P3-S1000", "P1-N1000") -WriteValues @("0x7300", "0x7301", "0x7302", "0x7303") -LogName "pc10g_prefixed_words_restore.log"

        Invoke-SmokeExpectedFailure -Label "PC10G M1800 negative" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--device", "M1800",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_m1800_negative.log")
        ) -LogPath (Join-Path $ResolvedLogDir "pc10g_m1800_negative.log") -ExpectedMessageContains "Area M is not available for direct access in profile '$profile': M1800"

        Invoke-SmokeExpectedFailure -Label "PC10G P1-M1800 negative" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--device", "P1-M1800",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_p1_m1800_negative.log")
        ) -LogPath (Join-Path $ResolvedLogDir "pc10g_p1_m1800_negative.log") -ExpectedMessageContains "Address out of range for profile '$profile': P1-M1800"

        Invoke-SmokeExpectedFailure -Label "PC10G U20000 negative" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--device", "U20000",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_u20000_negative.log")
        ) -LogPath (Join-Path $ResolvedLogDir "pc10g_u20000_negative.log") -ExpectedMessageContains "Address out of range for profile '$profile': U20000"

        Invoke-SmokeExpectedFailure -Label "PC10G EB40000 negative" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--device", "EB40000",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_eb40000_negative.log")
        ) -LogPath (Join-Path $ResolvedLogDir "pc10g_eb40000_negative.log") -ExpectedMessageContains "Address out of range for profile '$profile': EB40000"

        Invoke-SmokeExpectedFailure -Label "PC10G P1-D3000 negative" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--device", "P1-D3000",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_p1_d3000_negative.log")
        ) -LogPath (Join-Path $ResolvedLogDir "pc10g_p1_d3000_negative.log") -ExpectedMessageContains "Address out of range for profile '$profile': P1-D3000"

        Invoke-SmokeExpectedFailure -Label "PC10G FR200000 negative" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", $profile,
            "--fr-device", "FR200000",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "pc10g_fr200000_negative.log")
        ) -LogPath (Join-Path $ResolvedLogDir "pc10g_fr200000_negative.log") -ExpectedMessageContains "Address out of range for profile '$profile': FR200000"
    }

    if ($Target -eq "relay-10gx") {
        $suiteName = if ($FullSuite) { "full:Nano 10GX:Compatible mode" } else { "Nano 10GX:Compatible mode" }
        $suiteLog = if ($FullSuite) { "relay_suite_10gx_full.log" } else { "relay_suite_10gx.log" }

        Invoke-SmokeStep -Label "Relay Nano10GX suite" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--suite", $suiteName,
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir $suiteLog)
        )

        Invoke-SmokeStep -Label "Relay P1-D0000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D0000",
            "--write-value", "0x1357",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_word_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-D2FF0 multi write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D2FF0",
            "--count", "0x10",
            "--write-value", "0x4100",
            "--write-pattern", "ramp",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_d2ff0_multi_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-M0000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-M0000",
            "--toggle-bit-write",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_bit_restore.log")
        )

        Invoke-SmokeStep -Label "Relay FR write/commit/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--fr-device", "FR000000",
            "--fr-write-value", "0x55AB",
            "--fr-commit",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_fr_commit_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-D2FFF write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D2FFF",
            "--write-value", "0x1357",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_p1_d2fff_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-D2FF0 multi write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D2FF0",
            "--count", "0x10",
            "--write-value", "0x4200",
            "--write-pattern", "ramp",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_p1_d2ff0_multi_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-D0000L write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D0000L",
            "--write-value", "0x12",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_d0000l_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-D00F8L multi write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D00F8L",
            "--count", "0x20",
            "--write-value", "0x20",
            "--write-pattern", "ramp",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_d00f8l_multi_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-D0000H write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D0000H",
            "--write-value", "0x34",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_d0000h_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-M000W write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-M000W",
            "--write-value", "0xA55A",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_m0000w_restore.log")
        )

        Invoke-SmokeStep -Label "Relay P1-M07FW multi write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-M07FW",
            "--count", "0x10",
            "--write-value", "0x4300",
            "--write-pattern", "ramp",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_m07f0w_multi_restore.log")
        )

        Invoke-SmokeStep -Label "Relay U00000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "U00000",
            "--write-value", "0x1111",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_u00000_restore.log")
        )

        Invoke-SmokeStep -Label "Relay U07FFF write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "U07FFF",
            "--write-value", "0x2222",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_u07fff_restore.log")
        )

        Invoke-SmokeStep -Label "Relay U07FF0 multi write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "U07FF0",
            "--count", "0x20",
            "--write-value", "0x4400",
            "--write-pattern", "ramp",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_u07ff0_multi_restore.log")
        )

        Invoke-SmokeStep -Label "Relay U08000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "U08000",
            "--write-value", "0x3333",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_u08000_restore.log")
        )

        Invoke-SmokeStep -Label "Relay U1FFFF write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "U1FFFF",
            "--write-value", "0x4444",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_u1ffff_restore.log")
        )

        Invoke-SmokeStep -Label "Relay EB00000 write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "EB00000",
            "--write-value", "0x5555",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_eb00000_restore.log")
        )

        Invoke-SmokeStep -Label "Relay EB07FF0 multi write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "EB07FF0",
            "--count", "0x20",
            "--write-value", "0x4500",
            "--write-pattern", "ramp",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_eb07ff0_multi_restore.log")
        )

        Invoke-SmokeStep -Label "Relay EB3FFFF write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "EB3FFFF",
            "--write-value", "0x6666",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_eb3ffff_restore.log")
        )

        Invoke-SmokeStep -Label "Relay many words write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--devices", "P1-D0000,U08000,EB00000,P2-D0002",
            "--write-values", "0x5100,0x5101,0x5102,0x5103",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_many_words_restore.log")
        )

        Invoke-SmokeStep -Label "Relay many mixed write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--devices", "P1-M0000,P1-D0000,U08000,EB00000",
            "--write-values", "1,0x5201,0x5202,0x5203",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_many_mixed_restore.log")
        )

        Invoke-SmokeStep -Label "Relay many boundary write/restore" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--devices", "P1-D2FFF,U07FFF,U08000,EB3FFFF,P2-D2FFE",
            "--write-values", "0x5300,0x5301,0x5302,0x5303,0x5304",
            "--restore-after-write",
            "--verbose",
            "--log", (Join-Path $ResolvedLogDir "relay_many_boundary_restore.log")
        )

        $additionalRelayBitToggleCases = @(
            @{ Label = "Relay P1-P0000 write/restore"; Device = "P1-P0000"; Log = "relay_p0000_restore.log" },
            @{ Label = "Relay P1-P1000 write/restore"; Device = "P1-P1000"; Log = "relay_p1000_restore.log" },
            @{ Label = "Relay P1-K0000 write/restore"; Device = "P1-K0000"; Log = "relay_k0000_restore.log" },
            # V0000-V00FF is treated as a system area on this PLC; V1000+ was verified separately.
            @{ Label = "Relay P1-V1000 write/restore"; Device = "P1-V1000"; Log = "relay_v1000_restore.log" },
            @{ Label = "Relay P1-V17FF write/restore"; Device = "P1-V17FF"; Log = "relay_v17ff_restore.log" },
            @{ Label = "Relay P1-T0000 write/restore"; Device = "P1-T0000"; Log = "relay_t0000_restore.log" },
            @{ Label = "Relay P1-T1000 write/restore"; Device = "P1-T1000"; Log = "relay_t1000_restore.log" },
            @{ Label = "Relay P1-C0000 write/restore"; Device = "P1-C0000"; Log = "relay_c0000_restore.log" },
            @{ Label = "Relay P1-C1000 write/restore"; Device = "P1-C1000"; Log = "relay_c1000_restore.log" },
            @{ Label = "Relay P1-L0000 write/restore"; Device = "P1-L0000"; Log = "relay_l0000_restore.log" },
            @{ Label = "Relay P1-L1000 write/restore"; Device = "P1-L1000"; Log = "relay_l1000_restore.log" },
            @{ Label = "Relay P1-X0000 write/restore"; Device = "P1-X0000"; Log = "relay_x0000_restore.log" },
            @{ Label = "Relay P1-Y0000 write/restore"; Device = "P1-Y0000"; Log = "relay_y0000_restore.log" },
            @{ Label = "Relay P1-M1000 write/restore"; Device = "P1-M1000"; Log = "relay_m1000_restore.log" }
        )

        foreach ($case in $additionalRelayBitToggleCases) {
            Invoke-RelayWriteRestore -Label $case.Label -Device $case.Device -LogName $case.Log -ToggleBitWrite
        }

        Invoke-RelayManyWriteRestore -Label "Relay additional words write/restore" -Devices @(
            "P1-S0000",
            "P1-S1000",
            "P1-N0000",
            "P1-N1000",
            "P1-R0000",
            "ES00000",
            "EN00000",
            "H00000"
        ) -WriteValues @(
            "0x5600",
            "0x5601",
            "0x5602",
            "0x5603",
            "0x5604",
            "0x5605",
            "0x5606",
            "0x5607"
        ) -LogName "relay_additional_words_restore.log"

        # ET/EC, EX/EY, and GX/GY share packed-word bases on this target, so validate them separately.
        Invoke-RelayManyWriteRestore -Label "Relay ext packed-word write/restore" -Devices @(
            "EP000W",
            "EK000W",
            "EV000W",
            "EL000W",
            "EM000W"
        ) -WriteValues @(
            "0x5700",
            "0x5701",
            "0x5702",
            "0x5705",
            "0x5708"
        ) -LogName "relay_ext_packed_word_restore.log"

        Invoke-RelayWriteRestore -Label "Relay ET000W write/restore" -Device "ET000W" -WriteValue "0x5703" -LogName "relay_et0000w_restore.log"
        Invoke-RelayWriteRestore -Label "Relay EC000W write/restore" -Device "EC000W" -WriteValue "0x5704" -LogName "relay_ec0000w_restore.log"
        Invoke-RelayWriteRestore -Label "Relay EX000W write/restore" -Device "EX000W" -WriteValue "0x5706" -LogName "relay_ex0000w_restore.log"
        Invoke-RelayWriteRestore -Label "Relay EY000W write/restore" -Device "EY000W" -WriteValue "0x5707" -LogName "relay_ey0000w_restore.log"
        Invoke-RelayWriteRestore -Label "Relay GM000W write/restore" -Device "GM000W" -WriteValue "0x5710" -LogName "relay_gm0000w_restore.log"
        Invoke-RelayWriteRestore -Label "Relay GX000W write/restore" -Device "GX000W" -WriteValue "0x5711" -LogName "relay_gx0000w_restore.log"
        Invoke-RelayWriteRestore -Label "Relay GY000W write/restore" -Device "GY000W" -WriteValue "0x5712" -LogName "relay_gy0000w_restore.log"

        $relayProgramBitSpecs = @(
            @{ Suffix = "P0000"; LogStem = "p0000" },
            @{ Suffix = "P1000"; LogStem = "p1000" },
            @{ Suffix = "K0000"; LogStem = "k0000" },
            # V0000-V00FF is treated as a system area on this PLC; V1000+ was verified separately.
            @{ Suffix = "V1000"; LogStem = "v1000" },
            @{ Suffix = "V17FF"; LogStem = "v17ff" },
            @{ Suffix = "T0000"; LogStem = "t0000" },
            @{ Suffix = "T1000"; LogStem = "t1000" },
            @{ Suffix = "C0000"; LogStem = "c0000" },
            @{ Suffix = "C1000"; LogStem = "c1000" },
            @{ Suffix = "L0000"; LogStem = "l0000" },
            @{ Suffix = "L1000"; LogStem = "l1000" },
            @{ Suffix = "X0000"; LogStem = "x0000" },
            @{ Suffix = "Y0000"; LogStem = "y0000" },
            @{ Suffix = "M0000"; LogStem = "m0000" },
            @{ Suffix = "M1000"; LogStem = "m1000" }
        )

        foreach ($prefix in @("P2", "P3")) {
            foreach ($spec in $relayProgramBitSpecs) {
                $device = "{0}-{1}" -f $prefix, $spec.Suffix
                $label = "Relay {0} write/restore" -f $device
                Invoke-RelayWriteRestore `
                    -Label $label `
                    -Device $device `
                    -LogName ("relay_{0}_{1}_restore.log" -f $prefix.ToLowerInvariant(), $spec.LogStem) `
                    -ToggleBitWrite
            }
        }

        Invoke-RelayManyWriteRestore -Label "Relay prefixed words write/restore" -Devices @(
            "P1-S0000",
            "P1-S1000",
            "P1-N0000",
            "P1-N1000",
            "P1-R0000"
        ) -WriteValues @(
            "0x5800",
            "0x5801",
            "0x5802",
            "0x5803",
            "0x5804"
        ) -LogName "relay_prefixed_words_restore.log"

        $relayProgramWordCases = @(
            @{
                Prefix = "P2";
                Devices = @("D0000", "S0000", "S1000", "N0000", "N1000", "R0000");
                Values = @("0x6200", "0x6201", "0x6202", "0x6203", "0x6204", "0x6205");
                LogName = "relay_p2_prefixed_words_restore.log";
            },
            @{
                Prefix = "P3";
                Devices = @("D0000", "S0000", "S1000", "N0000", "N1000", "R0000");
                Values = @("0x6300", "0x6301", "0x6302", "0x6303", "0x6304", "0x6305");
                LogName = "relay_p3_prefixed_words_restore.log";
            }
        )

        foreach ($case in $relayProgramWordCases) {
            $devices = $case.Devices | ForEach-Object { "{0}-{1}" -f $case.Prefix, $_ }
            Invoke-RelayManyWriteRestore -Label ("Relay {0} prefixed words write/restore" -f $case.Prefix) -Devices $devices -WriteValues $case.Values -LogName $case.LogName
        }

        $negativeU20000Log = Join-Path $ResolvedLogDir "relay_u20000_negative.log"
        Invoke-SmokeExpectedFailure -Label "Relay U20000 negative read" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "U20000",
            "--verbose",
            "--log", $negativeU20000Log
        ) -LogPath $negativeU20000Log -ExpectedMessageContains "U PC10 range is 0x08000-0x1FFFF"

        $negativeEb40000Log = Join-Path $ResolvedLogDir "relay_eb40000_negative.log"
        Invoke-SmokeExpectedFailure -Label "Relay EB40000 negative read" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "EB40000",
            "--verbose",
            "--log", $negativeEb40000Log
        ) -LogPath $negativeEb40000Log -ExpectedMessageContains "EB index out of range"

        $negativeP1D3000Log = Join-Path $ResolvedLogDir "relay_p1_d3000_negative.log"
        Invoke-SmokeExpectedFailure -Label "Relay P1-D3000 negative read" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--device", "P1-D3000",
            "--verbose",
            "--log", $negativeP1D3000Log
        ) -LogPath $negativeP1D3000Log -ExpectedMessageContains "Program word address out of range"

        $negativeFr200000Log = Join-Path $ResolvedLogDir "relay_fr200000_negative.log"
        Invoke-SmokeExpectedFailure -Label "Relay FR200000 negative read" -SmokeArgs @(
            "--host", $PlcHost,
            "--port", $Port,
            "--protocol", $Protocol,
            "--profile", "Nano 10GX:Compatible mode",
            "--hops", $Hops,
            "--fr-device", "FR200000",
            "--verbose",
            "--log", $negativeFr200000Log
        ) -LogPath $negativeFr200000Log -ExpectedMessageContains "FR index out of range"
    }
}
finally {
    Pop-Location
}
