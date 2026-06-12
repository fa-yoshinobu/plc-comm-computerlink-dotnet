# GitHub Release Template: v0.1.9

Use this file as a copy-paste template for the GitHub Releases form.

## Release Settings

- Title: `Release v0.1.9`
- Tag: `v0.1.9`
- Target: `main`
- Set as latest release: `yes`
- Pre-release: `no`

## Release Body

TOYOPUC Computer Link .NET maintenance release.

## Highlights

- Records the 2026-06-12 defect verification results for PC10 sparse reads, FR commit/persistence, and relay-hop access.
- Keeps generic FR writes guarded; use explicit `WriteFrAsync(..., commit: false|true)` or `CommitFrAsync()`.
- Keeps single-request and chunked APIs explicit so the caller decides when protocol splitting is acceptable.
- Refreshes release documentation and the GitHub Release workflow so the required DLL zip is attached with the NuGet packages.

## Added APIs

No new public API additions are called out for this release.

## Compatibility

- Target framework: `net9.0`
- Package version: `0.1.9`
- Behavior remains compatible with `0.1.8` except that release documentation now treats automatic chunk splitting as caller-controlled rather than implicit convenience behavior.

## Verification

- `release_check.bat` -> passed
- `dotnet build` -> passed
- `dotnet test` -> 198 passed
- `dotnet format --verify-no-changes` -> passed
- `dotnet pack` -> passed
- DocFX build -> passed
- NuGet duplicate check for `PlcComm.Toyopuc` `0.1.9` -> not already published

## Assets

- `out/PlcComm.Toyopuc.0.1.9.nupkg`
- `out/PlcComm.Toyopuc.0.1.9.snupkg`
- `out/PlcComm.Toyopuc.0.1.9.dll.zip`

## Upload Checklist

- attach `PlcComm.Toyopuc.0.1.9.nupkg`
- attach `PlcComm.Toyopuc.0.1.9.snupkg`
- attach `PlcComm.Toyopuc.0.1.9.dll.zip`
- confirm the tag is `v0.1.9`
- confirm release notes match `CHANGELOG.md`
