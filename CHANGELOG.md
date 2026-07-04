# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Entry labels**

- `Release`: Package/version metadata and publishing preparation.
- `Library`: Runtime behavior, public API, protocol handling, or validation in the distributed library.
- `Docs`: README, user guides, generated API docs, or other documentation-only changes.
- `Samples`: Examples, sample flows, sample scripts, or sample applications.
- `Tests`: Test suites, test fixtures, golden vectors, or verification data.
- `Tooling`: Developer/operator command-line tools and helper utilities.
- `CI`: Release checks, workflow scripts, or automation-only changes.

## [Unreleased]

### Changed
- Docs: Added a Gotchas entry clarifying that `ReadNamedAsync` accepts one address per call and should not be used as a multi-address snapshot helper.
- Docs: Removed the per-library troubleshooting/code page; shared Computerlink troubleshooting and code guidance now lives in the PLC Setup Guide.
- Docs: Removed the per-library latest communication verification page and links so user docs stay focused on usage, not verification logs.
- Docs: Removed the manual page-navigation block from Getting Started and rely on site navigation instead.
- Docs: Moved shared supported-register, device-range, and troubleshooting guidance to the common PLC Setup Guide and kept the user docs to Getting Started, Usage Guide, PLC Profiles, and Gotchas.

## [1.1.0] - 2026-06-29

### Changed
- Release: Bumped package metadata to `1.1.0`.
- Library: Multi-targeted the package for `net8.0`, `net9.0`, and `net10.0`.
- Library: Made byte-unit parsing require explicit `L` / `H` suffixes and made `ReadMany` / `WriteMany` / `ReadNamedAsync` reject implicit multi-request splitting before communication.
- Tests: Multi-targeted the library test project for `net8.0`, `net9.0`, and `net10.0`, while keeping sample CLI contract coverage on the `net9.0` sample applications.
- Tests: Updated `Microsoft.NET.Test.Sdk` to `18.7.0`.
- Tests: Added coverage for explicit byte suffix requirements and rejected implicit multi-request `ReadMany` / `WriteMany` / `ReadNamedAsync` calls.
- Docs: Updated the SDK prerequisite guidance for the multi-target package and documented explicit request-boundary behavior.
- Samples: Updated high-level samples to avoid implicit multi-request named reads.
- CI: Installed .NET 8, .NET 9, and .NET 10 SDKs in CI and release workflows.
- CI: Included the `net8.0`, `net9.0`, and `net10.0` build outputs in the release DLL archive.

### Fixed
- Library: Replaced .NET 9-only `Convert.ToHexStringLower` usage with a cross-target helper so the library builds for `net8.0`.
- Library: Removed the remaining defensive bit-in-word `0` fallback in `ReadNamedAsync`; a missing bit index now raises `ToyopucProtocolError`.

## [1.0.1] - 2026-06-25

### Changed
- Release: Bumped NuGet/package metadata to `1.0.1`.
- Docs: Documented that `PlcProfile` / `plcProfile` must be an explicit canonical profile name: missing values, aliases, abbreviations, case variants, and implicit `toyopuc:generic` fallback are rejected.
- Samples: Updated Computerlink sample guidance and high-level sample code to use safer write/restore patterns.

### Fixed
- Samples: Made the high-level sample analyzer-clean without changing the library API.

## [1.0.0] - 2026-06-24

### Changed
- Release: Bumped NuGet and example project metadata to `1.0.0` for the first stable release line.
