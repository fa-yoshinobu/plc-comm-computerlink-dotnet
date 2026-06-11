# Development History

## 2026-06-11 Archived Refactor Plan

The previous `refactor-instructions.md` was archived into this history file.

### Scope

- Library: `PlcComm.Toyopuc` Computer Link implementation for .NET.
- Primary task: extract pure static helpers from `ToyopucDeviceClient.cs` and add direct unit tests for the extracted logic.
- Test-file cleanup was optional and limited to keeping new tests in focused files.

### Contracts To Preserve

- All public types, methods, signatures, defaults, and NuGet-facing API.
- Exact transmitted frame bytes from `ToyopucProtocol` and payload packing helpers.
- Run-plan batching rules, including PC10 block boundaries and FR-specific behavior.
- Cache semantics for resolved devices and planned runs.
- FR write guard behavior, commit/wait semantics, and semantic atomicity from the high-level API contract.
- NuGet package ID, version `0.1.8`, and changelog.

### Debt Notes

- D1: pure payload packing and run-planning logic had no direct characterization tests.
- D2: `ToyopucDeviceClient.cs` concentrated device resolution, run planning, payload packing, transport, and high-level methods.
- D3: the single broad test file was a maintenance issue; new tests were to be split into focused files.
- D4 and D5, including sync/async surface duplication and object-based public APIs, were report-only.

### Planned Verification

- Capture baseline `dotnet` results before edits.
- Add characterization tests using current implementation output as expected values.
- Move pure logic into internal static classes such as `Pc10Payloads` and `DeviceRunPlanner`.
- Run full tests and formatting checks after each extraction.

### Out Of Scope

- Public API changes.
- NuGet metadata or changelog changes.
- Behavior fixes discovered during characterization unless separately approved.
