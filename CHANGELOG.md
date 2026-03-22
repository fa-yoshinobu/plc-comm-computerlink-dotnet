# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.2] - 2026-03-22

### Changed
- Renamed NuGet package from legacy `Toyopuc` to `PlcComm.Toyopuc`; updated namespace and assembly name accordingly.
- Unified `Directory.Build.props` with `TreatWarningsAsErrors`, `EnableNETAnalyzers`, and `AnalysisLevel=latest-recommended`.
- Cleaned up `PlcComm.Toyopuc.csproj`: removed redundant `AssemblyName`, `RootNamespace`, `Product`, and `IsPackable` properties; improved `Title`.
- Fixed `README.md` and `USER_GUIDE.md` examples to use correct namespace (`PlcComm.Toyopuc`).

## [0.1.0] - 2026-03-19

### Added
- .NET 9.0 TOYOPUC computer-link client (`ToyopucDeviceClient`) with TCP and UDP support.
- Model-aware addressing profiles and device catalog support.
- Validation CLI, Windows device monitor (`DeviceMonitor`), and scripted hardware validation.
- Release output includes `Toyopuc.DeviceMonitor.exe` under `artifacts\release\<version>`.
- Release automation via `release.bat` and GitHub Actions workflows.
- Hardware verification against TOYOPUC-Plus and Nano 10GX targets.

### Notes
- Initial public release under the `PlcComm.*` package family.
