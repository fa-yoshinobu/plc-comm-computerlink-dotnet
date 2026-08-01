[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$checker = Join-Path $PSScriptRoot "check_documented_api_diff.ps1"
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "build"))
$workRoot = [IO.Path]::GetFullPath((Join-Path $buildRoot ("api-diff-policy-test-" + [guid]::NewGuid().ToString("N"))))
if (-not $workRoot.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use policy-test work directory outside the repository build directory."
}

function Write-Json {
    param([string]$Path, $Value)
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}

function Invoke-ExpectedFailure {
    param([string[]]$Arguments, [string]$Pattern)
    $output = & pwsh -NoProfile -File $checker @Arguments 2>&1
    if ($LASTEXITCODE -eq 0) { throw "Expected documented API policy failure did not occur: $Pattern" }
    if (($output -join "`n") -notmatch $Pattern) { throw "Documented API policy failed for an unexpected reason: $($output -join "`n")" }
}

$metadataPath = Join-Path $workRoot "baseline.json"
$classificationPath = Join-Path $workRoot "classifications.json"
$actualPath = Join-Path $workRoot "actual.json"
$changedActualPath = Join-Path $workRoot "actual-changed.json"
$prefixClassificationPath = Join-Path $workRoot "classifications-prefix.json"
$incompleteClassificationPath = Join-Path $workRoot "classifications-incomplete.json"
$sourceCommit = "1111111111111111111111111111111111111111"

try {
    [void](New-Item -ItemType Directory -Path $workRoot -Force)
    $metadata = @{
        schema_version = 2
        package_id = "Example.Package"
        baseline_version = "3.2.1"
        source_uri = "https://example.invalid/example.nupkg"
        sha256 = ("A" * 64)
        frameworks = @(
            @{ tfm = "net8.0"; asset_path = "lib/net8.0/Example.dll" },
            @{ tfm = "net9.0"; asset_path = "lib/net9.0/Example.dll" },
            @{ tfm = "net10.0"; asset_path = "lib/net10.0/Example.dll" }
        )
        source = @{
            tag = "v3.2.1"
            commit = $sourceCommit
            documentation_paths = @(
                "README.md",
                "docsrc/user/GETTING_STARTED.md",
                "docsrc/user/USAGE_GUIDE.md",
                "docsrc/user/PROFILES.md",
                "docsrc/user/GOTCHAS.md",
                "docsrc/user/API_REFERENCE.md"
            )
            examples_prefix = "examples/"
        }
        release_policy = @{ baseline_major = 3; minimum_release_major = 4; required_disposition = "next-major" }
    }
    Write-Json $metadataPath $metadata

    $actual = @(
        [pscustomobject]@{ tfm = "net8.0"; change = "removed"; symbol = "method:Example.Api::Documented"; before_signature = "before-documented"; after_signature = $null; baseline_documented = $true; baseline_documentation = @{ source_commit = $sourceCommit; required_terms = @("Api", "Documented"); matching_paths = @("docsrc/user/API_REFERENCE.md") } },
        [pscustomobject]@{ tfm = "net8.0"; change = "removed"; symbol = "method:Example.Api::Undocumented"; before_signature = "before-undocumented"; after_signature = $null; baseline_documented = $false; baseline_documentation = @{ source_commit = $sourceCommit; required_terms = @("Api", "Undocumented"); matching_paths = @() } },
        [pscustomobject]@{ tfm = "net8.0"; change = "added"; symbol = "method:Example.Api::Added"; before_signature = $null; after_signature = "after-added-net8"; baseline_documented = $null; baseline_documentation = @{ source_commit = $sourceCommit; required_terms = @(); matching_paths = @() } },
        [pscustomobject]@{ tfm = "net8.0"; change = "added"; symbol = "method:Example.Api::Generated"; before_signature = $null; after_signature = "after-generated"; baseline_documented = $null; baseline_documentation = @{ source_commit = $sourceCommit; required_terms = @(); matching_paths = @() } },
        [pscustomobject]@{ tfm = "net9.0"; change = "added"; symbol = "method:Example.Api::Added"; before_signature = $null; after_signature = "after-added-net9"; baseline_documented = $null; baseline_documentation = @{ source_commit = $sourceCommit; required_terms = @(); matching_paths = @() } },
        [pscustomobject]@{ tfm = "net10.0"; change = "added"; symbol = "method:Example.Api::Added"; before_signature = $null; after_signature = "after-added-net10"; baseline_documented = $null; baseline_documentation = @{ source_commit = $sourceCommit; required_terms = @(); matching_paths = @() } }
    )
    Write-Json $actualPath $actual

    $items = foreach ($change in $actual) {
        $item = [ordered]@{
            tfm = $change.tfm
            change = $change.change
            symbol = $change.symbol
            before_signature = $change.before_signature
            after_signature = $change.after_signature
            baseline_documented = $change.baseline_documented
            documentation_basis_commit = $sourceCommit
            rationale = "exact fixture"
        }
        if ($change.symbol -like "*::Documented") {
            $item.category = "documented-contract"
            $item.decision_id = "TEST-DOCUMENTED"
            $item.migration = "fixture migration"
            $item.changelog = "fixture changelog"
            $item.api_documentation = "fixture API reference"
            $item.release_disposition = "next-major"
            $item.minimum_release_major = 4
        }
        elseif ($change.symbol -like "*::Undocumented") {
            $item.category = "undocumented-public"
            $item.decision_id = "TEST-UNDOCUMENTED"
            $item.migration = "fixture migration"
            $item.changelog = "fixture changelog"
            $item.api_documentation = "fixture API reference"
            $item.release_disposition = "next-major"
            $item.minimum_release_major = 4
        }
        elseif ($change.symbol -like "*::Generated") {
            $item.category = "generated-or-noncontract"
            $item.noncontract_boundary = "exact generated fixture signature"
        }
        else {
            $item.category = "additive"
            $item.changelog = "fixture changelog"
            $item.api_documentation = "fixture API reference"
        }
        [pscustomobject]$item
    }
    $classifications = [ordered]@{
        schema_version = 2
        baseline_source_commit = $sourceCommit
        status = "complete"
        candidate_commit = "2222222222222222222222222222222222222222"
        items = @($items)
    }
    Write-Json $classificationPath $classifications

    & pwsh -NoProfile -File $checker -Mode Classify -MetadataPath $metadataPath -ClassificationPath $classificationPath -ActualChangesPath $actualPath
    if ($LASTEXITCODE -ne 0) { throw "Exact four-category/three-TFM classification fixture failed." }
    & pwsh -NoProfile -File $checker -Mode ReleasePolicy -MetadataPath $metadataPath -ClassificationPath $classificationPath -ReleaseVersion 4.0.0
    if ($LASTEXITCODE -ne 0) { throw "Next-major release fixture failed." }

    Invoke-ExpectedFailure @("-Mode", "ReleasePolicy", "-MetadataPath", $metadataPath, "-ClassificationPath", $classificationPath, "-ReleaseVersion", "3.2.2") "requires release major 4"

    $changedActual = @($actual | ConvertTo-Json -Depth 12 | ConvertFrom-Json)
    $changedActual[-1].after_signature = "unexpected-candidate-signature"
    Write-Json $changedActualPath $changedActual
    Invoke-ExpectedFailure @("-Mode", "Classify", "-MetadataPath", $metadataPath, "-ClassificationPath", $classificationPath, "-ActualChangesPath", $changedActualPath) "Unclassified exact public API differences"

    $prefixClassifications = $classifications | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $prefixClassifications.items[0] | Add-Member -NotePropertyName prefix -NotePropertyValue "removed|method:Example.Api"
    Write-Json $prefixClassificationPath $prefixClassifications
    Invoke-ExpectedFailure @("-Mode", "Classify", "-MetadataPath", $metadataPath, "-ClassificationPath", $prefixClassificationPath, "-ActualChangesPath", $actualPath) "Prefix API classifications are forbidden"

    $incompleteClassifications = $classifications | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $incompleteClassifications.status = "incomplete"
    Write-Json $incompleteClassificationPath $incompleteClassifications
    Invoke-ExpectedFailure @("-Mode", "Classify", "-MetadataPath", $metadataPath, "-ClassificationPath", $incompleteClassificationPath, "-ActualChangesPath", $actualPath) "classification status is 'incomplete'"

    Write-Host "[OK] Exact API policy fixtures cover four categories, three TFMs, candidate-signature drift, prefix rejection, incomplete status, and release-major enforcement."
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}
