[CmdletBinding()]
param(
    [string]$Treeish = "HEAD",
    [switch]$UseWorktreeAttributes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workRoot = Join-Path $repositoryRoot ("build/source-archive-check-" + [guid]::NewGuid().ToString("N"))
$archivePath = Join-Path $workRoot "source.zip"
$extractRoot = Join-Path $workRoot "extracted"
$stageRoot = Join-Path $workRoot "staged"

$forbiddenFileNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
@(
    ".gitattributes",
    ".gitignore"
) | ForEach-Object { [void]$forbiddenFileNames.Add($_) }

$forbiddenPrefixes = @(
    ".codex",
    ".pio",
    ".tools",
    "build",
    "build_win",
    "local_folder",
    "release-artifacts"
)

try {
    [void](New-Item -ItemType Directory -Path $workRoot -Force)

    & git -C $repositoryRoot rev-parse --verify "$Treeish`^{tree}" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot resolve treeish '$Treeish'."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $worktreeFiles = @()
    if ($UseWorktreeAttributes) {
        $worktreeFiles = @(& git -C $repositoryRoot ls-files --cached --others --exclude-standard |
            ForEach-Object { $_.Replace("\", "/") } |
            Where-Object {
                (Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf) -and
                $_ -notin @(".gitattributes", ".gitignore") -and
                $_ -notmatch '^(build|build_win|release-artifacts)/'
            } |
            Sort-Object -Unique)
        if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate current worktree files." }
        [void](New-Item -ItemType Directory -Path $stageRoot)
        foreach ($path in $worktreeFiles) {
            $destination = Join-Path $stageRoot $path
            [void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
            Copy-Item -LiteralPath (Join-Path $repositoryRoot $path) -Destination $destination -Force
        }
        [System.IO.Compression.ZipFile]::CreateFromDirectory($stageRoot, $archivePath)
    }
    else {
        & git -C $repositoryRoot archive --format=zip --output=$archivePath $Treeish
        if ($LASTEXITCODE -ne 0) { throw "git archive failed for '$Treeish'." }
    }
    if (-not (Test-Path -LiteralPath $archivePath)) {
        throw "Source archive was not created for '$Treeish'."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $archiveFiles = @(
            $archive.Entries |
                ForEach-Object { $_.FullName.Replace("\", "/") } |
                Where-Object { -not $_.EndsWith("/") } |
                Sort-Object -Unique
        )
    }
    finally {
        $archive.Dispose()
    }
    $trackedFiles = if ($UseWorktreeAttributes) { $worktreeFiles } else {
        @(& git -C $repositoryRoot ls-tree -r --name-only $Treeish |
            ForEach-Object { $_.Replace("\", "/") } |
            Sort-Object -Unique)
    }
    if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate source files for '$Treeish'." }

    $requiredTracked = @($trackedFiles | Where-Object {
        $_ -match '^(test|tests|\.github|docsrc/maintainer|internal_docs|scripts|tools)/' -or
        $_ -in @("AGENTS.md", "TODO.md", "release_check.bat", "run_ci.bat")
    })
    $missingTracked = @($requiredTracked | Where-Object { $_ -notin $archiveFiles })
    if ($missingTracked.Count -ne 0) {
        throw "Source archive omits tracked validation or maintainer material: $($missingTracked -join ', ')"
    }

    foreach ($guide in @("GETTING_STARTED.md", "USAGE_GUIDE.md", "PROFILES.md", "GOTCHAS.md", "API_REFERENCE.md")) {
        $guideCandidates = @("docsrc/user/$guide", "docs/$guide")
        if (@($guideCandidates | Where-Object { $_ -in $archiveFiles }).Count -eq 0) {
            throw "Source archive is missing standard user guide '$guide'."
        }
    }


    $forbidden = @(
        foreach ($path in $archiveFiles) {
            $fileName = [System.IO.Path]::GetFileName($path)
            $lowerPath = $path.ToLowerInvariant()
            $hasForbiddenPrefix = $false
            foreach ($prefix in $forbiddenPrefixes) {
                $lowerPrefix = $prefix.ToLowerInvariant()
                if ($lowerPath -eq $lowerPrefix -or $lowerPath.StartsWith("$lowerPrefix/")) {
                    $hasForbiddenPrefix = $true
                    break
                }
            }
            if ($forbiddenFileNames.Contains($fileName) -or $hasForbiddenPrefix) {
                $path
            }
        }
    )
    if ($forbidden.Count -ne 0) {
        throw "Source archive contains forbidden generated or release-output files: $($forbidden -join ', ')"
    }

    $requiredRootFiles = @("CHANGELOG.md", "LICENSE", "README.md")
    $missingRootFiles = @($requiredRootFiles | Where-Object { $_ -notin $archiveFiles })
    if ($missingRootFiles.Count -ne 0) {
        throw "Source archive is missing required root files: $($missingRootFiles -join ', ')"
    }

    $expectedSamples = @($trackedFiles |
        Where-Object { $_.StartsWith("examples/") -or $_.StartsWith("samples/") } |
        Sort-Object -Unique)
    if ($expectedSamples.Count -eq 0) {
        throw "No tracked files were found under examples/ or samples/."
    }

    $actualSamples = @(
        $archiveFiles |
            Where-Object { $_.StartsWith("examples/") -or $_.StartsWith("samples/") } |
            Sort-Object -Unique
    )
    $sampleDifference = @(Compare-Object -ReferenceObject $expectedSamples -DifferenceObject $actualSamples -CaseSensitive)
    if ($sampleDifference.Count -ne 0) {
        $differenceText = ($sampleDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join "; "
        throw "Source archive sample set differs from the tracked sample set: $differenceText"
    }

    $expectedTests = @($trackedFiles | Where-Object { $_.StartsWith("tests/") } | Sort-Object -Unique)
    if ($expectedTests.Count -eq 0) {
        throw "Cannot enumerate tracked tests for '$Treeish'."
    }
    $actualTests = @($archiveFiles | Where-Object { $_.StartsWith("tests/") } | Sort-Object -Unique)
    $testDifference = @(Compare-Object -ReferenceObject $expectedTests -DifferenceObject $actualTests -CaseSensitive)
    if ($testDifference.Count -ne 0) {
        $differenceText = ($testDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join "; "
        throw "Source archive test set differs from the tracked test set: $differenceText"
    }

    $expectedTools = @($trackedFiles | Where-Object { $_.StartsWith("tools/validation/") } | Sort-Object -Unique)
    if ($expectedTools.Count -eq 0) {
        throw "Cannot enumerate tracked validation tools for '$Treeish'."
    }
    $actualTools = @($archiveFiles | Where-Object { $_.StartsWith("tools/validation/") } | Sort-Object -Unique)
    $toolDifference = @(Compare-Object -ReferenceObject $expectedTools -DifferenceObject $actualTools -CaseSensitive)
    if ($toolDifference.Count -ne 0) {
        $differenceText = ($toolDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join "; "
        throw "Source archive validation-tool set differs from the tracked set: $differenceText"
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot
    Push-Location $extractRoot
    try {
        & dotnet restore PlcComm.Toyopuc.sln
        if ($LASTEXITCODE -ne 0) {
            throw "Source archive solution restore failed."
        }
        & dotnet build PlcComm.Toyopuc.sln -c Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Source archive solution build failed."
        }
        & dotnet test PlcComm.Toyopuc.sln -c Release --no-build
        if ($LASTEXITCODE -ne 0) {
            throw "Source archive solution tests failed."
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "[OK] Source archive contract passed: treeish=$Treeish files=$($archiveFiles.Count) samples=$($actualSamples.Count) tests=$($actualTests.Count) tools=$($actualTools.Count)"
}
finally {
    Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
}
