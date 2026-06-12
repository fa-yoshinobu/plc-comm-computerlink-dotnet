# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Fixed `CMD=98`/`CMD=99` multi-point word addressing: word points now carry monitor byte addresses (manual 3-60/3-61 "byte address N") instead of `CMD=94/95` word addresses. Sparse `ReadMany()` of word devices (including packed bit-device words such as `P1-V000W`) previously read the wrong area and returned incorrect (typically all-zero) data, and sparse `WriteMany()` of word devices previously wrote to the wrong area. This matches the fix in `plc-comm-computerlink-python`, where the defect was verified on real hardware.
- `ReadExtMulti()` / `WriteExtMulti()` word-point addresses are documented as monitor byte addresses; callers passing `CMD=94/95` word addresses must double them.
- Fixed `CMD=A0` CPU-status read to send `A0 00 11 00` and parse `00 11 00` plus the 8 status bytes, matching the manual and R08CPU hardware verification.
- Reduced FR PC10 block I/O chunks to `0x01F8` words (`0x03F0` bytes) so `CMD=C2/C3` requests stay within the documented byte-count limit.
- Added fail-fast protocol guards for oversized single-frame requests instead of silently producing out-of-range or count-wrapped frames. This covers continuous read/write, basic multi-point, extended multi-point, PC10 block, and PC10 multi-point commands.
- Restricted `CMD=94-99` EB extended-No addressing to `EB00000-EB1FFFF`; wider EB access remains on the PC10 route when enabled.

## [0.1.8] - 2026-05-02

### Changed
- Made profile-specific example apps and probe scripts reject omitted profile values instead of falling back to a default TOYOPUC model.
- Refreshed the README/examples guidance to state that profile-driven live tools require an explicit profile.

## [0.1.7] - 2026-05-02

### Changed
- Bumped the library revision and refreshed test package dependencies.

## [0.1.6] - 2026-04-27

### Fixed
- Fixed TOYOPUC address parsing so single-letter areas such as `D` and `U` are not misread as unknown two-letter areas when the address starts with a hexadecimal `A-F` digit.
- Kept unsupported areas as hard errors instead of falling back to another device interpretation.

## [0.1.5] - 2026-04-14

### Changed
- Rebuilt the public Toyopuc docs around a beginner-first user flow and moved maintainer-only material under `internal_docs`.
- Separated local and publish docs build steps and fixed the example/doc regression checks for the new documentation layout.

## [0.1.4] - 2026-04-01

### Changed
- Refreshed the README, user docs, examples, and generated DocFX output after the unified `SingleRequest` and `Chunked` helper split.
- Added regression coverage for atomic single-request writes on program devices so the documented high-level contract stays verified.

## [0.1.3] - 2026-03-28

### Changed
- `ToyopucDeviceClient` high-level 32-bit and float helpers now accept numeric low-level word addresses in addition to string device addresses, matching the Python implementation.
- Transport and high-level layers now cache relay hops, resolved devices, and compiled run plans to reduce repeated parsing and dispatch overhead.
- Async wrappers now run on a per-client exclusive scheduler instead of dispatching every call through plain `Task.Run`.
- TCP receive and trace hot paths now avoid extra allocations during repeated polling and frame capture.
- Documentation and TODO notes were refreshed to match the current `PlcComm.Toyopuc.*` example set and the current CI/analyzer status.
- Added `release_check.bat` to run CI and DocFX generation as one pre-release entry point.
- Added example/doc regression tests so stale sample names and removed helper assets are caught in CI.

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
