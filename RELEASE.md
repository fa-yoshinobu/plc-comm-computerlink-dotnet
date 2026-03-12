# Release Checklist

This document is the release checklist for `Toyopuc.Net`.

## Scope

Confirm that the release contains only public .NET assets:

- `src/Toyopuc`
- `examples/Toyopuc.SmokeTest`
- `examples/Toyopuc.MinimalRead`
- `examples/Toyopuc.DeviceMonitor`
- `examples/Toyopuc.SoakMonitor`
- `README.md`
- `CHANGELOG.md`
- `LICENSE`
- `docs/internal/VALIDATION.md`
- `docs/internal/TESTRESULTS.md`

Confirm that local output is excluded:

- `bin/`
- `obj/`
- `logs/`
- `artifacts/`

## Versioning

Before packaging:

1. Update `<Version>` in [src/Toyopuc/Toyopuc.csproj](src/Toyopuc/Toyopuc.csproj).
2. Update [CHANGELOG.md](CHANGELOG.md) so the released changes are recorded in
   the target version section.
3. Make sure the release tag matches the package version, for example `v1.0.0`.

## Quality Gates

Run these commands locally:

```powershell
dotnet build Toyopuc.sln
dotnet test Toyopuc.sln --no-build
dotnet pack src\Toyopuc\Toyopuc.csproj -c Release
```

If hardware verification is part of the release, also run:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target plus
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target relay-10gx
```

## Packaging

The canonical local release command is:

```bat
release.bat
```

`release.bat` also publishes and includes the Windows monitor single-file executable in the release output directory.

For a standalone DeviceMonitor publish run:

```bat
device_monitor_release.bat
```

For the Windows soak-monitor single-file executable:

```bat
soak_monitor_release.bat
```

Expected release outputs:

- `artifacts\release\<version>\Toyopuc.Net.<version>.nupkg`
- `artifacts\release\<version>\Toyopuc.Net.<version>.snupkg`
- `artifacts\release\<version>\Toyopuc.Net.<version>-dll.zip`
- `artifacts\release\<version>\Toyopuc.DeviceMonitor.exe`
- `artifacts\publish\Toyopuc.SoakMonitor\Toyopuc.SoakMonitor.exe`

## GitHub Actions

Repository workflows:

- `.github/workflows/ci.yml`
  - restore, build, and test on Windows for pushes and pull requests
- `.github/workflows/release.yml`
  - build release artifacts on tag pushes and manual dispatch
  - create or update a GitHub Release for `v*` tags
  - optionally push `.nupkg` and `.snupkg` to NuGet when `NUGET_API_KEY` is configured

Before cutting a tag, confirm the CI workflow is green on the release commit.

## NuGet Readiness

Confirm package metadata in [src/Toyopuc/Toyopuc.csproj](src/Toyopuc/Toyopuc.csproj):

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
5. If `NUGET_API_KEY` is configured, let the workflow publish the `.nupkg` and `.snupkg` to NuGet.
6. If NuGet publishing is still manual, publish them after the GitHub Release artifacts are verified.
