[CmdletBinding()]
param(
    [ValidateSet("Gate", "Export", "Classify", "ReleasePolicy")]
    [string]$Mode = "Gate",
    [string]$CandidateAssemblyRoot,
    [string]$MetadataPath = "internal_docs/maintainer/api-diff/baseline.json",
    [string]$ClassificationPath = "internal_docs/maintainer/api-diff/classifications.json",
    [string]$AssemblyPath,
    [string]$OutputPath,
    [string]$ActualChangesPath,
    [string]$ReviewOutput,
    [string]$ReleaseVersion,
    [string]$ReleaseVersionFile,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

function Get-StableTypeName {
    param([Type]$Type)

    if ($Type.IsByRef) { return "$(Get-StableTypeName ($Type.GetElementType()))&" }
    if ($Type.IsPointer) { return "$(Get-StableTypeName ($Type.GetElementType()))*" }
    if ($Type.IsArray) {
        $commas = "," * ($Type.GetArrayRank() - 1)
        return "$(Get-StableTypeName ($Type.GetElementType()))[$commas]"
    }
    if ($Type.IsGenericParameter) { return "!$($Type.GenericParameterPosition):$($Type.Name)" }
    if ($Type.IsGenericType) {
        $definition = $Type.GetGenericTypeDefinition().FullName
        if ($null -eq $definition) { $definition = $Type.Name }
        $definition = $definition.Replace("+", ".") -replace '`\d+$', ''
        $arguments = @($Type.GetGenericArguments() | ForEach-Object { Get-StableTypeName $_ })
        return "$definition<$($arguments -join ',')>"
    }

    $name = $Type.FullName
    if ($null -eq $name) { $name = $Type.Name }
    return $name.Replace("+", ".")
}

function Get-ParameterText {
    param([Reflection.ParameterInfo]$Parameter)

    $typeName = Get-StableTypeName $Parameter.ParameterType
    $direction = if ($Parameter.IsOut) { "out" } elseif ($Parameter.ParameterType.IsByRef -and $Parameter.IsIn) { "in" } elseif ($Parameter.ParameterType.IsByRef) { "ref" } else { "value" }
    $optional = if ($Parameter.IsOptional) { "optional" } else { "required" }
    $default = "none"
    if ($Parameter.HasDefaultValue) {
        if ($null -eq $Parameter.DefaultValue) {
            $default = "null"
        }
        elseif ($Parameter.DefaultValue -is [Enum]) {
            $default = [Convert]::ToString($Parameter.DefaultValue, [Globalization.CultureInfo]::InvariantCulture)
        }
        else {
            $default = [Convert]::ToString($Parameter.DefaultValue, [Globalization.CultureInfo]::InvariantCulture)
        }
        $default = $default.Replace("|", "\\|")
    }
    return "$($Parameter.Name):$($typeName):$($direction):$($optional):default=$default"
}

function Get-GenericConstraintText {
    param([Type[]]$Arguments)

    $result = foreach ($argument in $Arguments) {
        $attributes = $argument.GenericParameterAttributes.ToString()
        $constraints = @($argument.GetGenericParameterConstraints() | ForEach-Object { Get-StableTypeName $_ } | Sort-Object)
        "$($argument.Name):attributes=$($attributes):types=$($constraints -join ',')"
    }
    return $result -join ";"
}

function Add-SurfaceItem {
    param(
        [Collections.Generic.List[object]]$Items,
        [string]$Symbol,
        [string]$Signature
    )

    $Items.Add([pscustomobject]@{ symbol = $Symbol; signature = $Signature })
}

function Export-PublicSurface {
    param(
        [string]$InputAssembly,
        [string]$Destination
    )

    $resolvedAssembly = [IO.Path]::GetFullPath($InputAssembly)
    if (-not (Test-Path -LiteralPath $resolvedAssembly -PathType Leaf)) {
        throw "Assembly does not exist: $resolvedAssembly"
    }

    $assembly = [Reflection.Assembly]::LoadFrom($resolvedAssembly)
    $flags = [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::DeclaredOnly
    $surface = [Collections.Generic.List[object]]::new()

    foreach ($type in @($assembly.GetExportedTypes() | Sort-Object FullName)) {
        $owner = Get-StableTypeName $type
        $kind = if ($type.IsEnum) { "enum" } elseif ($type.IsInterface) { "interface" } elseif ($type.IsValueType) { "struct" } elseif ([MulticastDelegate].IsAssignableFrom($type.BaseType)) { "delegate" } else { "class" }
        $baseType = if ($null -eq $type.BaseType) { "none" } else { Get-StableTypeName $type.BaseType }
        $interfaces = @($type.GetInterfaces() | ForEach-Object { Get-StableTypeName $_ } | Sort-Object)
        $constraints = Get-GenericConstraintText @($type.GetGenericArguments() | Where-Object IsGenericParameter)
        $visibility = if ($type.IsNested) { "nested-public" } else { "public" }
        Add-SurfaceItem $surface "type:$owner" "type|$kind|$owner|visibility=$visibility|abstract=$($type.IsAbstract)|sealed=$($type.IsSealed)|base=$baseType|interfaces=$($interfaces -join ',')|constraints=$constraints"

        foreach ($constructor in @($type.GetConstructors($flags) | Sort-Object { $_.ToString() })) {
            $parameters = @($constructor.GetParameters() | ForEach-Object { Get-ParameterText $_ })
            Add-SurfaceItem $surface "constructor:$owner::.ctor" "member|$owner|constructor|.ctor|public|instance|params=$($parameters -join ',')"
        }

        foreach ($method in @($type.GetMethods($flags) | Where-Object { -not $_.IsSpecialName } | Sort-Object Name, { $_.ToString() })) {
            $parameters = @($method.GetParameters() | ForEach-Object { Get-ParameterText $_ })
            $methodConstraints = Get-GenericConstraintText @($method.GetGenericArguments() | Where-Object IsGenericParameter)
            $static = if ($method.IsStatic) { "static" } else { "instance" }
            Add-SurfaceItem $surface "method:$owner::$($method.Name)" "member|$owner|method|$($method.Name)|public|$static|abstract=$($method.IsAbstract)|virtual=$($method.IsVirtual)|final=$($method.IsFinal)|returns=$(Get-StableTypeName $method.ReturnType)|params=$($parameters -join ',')|constraints=$methodConstraints"
        }

        foreach ($property in @($type.GetProperties($flags) | Sort-Object Name, { $_.ToString() })) {
            $getter = $property.GetGetMethod()
            $setter = $property.GetSetMethod()
            $access = "public-get=$($null -ne $getter);public-set=$($null -ne $setter)"
            $static = if (($null -ne $getter -and $getter.IsStatic) -or ($null -ne $setter -and $setter.IsStatic)) { "static" } else { "instance" }
            $index = @($property.GetIndexParameters() | ForEach-Object { Get-ParameterText $_ })
            Add-SurfaceItem $surface "property:$owner::$($property.Name)" "member|$owner|property|$($property.Name)|$static|type=$(Get-StableTypeName $property.PropertyType)|$access|index=$($index -join ',')"
        }

        foreach ($event in @($type.GetEvents($flags) | Sort-Object Name)) {
            $addMethod = $event.GetAddMethod()
            $static = if ($null -ne $addMethod -and $addMethod.IsStatic) { "static" } else { "instance" }
            Add-SurfaceItem $surface "event:$owner::$($event.Name)" "member|$owner|event|$($event.Name)|public|$static|type=$(Get-StableTypeName $event.EventHandlerType)"
        }

        foreach ($field in @($type.GetFields($flags) | Sort-Object Name)) {
            $static = if ($field.IsStatic) { "static" } else { "instance" }
            $value = "none"
            if ($field.IsLiteral) {
                $value = [Convert]::ToString($field.GetRawConstantValue(), [Globalization.CultureInfo]::InvariantCulture)
            }
            Add-SurfaceItem $surface "field:$owner::$($field.Name)" "member|$owner|field|$($field.Name)|public|$static|type=$(Get-StableTypeName $field.FieldType)|literal=$($field.IsLiteral)|readonly=$($field.IsInitOnly)|value=$value"
        }
    }

    $ordered = @($surface | Sort-Object symbol, signature)
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($Destination), (ConvertTo-Json -InputObject $ordered -Depth 5), [Text.UTF8Encoding]::new($false))
}

function Resolve-RepositoryPath {
    param([string]$RepositoryRoot, [string]$Path)
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function Read-JsonDocument {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "JSON document does not exist: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-Metadata {
    param($Metadata)

    if ($Metadata.schema_version -ne 2) { throw "Unsupported baseline schema: $($Metadata.schema_version)" }
    foreach ($name in @("package_id", "baseline_version", "source_uri", "sha256")) {
        if ([string]::IsNullOrWhiteSpace([string]$Metadata.$name)) { throw "Baseline metadata is missing '$name'." }
    }
    if ([string]$Metadata.sha256 -notmatch '^[0-9A-F]{64}$') { throw "Baseline SHA-256 must be a full uppercase digest." }
    if ([string]$Metadata.source.tag -cne "v$($Metadata.baseline_version)") { throw "Baseline source tag must match baseline_version exactly." }
    $frameworks = @($Metadata.frameworks)
    $expectedFrameworks = @("net8.0", "net9.0", "net10.0")
    if ($frameworks.Count -ne 3 -or @($expectedFrameworks | Where-Object { $_ -notin @($frameworks.tfm) }).Count -ne 0) {
        throw "Baseline metadata must define independent net8.0, net9.0, and net10.0 assets."
    }
    foreach ($framework in $frameworks) {
        if ([string]::IsNullOrWhiteSpace([string]$framework.asset_path)) { throw "Framework '$($framework.tfm)' has no asset path." }
    }

    foreach ($name in @("tag", "commit", "examples_prefix")) {
        if ([string]::IsNullOrWhiteSpace([string]$Metadata.source.$name)) { throw "Baseline source metadata is missing '$name'." }
    }
    if ([string]$Metadata.source.commit -notmatch '^[0-9a-f]{40}$') { throw "Baseline source commit must be a full lowercase Git commit ID." }
    $requiredDocs = @(
        "README.md",
        "docsrc/user/GETTING_STARTED.md",
        "docsrc/user/USAGE_GUIDE.md",
        "docsrc/user/PROFILES.md",
        "docsrc/user/GOTCHAS.md",
        "docsrc/user/API_REFERENCE.md"
    )
    $configuredDocs = @($Metadata.source.documentation_paths)
    if (@($requiredDocs | Where-Object { $_ -notin $configuredDocs }).Count -ne 0) {
        throw "Baseline source metadata must include README and all five standard/generated user pages."
    }

    if ([int]$Metadata.release_policy.baseline_major -ne ([int](([string]$Metadata.baseline_version -split '\.')[0]))) {
        throw "Release policy baseline major does not match baseline_version."
    }
    if ([int]$Metadata.release_policy.minimum_release_major -le [int]$Metadata.release_policy.baseline_major) {
        throw "Breaking release minimum major must be greater than the prior stable major."
    }
    if ([string]$Metadata.release_policy.required_disposition -cne "next-major") {
        throw "Breaking release disposition must be 'next-major'."
    }
}

function Get-ChangeKey {
    param($Change)
    $before = if ($null -eq $Change.before_signature) { "<absent>" } else { [string]$Change.before_signature }
    $after = if ($null -eq $Change.after_signature) { "<absent>" } else { [string]$Change.after_signature }
    return @([string]$Change.tfm, [string]$Change.change, [string]$Change.symbol, $before, $after) -join [char]0x1F
}

function Get-RequiredDocumentationTerms {
    param($Change)

    $terms = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $symbol = [string]$Change.symbol
    $ownerAndMember = $symbol.Substring($symbol.IndexOf(':') + 1)
    $parts = $ownerAndMember -split '::', 2
    [void]$terms.Add(($parts[0] -split '\.')[-1])
    if ($parts.Count -eq 2 -and $parts[1] -ne ".ctor") { [void]$terms.Add($parts[1]) }
    foreach ($match in [regex]::Matches([string]$Change.before_signature, 'PlcComm\.Toyopuc\.([A-Za-z_][A-Za-z0-9_]*)')) {
        [void]$terms.Add($match.Groups[1].Value)
    }
    return @($terms | Sort-Object)
}

function Get-BaselineDocumentation {
    param($Metadata)

    $commit = [string]$Metadata.source.commit
    $tagExpression = "$($Metadata.source.tag)^{commit}"
    $tagCommit = (& git rev-parse $tagExpression 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $tagCommit -cne $commit) {
        throw "Baseline source tag '$($Metadata.source.tag)' does not resolve to immutable commit '$commit'. Ensure checkout fetch-depth is 0."
    }

    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in @($Metadata.source.documentation_paths)) { [void]$paths.Add([string]$path) }
    $examplePaths = @(& git ls-tree -r --name-only $commit -- ([string]$Metadata.source.examples_prefix))
    if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate baseline examples at $commit." }
    foreach ($path in $examplePaths) {
        if ($path -match '\.(cs|csproj|md|json)$') { [void]$paths.Add([string]$path) }
    }

    $documents = [Collections.Generic.List[object]]::new()
    foreach ($path in @($paths | Sort-Object)) {
        $text = (& git show "${commit}:$path" 2>$null | Out-String)
        if ($LASTEXITCODE -ne 0) { throw "Baseline documentation path '$path' does not exist at $commit." }
        $documents.Add([pscustomobject]@{ path = $path; text = $text })
    }
    return @($documents)
}

function Add-DocumentationEvidence {
    param($Change, [object[]]$Documents, [string]$SourceCommit)

    if ([string]$Change.change -eq "added") {
        $Change | Add-Member -NotePropertyName baseline_documented -NotePropertyValue $null
        $Change | Add-Member -NotePropertyName baseline_documentation -NotePropertyValue ([pscustomobject]@{
            source_commit = $SourceCommit
            required_terms = @()
            matching_paths = @()
        })
        return
    }

    $terms = @(Get-RequiredDocumentationTerms $Change)
    $matches = @($Documents | Where-Object {
        $text = [string]$_.text
        @($terms | Where-Object { $text.IndexOf([string]$_, [StringComparison]::Ordinal) -lt 0 }).Count -eq 0
    } | ForEach-Object { $_.path })
    $Change | Add-Member -NotePropertyName baseline_documented -NotePropertyValue ($matches.Count -gt 0)
    $Change | Add-Member -NotePropertyName baseline_documentation -NotePropertyValue ([pscustomobject]@{
        source_commit = $SourceCommit
        required_terms = $terms
        matching_paths = $matches
    })
}

function Compare-Surfaces {
    param(
        [string]$Tfm,
        [string]$BaselineSurfacePath,
        [string]$CandidateSurfacePath,
        [object[]]$Documents,
        [string]$SourceCommit
    )

    $baseline = @(Read-JsonDocument $BaselineSurfacePath)
    $candidate = @(Read-JsonDocument $CandidateSurfacePath)
    $baselineKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $candidateKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $baseline) { [void]$baselineKeys.Add("$($item.symbol)$([char]0x1F)$($item.signature)") }
    foreach ($item in $candidate) { [void]$candidateKeys.Add("$($item.symbol)$([char]0x1F)$($item.signature)") }

    $changes = [Collections.Generic.List[object]]::new()
    foreach ($item in $baseline) {
        $key = "$($item.symbol)$([char]0x1F)$($item.signature)"
        if (-not $candidateKeys.Contains($key)) {
            $change = [pscustomobject]@{
                tfm = $Tfm
                change = "removed"
                symbol = [string]$item.symbol
                before_signature = [string]$item.signature
                after_signature = $null
            }
            Add-DocumentationEvidence $change $Documents $SourceCommit
            $changes.Add($change)
        }
    }
    foreach ($item in $candidate) {
        $key = "$($item.symbol)$([char]0x1F)$($item.signature)"
        if (-not $baselineKeys.Contains($key)) {
            $change = [pscustomobject]@{
                tfm = $Tfm
                change = "added"
                symbol = [string]$item.symbol
                before_signature = $null
                after_signature = [string]$item.signature
            }
            Add-DocumentationEvidence $change $Documents $SourceCommit
            $changes.Add($change)
        }
    }
    return @($changes)
}

function Assert-RequiredTextFields {
    param($Item, [string[]]$Names)
    foreach ($name in $Names) {
        if ($null -eq $Item.PSObject.Properties[$name] -or [string]::IsNullOrWhiteSpace([string]$Item.$name)) {
            throw "Classification for '$($Item.symbol)' requires '$name'."
        }
    }
}

function Assert-ReleasePolicy {
    param($Metadata, $Classifications, [string]$CandidateVersion)

    if ($Classifications.schema_version -ne 2) { throw "Unsupported classification schema: $($Classifications.schema_version)" }
    if ([string]$Classifications.baseline_source_commit -cne [string]$Metadata.source.commit) { throw "Release policy classifications use the wrong immutable source commit." }
    if ([string]$Classifications.status -cne "complete") {
        throw "API classifications are incomplete; release-major disposition cannot be approved."
    }
    $minimumMajor = [int]$Metadata.release_policy.minimum_release_major
    $requiredDisposition = [string]$Metadata.release_policy.required_disposition
    $breakingItems = @($Classifications.items | Where-Object { $_.category -in @("documented-contract", "undocumented-public") })
    foreach ($item in @($Classifications.items)) {
        if ($null -ne $item.PSObject.Properties["prefix"] -or $null -ne $item.PSObject.Properties["prefixes"]) {
            throw "Prefix API classifications are forbidden in release policy."
        }
    }
    foreach ($item in $breakingItems) {
        if ([string]$item.release_disposition -cne $requiredDisposition) {
            throw "Breaking classification '$($item.symbol)' lacks release disposition '$requiredDisposition'."
        }
        if ([int]$item.minimum_release_major -ne $minimumMajor) {
            throw "Breaking classification '$($item.symbol)' must require release major $minimumMajor."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($CandidateVersion) -and $breakingItems.Count -gt 0) {
        if ($CandidateVersion -notmatch '^(\d+)\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$') {
            throw "Invalid release version for API policy: $CandidateVersion"
        }
        if ([int]$Matches[1] -lt $minimumMajor) {
            throw "Breaking API delta requires release major $minimumMajor or later; got $CandidateVersion."
        }
    }
}

function Assert-Classifications {
    param($Metadata, [object[]]$ActualChanges, $Classifications, [string]$CandidateVersion)

    if ($Classifications.schema_version -ne 2) { throw "Unsupported classification schema: $($Classifications.schema_version)" }
    if ([string]$Classifications.baseline_source_commit -cne [string]$Metadata.source.commit) {
        throw "Classification baseline source commit does not match immutable baseline metadata."
    }
    if ([string]$Classifications.status -cne "complete") {
        throw "API classification status is '$($Classifications.status)'. Review the emitted actual differences and replace the incomplete record with exact classifications."
    }

    $items = @($Classifications.items)
    $byKey = @{}
    foreach ($item in $items) {
        if ($null -ne $item.PSObject.Properties["prefix"] -or $null -ne $item.PSObject.Properties["prefixes"]) {
            throw "Prefix API classifications are forbidden; '$($item.symbol)' must use exact before/after signatures."
        }
        Assert-RequiredTextFields $item @("tfm", "change", "symbol", "category", "rationale", "documentation_basis_commit")
        foreach ($name in @("before_signature", "after_signature", "baseline_documented")) {
            if ($null -eq $item.PSObject.Properties[$name]) {
                throw "Classification for '$($item.symbol)' must contain exact '$name', including an explicit null where absent."
            }
        }
        if ($item.tfm -notin @("net8.0", "net9.0", "net10.0")) { throw "Classification has unknown TFM '$($item.tfm)'." }
        if ($item.change -notin @("added", "removed")) { throw "Classification has unknown change '$($item.change)'." }
        if ($item.category -notin @("documented-contract", "undocumented-public", "additive", "generated-or-noncontract")) { throw "Classification has unknown category '$($item.category)'." }
        if ([string]$item.documentation_basis_commit -cne [string]$Metadata.source.commit) { throw "Classification '$($item.symbol)' uses the wrong documentation source commit." }
        $key = Get-ChangeKey $item
        if ($byKey.ContainsKey($key)) { throw "Duplicate exact classification for '$($item.symbol)' on '$($item.tfm)'." }
        $byKey[$key] = $item
    }

    $actualByKey = @{}
    foreach ($change in $ActualChanges) { $actualByKey[(Get-ChangeKey $change)] = $change }
    $unclassified = @($ActualChanges | Where-Object { -not $byKey.ContainsKey((Get-ChangeKey $_)) })
    $stale = @($items | Where-Object { -not $actualByKey.ContainsKey((Get-ChangeKey $_)) })
    if ($unclassified.Count -gt 0) {
        throw "Unclassified exact public API differences:`n$($unclassified | ConvertTo-Json -Depth 8)"
    }
    if ($stale.Count -gt 0) {
        throw "Stale or candidate-mismatched API classifications:`n$($stale | ConvertTo-Json -Depth 8)"
    }

    foreach ($item in $items) {
        $actual = $actualByKey[(Get-ChangeKey $item)]
        if ($null -eq $actual.baseline_documented) {
            if ($null -ne $item.baseline_documented) { throw "Added item '$($item.symbol)' must use null baseline_documented." }
        }
        elseif ([bool]$item.baseline_documented -ne [bool]$actual.baseline_documented) {
            throw "Classification '$($item.symbol)' disagrees with documentation evidence from $($Metadata.source.commit)."
        }

        switch ([string]$item.category) {
            "documented-contract" {
                if ($item.change -eq "removed" -and -not [bool]$actual.baseline_documented) { throw "Removed '$($item.symbol)' is absent from prior stable docs and cannot be documented-contract." }
                Assert-RequiredTextFields $item @("decision_id", "migration", "changelog", "api_documentation", "release_disposition", "minimum_release_major")
            }
            "undocumented-public" {
                if ($item.change -ne "removed" -or [bool]$actual.baseline_documented) { throw "undocumented-public requires a removed prior public symbol absent from the immutable prior docs/examples basis." }
                Assert-RequiredTextFields $item @("decision_id", "migration", "changelog", "api_documentation", "release_disposition", "minimum_release_major")
            }
            "additive" {
                if ($item.change -ne "added") { throw "additive classification requires an added signature." }
                Assert-RequiredTextFields $item @("changelog", "api_documentation")
            }
            "generated-or-noncontract" {
                Assert-RequiredTextFields $item @("noncontract_boundary")
            }
        }
    }

    Assert-ReleasePolicy $Metadata $Classifications $CandidateVersion
    Write-Host "[OK] Exact documented API classifications passed: changes=$($ActualChanges.Count) tfms=net8.0,net9.0,net10.0"
}

if ($Mode -eq "Export") {
    if ([string]::IsNullOrWhiteSpace($AssemblyPath) -or [string]::IsNullOrWhiteSpace($OutputPath)) { throw "Export mode requires AssemblyPath and OutputPath." }
    Export-PublicSurface $AssemblyPath $OutputPath
    exit 0
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedMetadata = Resolve-RepositoryPath $repositoryRoot $MetadataPath
$resolvedClassifications = Resolve-RepositoryPath $repositoryRoot $ClassificationPath
$metadata = Read-JsonDocument $resolvedMetadata
$classifications = Read-JsonDocument $resolvedClassifications
Assert-Metadata $metadata

if ($Mode -eq "ReleasePolicy") {
    if ([string]::IsNullOrWhiteSpace($ReleaseVersion) -and -not [string]::IsNullOrWhiteSpace($ReleaseVersionFile)) {
        [xml]$versionDocument = Get-Content -LiteralPath (Resolve-RepositoryPath $repositoryRoot $ReleaseVersionFile) -Raw
        $ReleaseVersion = [string]$versionDocument.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) { throw "ReleasePolicy mode requires ReleaseVersion." }
    Assert-ReleasePolicy $metadata $classifications $ReleaseVersion
    Write-Host "[OK] API release-major policy passed for $ReleaseVersion"
    exit 0
}

if ($Mode -eq "Classify") {
    if ([string]::IsNullOrWhiteSpace($ActualChangesPath)) { throw "Classify mode requires ActualChangesPath." }
    $actual = @(Read-JsonDocument (Resolve-RepositoryPath $repositoryRoot $ActualChangesPath))
    Assert-Classifications $metadata $actual $classifications $ReleaseVersion
    exit 0
}

if ([string]::IsNullOrWhiteSpace($CandidateAssemblyRoot)) { throw "Gate mode requires CandidateAssemblyRoot." }
$resolvedCandidateRoot = Resolve-RepositoryPath $repositoryRoot $CandidateAssemblyRoot
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "build"))
$workRoot = [IO.Path]::GetFullPath((Join-Path $buildRoot ("toyopuc-api-diff-" + [guid]::NewGuid().ToString("N"))))
if (-not $workRoot.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing API-diff work outside the repository build directory." }

$baselinePackage = Join-Path $workRoot "$($metadata.package_id).$($metadata.baseline_version).nupkg"
$baselineExtract = Join-Path $workRoot "baseline"
try {
    [void](New-Item -ItemType Directory -Path $workRoot -Force)
    Invoke-WebRequest -Uri ([string]$metadata.source_uri) -OutFile $baselinePackage
    $actualDigest = (Get-FileHash -LiteralPath $baselinePackage -Algorithm SHA256).Hash
    if ($actualDigest -cne ([string]$metadata.sha256).ToUpperInvariant()) { throw "Immutable baseline digest mismatch: expected=$($metadata.sha256) actual=$actualDigest" }
    [IO.Compression.ZipFile]::ExtractToDirectory($baselinePackage, $baselineExtract)

    $documents = @(Get-BaselineDocumentation $metadata)
    $allChanges = [Collections.Generic.List[object]]::new()
    foreach ($framework in @($metadata.frameworks)) {
        $tfm = [string]$framework.tfm
        $baselineAssembly = Join-Path $baselineExtract ([string]$framework.asset_path)
        $candidateAssembly = Join-Path (Join-Path $resolvedCandidateRoot $tfm) "PlcComm.Toyopuc.dll"
        if (-not (Test-Path -LiteralPath $baselineAssembly -PathType Leaf)) { throw "Baseline package has no $tfm assembly at '$($framework.asset_path)'." }
        if (-not (Test-Path -LiteralPath $candidateAssembly -PathType Leaf)) { throw "Candidate has no independently built $tfm assembly: $candidateAssembly" }

        $baselineSurface = Join-Path $workRoot "baseline-$tfm.json"
        $candidateSurface = Join-Path $workRoot "candidate-$tfm.json"
        $exporterAssembly = Join-Path $repositoryRoot "tools/api-diff/PlcComm.Toyopuc.ApiSurfaceExporter/bin/$Configuration/$tfm/PlcComm.Toyopuc.ApiSurfaceExporter.dll"
        if (-not (Test-Path -LiteralPath $exporterAssembly -PathType Leaf)) { throw "Missing $tfm API exporter for ${Configuration}: $exporterAssembly" }
        & dotnet $exporterAssembly $baselineAssembly $baselineSurface
        if ($LASTEXITCODE -ne 0) { throw "$tfm baseline public-surface export failed." }
        & dotnet $exporterAssembly $candidateAssembly $candidateSurface
        if ($LASTEXITCODE -ne 0) { throw "$tfm candidate public-surface export failed." }
        foreach ($change in @(Compare-Surfaces $tfm $baselineSurface $candidateSurface $documents ([string]$metadata.source.commit))) { $allChanges.Add($change) }
    }

    $orderedChanges = @($allChanges | Sort-Object tfm, change, symbol, before_signature, after_signature)
    if (-not [string]::IsNullOrWhiteSpace($ReviewOutput)) {
        $resolvedReview = Resolve-RepositoryPath $repositoryRoot $ReviewOutput
        [void](New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedReview) -Force)
        [IO.File]::WriteAllText($resolvedReview, (ConvertTo-Json -InputObject $orderedChanges -Depth 10), [Text.UTF8Encoding]::new($false))
        Write-Host "[INFO] Exact API review records: $resolvedReview"
    }
    Assert-Classifications $metadata $orderedChanges $classifications $ReleaseVersion
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}
