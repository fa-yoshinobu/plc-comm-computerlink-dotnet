# Changelog

All notable changes to this project should be documented in this file.

The format is based on Keep a Changelog, and this project follows SemVer.

## [1.0.1]

### Added

- Release output now includes `Toyopuc.DeviceMonitor.exe` directly under
  `artifacts\release\<version>` for GitHub Release attachment.
- Added a pre-tag review checklist in `RELEASE.md` to verify tag/notes/assets
  consistency and DeviceMonitor regression checks.

### Changed

- Stabilized local TCP protocol tests by increasing test client timeout in
  `ProtocolAndClientTests` to reduce CI flakiness.

## [1.0.0]

### Added

- .NET low-level and high-level TOYOPUC computer-link clients.
- Model-aware addressing profiles and device catalog support.
- Validation CLI, Windows device monitor, and scripted hardware validation.
- Release automation via `release.bat` and GitHub Actions workflows.

### Changed

- Documentation was consolidated around the canonical profile names and the
  upstream Python reference repository.

### Notes

- First public release.
