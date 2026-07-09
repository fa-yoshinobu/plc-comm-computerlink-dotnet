# Release Checklist

This document is the release checklist for `PlcComm.Toyopuc`.

## Scope

Confirm that the release contains only public .NET assets:

- `src/Toyopuc`
- `tools/validation/PlcComm.Toyopuc.SmokeTest`
- `examples/PlcComm.Toyopuc.MinimalRead`
- `tools/validation/PlcComm.Toyopuc.SoakMonitor`
- `README.md`
- `CHANGELOG.md`
- `LICENSE`
- maintainer archive

Confirm that local output is excluded:

- `bin/`
- `obj/`
- `logs/`
- `artifacts/`

## Versioning

Before packaging:

1. Update `<Version>` in [../../src/Toyopuc/PlcComm.Toyopuc.csproj](../../src/Toyopuc/PlcComm.Toyopuc.csproj).
2. Update [CHANGELOG.md](../../CHANGELOG.md) so the released changes are recorded in
   the target version section.
3. Make sure the release tag matches the package version, for example `v1.0.0`.

## Quality Gates

Run these commands locally:

```powershell
cmd /c run_ci.bat
```

If hardware verification is part of the release, also run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\validation\run_validation.ps1 -Target plus
powershell -ExecutionPolicy Bypass -File tools\validation\run_validation.ps1 -Target relay-10gx
```

## Packaging

Canonical pre-release entry point:

```bat
release_check.bat
```

This runs:

1. NuGet registry duplicate check
2. `run_ci.bat`
3. `dotnet pack`

For package output, run:

```powershell
dotnet pack src\Toyopuc\PlcComm.Toyopuc.csproj -c Release
```

For a standalone HighLevelSample publish run:

```powershell
dotnet publish examples\PlcComm.Toyopuc.HighLevelSample\PlcComm.Toyopuc.HighLevelSample.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false -o publish\HighLevelSample
```

Expected release outputs:

- `src\Toyopuc\bin\Release\*.nupkg`
- `src\Toyopuc\bin\Release\*.snupkg`
- `publish\HighLevelSample\PlcComm.Toyopuc.HighLevelSample.exe`

GitHub Release assets should be built from the same commit as the release tag.
Do not upload assets built from `main` when the release points at an older tag.

Required GitHub Release assets:

- `PlcComm.Toyopuc.<version>.nupkg`
- `PlcComm.Toyopuc.<version>.snupkg`
- `PlcComm.Toyopuc.<version>.dll.zip`

The DLL zip should contain the Release build output for the package version.

## Pre-Tag Review Checklist

Before creating a release tag, confirm these review items:

1. Tag alignment
   - The target tag commit matches the intended `main` commit.
   - If `main` moved after a prior tag, create a new version tag instead of reusing old notes.
2. Changelog alignment
   - Recent fixes are recorded in [CHANGELOG.md](../../CHANGELOG.md) (including test-stability fixes).
3. Example CLI regression
   - Manual validation confirms the high-level sample still runs with the current package.
   - If behavior changed, add or update automated regression coverage.
4. Release consistency (tag, notes, assets)
   - GitHub Release notes mention bundled example artifacts only when they are intentionally shipped.
   - Attached release assets match the release notes and target tag commit.

## GitHub Actions

Repository workflows:

- `.github/workflows/ci.yml`
  - restore, build, and test on Windows for pushes and pull requests
- `.github/workflows/release.yml`
  - build release artifacts on tag pushes and manual dispatch
  - create or update a GitHub Release for `v*` tags

Before cutting a tag, confirm the CI workflow is green on the release commit.

## NuGet Readiness

Confirm package metadata in [../../src/Toyopuc/PlcComm.Toyopuc.csproj](../../src/Toyopuc/PlcComm.Toyopuc.csproj):

- package id
- version
- description
- repository URL
- README
- license
- symbols package generation
- Source Link repository metadata

## Final Git Check

Before tagging:

```powershell
git status
git diff --stat
```

Confirm:

- no accidental local files
- no generated logs
- no leftover temporary artifacts

## Publish Order

Recommended order:

1. Merge the release commit.
2. Verify CI on that commit.
3. Create and push the version tag.
4. Let the release workflow build the package artifacts and attach them to the GitHub Release.
5. Publish the `.nupkg` and `.snupkg` to NuGet manually after the GitHub Release artifacts are verified.

## Release Notes

Each GitHub Release should include:

- `Added APIs`: list new public API names and the version they first appear in.
- `Compatibility`: target framework, behavior changes, and migration notes.
- `Assets`: list the attached `.nupkg`, `.snupkg`, and DLL zip filenames.

Use `No new public API additions are called out for this release.` when there
are no public API additions.

After publishing, verify the release:

```powershell
gh release view vX.Y.Z --repo fa-yoshinobu/plc-comm-computerlink-dotnet --json tagName,name,assets,url
gh release list --repo fa-yoshinobu/plc-comm-computerlink-dotnet --limit 3
```

