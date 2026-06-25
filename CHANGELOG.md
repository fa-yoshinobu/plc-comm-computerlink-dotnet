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
