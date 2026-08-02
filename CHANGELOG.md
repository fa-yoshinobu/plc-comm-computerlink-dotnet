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

- Library: Send and receive phases now update only their own socket deadline. This preserves the
  opposite direction's active timeout while retaining the single absolute transaction deadline.
- Docs: Corrected generated `init` accessors and TCP `LocalPort` wording, disabled general state-changing examples by default, and synchronized maintainer release/CI descriptions with the executable gates.
- Tests: Added real mutable/init-only generator fixtures, generated-reference CI coverage, and documentation-contract checks for connection options and controlled state changes.
- Library: Synchronous `Open()` now validates its lifecycle generation after DNS resolution and connection, and publishes only a still-current connected socket, so `Close()` cannot leave a late-created TCP or UDP socket open.
- Library: Data-bearing NG responses now require their command to match the active request before the PLC error becomes definitive. A mismatch is malformed, retires the transport, and makes a transmitted state change outcome-unknown; the protocol's no-data special NG form continues to use its command field as the detailed error code.
- Samples: Retargeted all five user examples to `net10.0`; building repository examples now requires the .NET 10 SDK, while the library and test projects continue to target .NET 8, 9, and 10 and maintainer validation tools remain outside the sample policy.
- CI: Added an immutable prior stable NuGet and matching source-commit documentation baseline plus a three-TFM public API diff gate; every difference must match one exact candidate signature classification, and a breaking release must satisfy the recorded next-major policy.
- Tests: Added exact four-category API-difference policy fixtures for all three target frameworks, candidate-signature drift, forbidden prefix rules, incomplete review state, unclassified differences, and release-major enforcement.
- Release: Aligned artifact roles so the registry package contains consumer runtime, native API metadata, license, README, and ecosystem-native examples where applicable while excluding repository tests and maintainer tooling; the GitHub source archive retains tracked non-hardware validation and maintainer inputs.
- Library: Audited profile-bound `ResolvedDevice` inputs: every live read/write path requires exact canonical profile identity with the client before request construction or transport state changes; no base-family or addressing-mode fallback is used.
- Tests: Added profile-mismatch regression coverage for error diagnostics, traffic counters, and trace state.
- Docs: Getting Started now states that the .NET 8, 9, or 10 SDK is required, matching the package target frameworks and CI matrix.
- Docs: README documentation links now include the shared Performance and Choosing a Language pages, and package registry metadata was expanded for discoverability. No functional change.
- CI: GitHub source archives now include the complete test suite and solution-referenced validation tools, and the archive gate extracts each archive and requires its solution build and tests to pass.
- Docs: Generated API documentation now classifies single-request helpers, one-address named/poll reads, and the explicit two-request, locally exclusive but non-PLC-atomic bit-in-word read-modify-write helper.
- CI: The NuGet package gate now restores and runs an isolated net8.0 consumer using only the generated local package.
- CI: The NuGet guard now rejects CI, cache/build, source, maintainer, release-output, and credential-like material in addition to its consumer-file allowlist.
- CI: Source-archive validation can now synthesize the complete current worktree, including intended new files and deletions, instead of testing stale `HEAD` content during pre-commit review.
- Samples: Corrected the high-level write/restore example to restore from the renamed aggregate-read result so the sample compiles in the extracted source-archive gate.

### BREAKING

- Library: Named, aggregate, typed dword/float, bit-in-word, direct, relay, sync, and async generic write paths now reject FR addresses before transport. Migrate intentional FR updates to `WriteFrWorkArea` / `RelayWriteFrWorkArea`; low-level numeric/raw and explicit FR APIs remain available.
- Library: Relay route strings and `FormatRelayHop` are now decimal-only. Migrate hexadecimal route text such as `PA-LB:N20` or `0xAB:0x20` to `P10-L11:N20` or `171:32`.
- Library: Removed `QueuedToyopucDeviceClient` and all queued-only extension overloads. `ToyopucClient` and `ToyopucDeviceClient` now provide the required per-instance arrival-order FIFO contract directly; migrate wrapper variables and constructors to the ordinary client.
- Library: `ToyopucDeviceClientFactory.OpenAndConnectAsync` now returns `ToyopucDeviceClient`. The returned client retains the immutable `ToyopucRoute`, and ordinary high-level async methods automatically use its direct or relay route.
- Library: Extended-area numbers, FR indices, PC10 addresses, bit ranges, write values, and polling intervals now reject values that cannot be represented by their protocol field instead of truncating, wrapping, or accepting non-finite values.
- Library: Fixed-format and relay responses now require their exact command-specific length; trailing bytes and malformed fixed fields that were previously ignored now raise a protocol error.
- Library: Semantic bit-write APIs now accept only Boolean values. Generic callers that pass integer `0`/`1` must migrate to `false`/`true`; raw frame builders continue to use validated wire integers.
- Library: Connection timeouts, retry delays, and polling intervals now share an inclusive maximum of `2,147,483,647` milliseconds. Larger values fail with `ArgumentOutOfRangeException` before transport or timer creation.
- Library: TCP and UDP connections are now IPv4-only. IPv6 literals are rejected before socket creation, and hostnames use the first IPv4 result returned by the resolver; callers using IPv6 endpoints must migrate to an IPv4 address or IPv4-resolving hostname.

### Fixed

- Library: Full and header-trimmed relay requests now share one strict parser, including a zero length-low byte, and every command-specific response validator runs inside the post-send lifecycle boundary. Malformed reads retire the transport and raise `ToyopucProtocolError`; malformed state-changing calls raise outcome-unknown with the protocol error preserved as the cause.
- Library: Explicit direct and relay read aggregates now validate their complete plan before transport and split only when required by protocol capacity, address block, or route boundaries. Every aggregate preserves caller order in one FIFO turn, remains non-atomic across PLC scan instants, and withholds partial results when a later request fails; writes that require multiple requests remain pre-transport errors.
- Library: The transaction deadline now includes lazy connection, send, receive, relay unwrapping, exact command-specific response validation, and decoding. No operation, including a read, is automatically retried after sending may have started; configured retries are restricted to failures proven before send.
- Library: Concurrent async operations now use an explicit FIFO admission queue. Waiting cancellation sends nothing, admitted collections and relay hops are snapshotted, nested compound helpers do not deadlock, and `Close`/dispose reject active and queued work from the retired transport generation.
- Library: Corrected bit-area size and address encoding, including the previous word/bit span mismatch, and use checked arithmetic for every extended-area wire address.
- Library: Random/sparse write duplicate detection now compares the actual wire destination, so aliases that encode to the same address cannot bypass overlap guards.
- Library: Enumerable inputs are materialized once before validation and encoding, preventing one-shot or mutable enumerations from changing a request between passes.
- Library: EOF and malformed post-send responses for state-changing direct and relay operations now report `ToyopucOperationOutcomeUnknownException`; affected fixed-endpoint UDP clients are tainted before reuse.

### Tests

- Tests: Added read-aggregate capacity splitting, cross-route caller order, whole-plan preflight, one-turn FIFO exclusion, and later-split partial-result suppression coverage.
- Tests: Added FIFO ordering across failures, reentrancy, cross-instance independence, queued cancellation, mutable-input snapshot, route immutability, factory return-type, and close-generation retirement coverage.
- Tests: Added boundary, exact-response, finite-value, duplicate-wire-destination, one-shot enumerable, EOF, malformed-response, relay, UDP-taint, IPv6-rejection, and IPv4-resolution regressions.

## [3.2.1] - 2026-07-29

- Release: Bumped .NET package metadata to `3.2.1`.
- Release: GitHub Release drafts now prepend this version's changelog section to generated notes and repair a missing section on workflow reruns.
- Tooling: Pinned canonical profile fixture refreshes to `plc-comm-computerlink-profiles` `v1.0.4`, matching the embedded profile data used by this release.

- Tests: Added focused CPU-status coverage for the documented `AbnormalWriteDuringRun` response bit and its adjacent flags.
- Library: State-changing requests that may have reached the PLC but do not receive a confirmed matching response now raise `ToyopucOperationOutcomeUnknownException`. This covers timeout, disconnect, send failure, malformed or command-mismatched responses, and relay response failures; pre-send failures, reads, and confirmed PLC errors retain their existing classifications. Callers must resolve PLC state before retrying an unknown-outcome operation.
- Tests: Added synchronous, asynchronous, direct, and relay regression coverage for unknown-outcome classification without double-wrapping.
- Library: CMD 98/99 word-address requests now encode byte offsets, injected profile options are isolated per client instead of sharing stale cached state, and retry reconnects preserve trace callbacks.
- Library: Profile catalog address bounds are advisory and do not reject transport sends; wire-format width checks remain.

## [3.2.0] - 2026-07-17

- Release: Bumped .NET package metadata to `3.2.0`.
- CI: Excluded maintainer-only files, tests, and release tooling from generated source archives while retaining the complete example set, and added source-archive contract checks to local, CI, and release gates.

- Library: Added immutable client-lifetime traffic snapshots through `TrafficStats` on direct and queued clients.

## [3.1.0] - 2026-07-13

### BREAKING
- Samples: Minimal, high-level, and multi-PLC examples require explicit host, destination port, and transport; no example substitutes `1025` or TCP for missing endpoint input.
- Library: FR work-area word values require integral values in `0..65535`; negative, overflowing, Boolean, fractional, and string values are rejected before transport instead of being coerced or masked.
- Library: Generic bit/byte/word writes and typed U/S/D/L/F writes reject masking, Boolean/string conversion, fractional conversion, out-of-range values, and non-finite float32 values before transport.
- Library: UDP sockets are connected to the configured PLC endpoint. A fixed-local-port UDP client becomes terminal after an uncertain post-send failure because Computerlink cannot identify stale same-endpoint responses.
- Library: Timeout and retry-delay values exceeding the socket/runtime millisecond range are rejected during configuration.

### Added
- Library: Added `ToyopucPlcProfileDescriptor` and `ToyopucPlcProfiles.GetProfileDescriptors()` for canonical TOYOPUC Computer Link profile metadata.

### Fixed
- Library: Keep protocol-width and unsupported-route validation while treating profile catalog index ranges as advisory application/UI metadata.
- Docs: Fixed generated XML `cref` labels, corrected the release guide, and removed the hand-maintained Getting Started navigation block.
- Packaging: Restored `fa-yoshinobu` as the NuGet author and derive release assets from the centrally defined package version.
- CI: Made release dispatch check out an existing exact tag and verify tag, assembly/package version, and built asset names before upload.

## [3.0.0] - 2026-07-10

### Changed
- Release: Bumped .NET package metadata to `3.0.0`.
- Release: Centralized the package version in `Directory.Build.props` and marked validation probes as non-packable.
- Packaging: Marked samples, CLI, and validation tools non-packable so only the library package is produced.
- Docs: Replaced relative README links with absolute URLs so they resolve on package registry pages.

### BREAKING
- Library: Breaking: `ToyopucConnectionOptions.PlcProfile` is now a required canonical profile property, and the convenience `OpenAndConnectAsync` overload requires `plcProfile`.
- Migration: Set `PlcProfile` on every `ToyopucConnectionOptions` instance and pass `plcProfile` to `OpenAndConnectAsync`; use the canonical profile name for configuration storage.

### Added
- Library: Added `DisplayName` to `ToyopucPlcProfile` and resolved display-name helpers from the profile descriptor.

### Docs
- Docs: Updated the Computerlink API reference for required profile selection and profile display metadata.

## [2.0.0] - 2026-07-06

### BREAKING
- Release: No .NET package ID changed; this package is versioned at `2.0.0` to align with the plc-comm family breaking release wave.

### Changed
- Release: Bumped package metadata to `2.0.0`.
- Docs: Added the plc-comm family package matrix link to the README.
- Tooling: Added common `plc-comm` package tags.

## [1.2.0] - 2026-07-05

### Changed
- Release: Bumped package metadata to `1.2.0`.
- Tooling: Normalized line-ending handling in the canonical profile JSON update script so `-SourceRoot` runs no longer report false changes.
- Library: Synced the embedded TOYOPUC profile fixture to `plc-comm-computerlink-profiles` `v1.0.1`, including canonical `display_name` labels.
- Library: Added `ToyopucPlcProfiles.GetDisplayName(profile)` as the public UI-label helper while keeping stored PLC profile values canonical.
- Docs: Documented the profile display-name helper and canonical-ID storage guidance.
- Tests: Added canonical fixture parity coverage for profile `display_name` values.
- Samples: Added read-only multi-PLC monitoring and JSON config polling recipes with independent reconnect loops, dry-run validation, and long-form CSV output.
- Docs: Added generated .NET API reference from the public assembly surface and XML documentation comments, with CI freshness validation.
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
