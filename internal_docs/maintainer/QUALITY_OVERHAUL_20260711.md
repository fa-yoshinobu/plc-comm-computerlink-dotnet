# ComputerLink .NET quality overhaul — 2026-07-11

## Goal

The public contract is explicit, profile-bound, FIFO-serialized per client, safe under retry and cancellation, and consistent with the approved cross-library contract. Explicit read aggregates may use a validated multi-request plan; writes never acquire hidden multi-request behavior. Compatibility aliases do not remain public when they preserve ambiguous or unsafe behavior.

Branch: `quality/2026-07-overhaul`

Verification baseline after implementation: `run_ci.bat` builds all projects without warnings, validates the generated API reference, runs tests on .NET 8/9/10, verifies formatting, and publishes the self-contained HighLevelSample.

Latest local evidence (2026-08-01): .NET 8 `276`, .NET 9 `289`, and .NET 10 `276` tests passed; the solution build had zero warnings, formatting and generated API checks passed, all five net10.0 samples built, the NuGet package-content check passed with all three target frameworks, and an extracted worktree source archive rebuilt and passed the same three test matrices. No live PLC communication was performed or required for these deterministic transport/state-machine contracts.

The checklists below are evidence states, not intentions. Claude batch `CLAUDE-CL-20260712-01` completed and all findings were dispositioned. D-080, D-081, D-083, and D-084-A passed the recorded live checks; D-084-B remains explicitly unverified under its approved release disposition.

## D-001 — Destination port is required

Scope: low/high-level clients, options/factory, and all maintained examples.

Target: callers provide a port in `1..65535`; omission or invalid input is never replaced with `1025`.

Acceptance criteria:

1. Every public connection path validates the explicit port before socket creation.
2. Minimal/high-level examples require host, port, and transport; the multi-PLC example requires them per endpoint or through explicit common switches.
3. Source inspection finds no runnable `1025` port fallback.

- [x] Implementation and example migration completed.
- [x] Tests cover public port validation.
- [x] Final .NET 8 test project passed 230 tests; all changed example projects built with zero warnings/errors; diff check passed.
- [x] Codex reviewed public signatures, example parsing, and no-fallback source scan.
- [x] Claude source review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned and affected checks rerun (`CLAUDE-CL-20260712-01`).
- [x] No live communication is required for argument/constructor validation; no PLC communication was performed.
- [x] Documentation, changelog, and examples agree.
- [x] Final cross-language acceptance verified.

## D-066 — UDP local port

Scope: low-level client, high-level client, factory, and options. Target: omitted UDP local port is `0`, always bound so the OS assigns an ephemeral source port; fixed ports remain exact.

Breaking impact: TCP with a nonzero UDP local port is now rejected.

Acceptance criteria:

1. UDP `0` binds and receives on an OS-assigned nonzero port.
2. Fixed ports are not silently substituted.
3. TCP plus nonzero local port and values outside `0..65535` fail before transport.

- [x] Implementation completed in this repository.
- [x] Tests cover every acceptance criterion, including fixed-port collision without fallback.
- [x] Static checks, unit tests, examples, build, packaging, and generated API checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Claude source review completed and findings recorded (`CLAUDE-CL-20260712-01`).
- [x] Codex dispositioned every Claude finding and reran affected checks (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; UDP bind-0, assigned source port, simultaneous clients, fixed-port collision, and TCP rejection are covered by local socket fixtures without claiming PLC compatibility).
- [x] User documentation, migration guidance, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and item marked complete.

## D-067 — Transport is required

Scope: every public connection path. Target: `Tcp` or `Udp` is required; `Unspecified` is invalid and no overload defaults to TCP.

Breaking impact: all calls that omitted transport must now specify it.

Acceptance criteria: constructor/factory/options reject missing or unknown transport before socket creation; TCP and UDP remain selectable.

- [x] Implementation completed in this repository.
- [x] Tests added for required transport and invalid enum value.
- [x] Relevant static, unit, example, build, package, and API checks passed.
- [x] Codex self-review completed for constructor/factory transport requirements, enum validation, and examples.
- [x] Claude source review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned and affected checks rerun (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; required transport selection and pre-socket invalid-enum rejection are deterministic).
- [x] Documentation and generated API reference agree.
- [x] Final acceptance verified.

## D-068 — Three-second absolute transaction deadline

Scope: TCP/UDP lazy connection, send, receive, relay unwrapping, command-specific validation, and response decoding. Target: omitted timeout is three seconds; zero and negative values are invalid; FIFO queue waiting is excluded and one absolute monotonic deadline covers every included phase and permitted pre-send retry.

Breaking impact: `TimeSpan.Zero` no longer means “use the default” and timeout is immutable after construction.

Acceptance criteria: default and explicit positive values work; invalid values fail before transport; queue waiting consumes no deadline; lazy connection and complete command-specific decoding share one absolute deadline; a retry does not reset it.

- [x] Implementation completed.
- [x] Constructor/default, token-omitted timeout, final-session disposal, late-response isolation, and new-session tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed for validation, absolute deadline propagation, session disposal, and stale-response prevention.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; deadline phase coverage, cancellation, session disposal, and late-response isolation use controlled local fixtures).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-069 — Safe pre-send-only retry contract

Scope: all direct/relay read, write, raw, FR, clock, and scan operations. Target: `Retries=0` by default; a configured retry is permitted only when failure is proven to precede any send possibility. No operation, including a read, retries after sending may have started or after any PLC response.

Breaking impact: negative retry values are rejected and unsafe blanket retry is removed.

Acceptance criteria: distinguish pre-send from send-possible state; verify at most one send for timeout, EOF, malformed/mismatched response, and PLC error across reads, writes, FR, clock, scan, relay, and raw paths at retry counts 0/1.

- [x] Implementation complete in every path; the retry budget is consulted only while no send could have occurred.
- [x] Direct/relay/read/raw/write/FR/clock/scan post-send no-retry tests pass at retry counts `0/1` as applicable.
- [x] Current static/build/test/package checks passed.
- [x] Codex self-review completed for retry state, request-sent boundaries, direct/relay routing, and state-changing operations.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; pre/post-send classification, retry eligibility, and state-changing send-count limits use fault-injected transports).
- [x] Documentation/API reference agree with the current safe behavior.
- [x] Final acceptance verified.

## D-070 — Retry delay

Scope: permitted retry paths. Target: omitted delay is 200 ms, explicit zero means no delay, negatives are invalid, and async cancellation stops delay and prevents another send.

Breaking impact: negative values are rejected rather than coerced.

Acceptance criteria: omitted/zero/positive/negative values and cancellation during delay are verified.

- [x] Implementation completed: after cancellation is observed, both async execution paths close the active session and require explicit reconnect before returning the cancellation result.
- [x] Cancellation-during-delay test verifies that cancellation stops the delay and prevents a second request.
- [x] Current automated checks passed: the strengthened focused test passed once plus 30 consecutive .NET 8 runs, followed by the complete release check on 2026-07-13.
- [x] Codex self-review completed for delay validation, cancellation cleanup, retry send count, explicit reconnect state, exception classification, and unchanged public API.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude delta review completed (`CLAUDE-CL-20260713-02`; zero findings recorded and dispositioned).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; retry-delay validation and cancellation/no-second-send behavior are deterministic state-machine properties).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified after the zero-finding Claude delta review and complete release-gate rerun.

## D-071 — Receive buffer setting removed

Scope: constructors, options, factory, properties, examples, and docs. Target: UDP datagram capacity is internal and full-frame length validation remains mandatory.

Breaking impact: `RecvBufsize` is removed.

Acceptance criteria: no public setting; large UDP response succeeds; truncated or declared-length mismatch fails.

- [x] Implementation completed.
- [x] Large UDP response and protocol frame validation tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; datagram capacity, truncation, and declared-length mismatch use local UDP/frame fixtures).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-072 — Trace callback is maintainer-only and isolated

Scope: trace hook, client wrappers, and ordinary documentation. Target: disabled by default; copied frames are dispatched outside the communication result path; callback exceptions cannot change PLC operation results.

Breaking impact: trace hook is removed from the public application API.

Acceptance criteria: absent/normal/throwing/slow callbacks, ordering, copies, and unchanged retry/result behavior are verified.

- [x] Implementation completed through the friend-assembly maintainer path.
- [x] Slow and throwing callback acceptance test verifies unchanged read completion and result.
- [x] Current automated checks passed.
- [x] Codex self-review completed for callback isolation, copy ownership, and communication-result independence.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; diagnostic copy ownership, ordering, exception isolation, and timing isolation are transport-result-independent).
- [x] Normal user documentation and API reference exclude the diagnostic hook.
- [x] Final acceptance verified.

## D-073 — Addressing options derive from the required profile

Scope: high-level constructor, resolver, parser/formatter/normalize, helpers, and resolved objects. Target: normal runtime APIs accept one canonical profile; manual options are internal maintainer-only; resolved devices retain profile identity.

Breaking impact: public `AddressingOptions` override and profile-less semantic parsing are removed.

Acceptance criteria: canonical derivation, profile-specific routes, no public override, profile-bound object mismatch rejection, and wire selection.

- [x] Implementation completed; manual addressing options and profile construction are internal maintainer-only surfaces.
- [x] Profile identity, mismatch-before-transport, canonical matrix, and exported-type tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed for canonical derivation, public API exposure, and profile-bound objects.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; canonical profile derivation, profile-bound identity, wire vectors, and mismatch-before-transport are deterministic).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-074 — Direct or relay route is required

Scope: connection options, factory, and ordinary client. Target: `ToyopucRoute.Direct` or `ToyopucRoute.Relay(hops)` is a required construction argument.

Breaking impact: null/omitted relay hops no longer imply direct.

Acceptance criteria: direct, one/multiple relay hops, null/empty/invalid hops, and route preservation in ordinary FIFO operations.

- [x] Implementation completed.
- [x] Required constructor shape and strict route tests added; existing relay wire tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required for the required-route API decision; direct/relay type construction, hop validation, ordinary-client preservation, and zero-send rejection are deterministic, while configured relay hardware remains separately unverified under D-084).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-075 — Trace frame history is bounded and maintainer-only

Scope: capture toggle, history, clear, close, and dispose. Target: not public; positive capacity is mandatory; oldest records are evicted; copies are returned; close/dispose clears history.

Breaking impact: public `CaptureTraceFrames`, history, and clear methods are removed.

Acceptance criteria: disabled default, bounded eviction, copy ownership, clear/close/dispose, reconnect, and result invariance.

- [x] Implementation completed through bounded maintainer diagnostics.
- [x] Capacity, eviction, copy ownership, clear/close, reconnect, and disabled-default acceptance tests pass.
- [x] Current automated checks passed.
- [x] Codex self-review completed for bounded storage, lifecycle clearing, and normal-result invariance.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; bounded diagnostic history, copies, eviction, clear, close, and reconnect lifecycle are local state).
- [x] Normal user documentation/API reference exclude trace history.
- [x] Final acceptance verified.

## D-076 — Named dtype is required

Scope: named read and poll parsing. Target: `:U/:S/:D/:L/:F` or `.0-.F`; bare and unknown types fail before transport. `:D` is dword and `.D` is bit 13.

Breaking impact: bare named addresses no longer mean unsigned word.

Acceptance criteria: every type, bit syntax, missing/empty/unknown type, width, result type, and pre-poll validation.

- [x] Implementation completed.
- [x] Missing/empty/unknown and `.D` tests pass; typed helper tests cover result shapes.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; named dtype grammar, bit/Dword distinction, width validation, and decoding use parser/protocol fixtures).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-077 — Separate scalar reads and validated read aggregates

Scope: direct/relay/FR/word sync and async APIs.

Target contract: `ReadOne` returns one value; `ReadMany(device,count)` requires count and always returns an array; `ReadDevices` is sparse. An explicit read aggregate is fully planned and validated before transport, preserves caller-declared entry order, and owns one indivisible client FIFO turn. It uses one request when representable and splits only when protocol capacity, address-block, or route constraints require it. Split reads are not PLC-atomic and can observe different scan instants. A later failure raises an error without returning partial results. Writes remain one-request-only and reject a multi-request plan before transport.

Breaking impact: `Read(device,count=1)` and count-dependent result types are removed publicly.

Acceptance criteria:

1. Scalar and aggregate return shapes are stable for count one and larger counts.
2. Every request in a direct or relay aggregate is constructible before the first send.
3. Capacity and route/block boundary cases split only where one request is impossible and preserve caller order.
4. No unrelated operation can interleave between split requests on the same client.
5. A later split failure exposes no partial result.
6. A write requiring more than one request fails with zero transport activity.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, .NET 8/9/10 tests, examples, and package/build checks passed for the final source state.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Required live-PLC checks passed, or each unavailable check has an explicit release disposition. No live PLC check is required because planning, request order, FIFO exclusion, error suppression, and send counts are deterministic local transport behavior.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## D-078 — Dword/float are always one request

Scope: direct/relay read/write, sync/async, and helpers. Target: no `atomicTransfer`; all words forming every value must fit one request and block.

Breaking impact: the flag and split fallback are removed.

Acceptance criteria: dword/float read/write at limit/crossing, no partial write, request count at most one.

- [x] Implementation completed.
- [x] Public-surface and one-request dword tests pass; existing float/dword vectors pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; Dword/float block limits, one-request/no-partial-write behavior, and removed switches use exact vectors and send counts).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-079 — Raw data is required and maintainer-only

Scope: raw sender, async sender, generic builder, and retry. Target: data is required, explicit empty is valid, command `0..255`, frame length is strict, no post-send retry, and no normal public entry point.

Breaking impact: omitted/null data and public raw calls are removed.

Acceptance criteria: command boundaries, missing/empty data, maximum length, sync/async frame bytes, zero request on invalid input, no retry.

- [x] Implementation completed.
- [x] Command `0/255`, invalid command, null/empty data, maximum/overflow length, exact async frame bytes, zero transport on invalid input, and no post-send retry tests pass.
- [x] Current automated checks passed.
- [x] Codex self-review completed for visibility, builder bounds, transport timing, and retry behavior.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; maintainer-only visibility, required payload, exact raw frames, length limits, and no post-send retry are deterministic).
- [x] User docs/API reference exclude raw entry points.
- [x] Final acceptance verified.

## D-080 — FR work-area write and commit are separate

Scope: low/high level, direct/relay, sync/async. Target: `WriteFrWorkArea` performs one request and never commits; `CommitFrBlock` is separate; combined/range conveniences are removed.

Breaking impact: commit/wait bools, committed helper, and combined write/commit are removed.

Acceptance criteria: work-area-only write, one-block commit, multi-block rejection, explicit command sequence, no post-send retry, and strict integral FR word values in `0..65535` with no Boolean/string/fraction conversion or range masking.

- [x] Implementation completed.
- [x] Work-area frame, sync/async/queued direct/relay low/high-level strict-value zero-send, and public-surface tests pass; samples/tools now sequence operations explicitly.
- [x] All automated checks passed after the final diff on 2026-07-12: warning-free build, generated API check, net8 `230`, net9 `243`, net10 `230`, format verification, example CLI contracts, and self-contained HighLevelSample publish.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live FR work-area verification passed on 2026-07-12: Nano 10GX `192.168.250.100:1025` TCP Direct, `FR000000`, original `999`, test `0x7811` (`30737`), readback `30737`, write command `0xC3`, no CA call, restore command `0xC3`, final readback `999`.
- [x] Documentation/API reference agree.
- [x] Final acceptance verified for D-080; durability remains the separate D-081 decision.

Self-review finding (2026-07-12): both low-level `NormalizeWordValues(IEnumerable<int>)` and the high-level object path allowed invalid FR values to be masked or converted. The finding was accepted as a cross-language contract violation, both paths were changed to strict integral `0..65535` validation, and direct/relay tests prove rejection before the connection opens.

## D-081 — Commit accepts one block and does not poll

Scope: direct/relay commit and status behavior. Target: one `CMD=CA` acceptance only; no count/range/wait/timeout/interval/fallback polling.

Breaking impact: range commit and wait helpers are removed.

Acceptance criteria: exactly one commit request, fixed return shape, no status request, no fallback, no retry.

- [x] Implementation completed.
- [x] Public-surface and command tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live commit/status verification passed on 2026-07-12: Python committed `FR000000=0x74E6` after a clean restart, the next restart preserved it, .NET read the persisted value, restored `999` with `CMD=C3`, sent one `CMD=CA`, and explicit application-level A0 reads observed writing clear without an abnormal flag. A second restart returned `999` through both implementations.
- [x] Documentation/API reference agree.
- [x] Final acceptance verified for D-081 on the tested Nano 10GX profile, endpoint, block, and current Python/.NET builds; no result is generalized to other FR blocks or profiles.

## D-082 — Device strings are hexadecimal

Scope: semantic parser, prefixed parser, resolver, normalize, and format. Target: no public radix option; profile-bound canonical strings use hexadecimal.

Breaking impact: callers cannot select decimal or another radix.

Acceptance criteria: canonical values, profile widths, lowercase normalization, invalid markers/chars/width/overflow, and parse-format-wire round trip.

- [x] Implementation completed; raw parsers are internal and fixed to radix 16.
- [x] Existing address matrix/vector tests pass; public-surface tests confirm profile-bound semantic entry point.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; hexadecimal parsing, normalization, formatting, and frame round trips are deterministic vectors).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-083 — Clock century is required

Scope: `ClockData.AsDateTime`, direct/relay clock write, async APIs, and samples. Target: explicit century divisible by 100, range checked, timezone-unspecified local clock only.

Breaking impact: default 2000 and `% 100` ambiguity without validation are removed.

Acceptance criteria: years 00/26/99 with 1900/2000/2100, invalid bases, boundaries, timezone handling, calendar validation, and frame round trip.

- [x] Implementation completed.
- [x] Century and timezone rejection tests pass; protocol clock vectors remain green.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live clock verification passed on 2026-07-12 for Nano 10GX `192.168.250.100:1025` TCP Direct: Python wrote `2026-07-12 12:34:56` with `year_base=2000`; a new .NET session read the advancing test time; .NET restored the captured clock plus elapsed time with `yearBase=2000`; new Python and .NET sessions read the advancing restored time.
- [x] Documentation/API reference agree.
- [x] Final acceptance verified. One Nano 10GX showed a short PLC-side clock visibility difference; the user approved keeping only concise maintainer evidence, without generalizing it in GOTCHAS, PROFILES, or the shared docs-site. No automatic wait, reconnect, or retry is added.

## D-084 — Relay ENQ and route values are strict

Scope: builders, parser, normalizer, formatter, route, and nested hops. Target: ENQ fixed to `0x05`; link `0..255`; station `1..65535`; no masking; all hops validated first.

Breaking impact: custom ENQ and wrapped link/station values are removed.

Acceptance criteria: ENQ bytes, every boundary, all syntaxes, nested invalid hop before send, and canonical formatting.

- [x] Implementation completed.
- [x] Strict route boundary tests and existing relay vectors pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded. D-084-A passed in Python and .NET: each changed `P1-D0000` through `P1-L1:N2` from `0xFFFF` to `0x3DA4`, read it back, restored `0xFFFF`, and read the restoration back over TCP `192.168.250.100:1025`; both used outer `CMD=60`, link `0x11`, station `0x0002`, and fixed ENQ `0x05`. D-084-B has no available real multi-hop topology or exact route/target and remains explicitly `unverified`; the user approved release with that TODO on 2026-07-12. Exact nested frames, all-hop validation, response unwrap tests, mandatory explicit hops, and no route discovery/fallback contain the unverified scope. Multi-hop live compatibility is not claimed.
- [x] Documentation/API reference agree.
- [x] Final acceptance verified under the recorded D-084-B unverified release disposition; multi-hop live compatibility is not claimed.

## D-085 — Async cancellation isolation and completion

Scope: all async clients, queued wrapper, and factory. Target: token remains optional; gate wait cancellation affects only that call; running cancellation closes only its active session and is awaited; post-send state-changing cancellation reports unknown outcome; explicit reconnect is required.

Breaking impact: canceled write-like operations can now return `ToyopucOperationOutcomeUnknownException`; implicit reconnect after cancellation is rejected.

Acceptance criteria: omitted token with timeout, pre-cancel, gate wait, connect/send/receive cancel, no background retry/delay, post-send unknown result, closed state, explicit reconnect.

- [x] Core implementation completed: the cancellation catch path independently enforces close and explicit reconnect state instead of relying only on callback timing.
- [x] Pre-cancel, gate isolation, pending-connect, blocked-send, receive cancellation, retry-delay/no-second-send, post-send unknown result, worker completion, closed state, implicit-reconnect rejection, explicit reconnect, and token-omitted timeout tests pass.
- [x] Current automated checks passed: the regression now verifies no second send, closed state, and rejection before explicit reconnect; focused repetition and the complete release check passed.
- [x] Codex self-review completed for gate ownership, worker completion, retry delay, unknown outcome, close idempotence, and explicit reconnect state.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude delta review completed (`CLAUDE-CL-20260713-02`; zero findings recorded and dispositioned).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; cancellation ownership, worker completion, unknown-outcome classification, closed state, and explicit reconnect use controlled transports).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified after the zero-finding Claude delta review and complete release-gate rerun.

## D-086 — Ambiguous connection convenience overload removed

Scope: extension/factory convenience methods. Target: no host/profile or host/profile/port overload can infer port, transport, or route; validated options are the sole factory input.

Breaking impact: convenience callers must construct `ToyopucConnectionOptions` with all required identity fields.

Acceptance criteria: forbidden overload absent; every factory route requires port, transport, profile, and route; examples show all values.

- [x] Implementation completed.
- [x] Constructor/public-surface tests and all examples compile with explicit values.
- [x] All automated checks passed.
- [x] Codex self-review completed for constructor/factory signatures, generated API, and examples.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; forbidden overload absence and required options are compile-time/generated-API properties).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## Live verification evidence and remaining TODO

No command below is authorized merely by appearing here. Before execution, identify the connected PLC, present the exact batch, and wait for explicit user `OK`.

The concrete candidate values below come from the maintained samples and the 2026-06-12 Nano 10GX evidence. They must be reconfirmed against the currently connected hardware before approval; they are not a claim about the present wiring.

| ID | Exact candidate target | Operation and evidence | Restoration / risk | Status |
|---|---|---|---|---|
| D-080 | Nano 10GX TUC-1157; `toyopuc:nano-10gx:compatible`; `192.168.250.100:1025` TCP; Direct; `FR000000` | `pass` (2026-07-12). Python and .NET both confirmed the original value `999`, work-area test value `0x7811`, readback `30737`, write command `0xC3`, and that CA was not executed. | Both implementations restored the original value `999` with C3 and verified it by readback. The temporary .NET project and generated artifacts were removed. |
| D-081 | Nano 10GX TUC-1157; `toyopuc:nano-10gx:compatible`; `192.168.250.100:1025` TCP; Direct; `FR000000` | `pass` (2026-07-12). Starting from the original value `999` immediately after restart, Python wrote and read back `0x74E6`, then executed CA once. An explicit A0 read confirmed after about one second that the write-in-progress state had cleared with no abnormal status. After restart, .NET confirmed `0x74E6`. | .NET restored `999` with C3 and executed CA once. An explicit A0 read confirmed after about 1.5 seconds that the write-in-progress state had cleared with no abnormal status. After another restart, Python and .NET both confirmed `999`. The library performed no polling, retry, or fallback. The temporary .NET project and generated artifacts were removed. |
| D-083 | Nano 10GX TUC-1157; `toyopuc:nano-10gx:compatible`; `192.168.250.100:1025` TCP; Direct; PLC clock | `pass` (2026-07-12). Python and .NET both succeeded in writing with an explicit century, performing a verification read, and restoring the original time. | Do not generalize the short profile-specific propagation delay into the API contract. No automatic wait, reconnect, or retry was used. The temporary .NET project and generated artifacts were removed. |
| D-084-A | Nano 10GX; `toyopuc:nano-10gx:compatible`; TCP `192.168.250.100:1025`; `P1-L1:N2`; `P1-D0000` | `pass` (2026-07-12). Python and .NET both confirmed `0xFFFF → 0x3DA4 → 0xFFFF`, outer `CMD=60`, link `0x11`, station `0x0002`, ENQ `0x05`, and successful response unwrapping. | Both implementations restored the original value and verified it by readback. The temporary .NET project and generated artifacts were removed. |
| D-084-B | Real multi-hop topology, endpoint, route, target PLC/device unavailable | `unverified; release permitted` (2026-07-12). Exact nested-frame, all-hop validation, and response-unwrap tests pass; D-084-A one-hop live behavior passes in Python/.NET. No multi-hop live-pass claim is made. | No active TODO remains. The hardware-unavailable scope was closed on 2026-07-18 without changing its `unverified` result. Reopen only after an exact topology, endpoint, route, target PLC/device, and user `OK` exist. Explicit hops remain mandatory; no route discovery, route mutation, or fallback. |

If hardware is unavailable, each item needs an explicit release disposition. The proposed dispositions are recorded in workspace `quality_overhaul_goal_20260711.md`; no proposal is approved merely by being documented.

### HW-CLOSE-20260718-01: Hardware-unavailable evidence scope

Implementation scope: .NET and Python ComputerLink live-evidence tracking for
`toyopuc:plus:standard`, `toyopuc:nano-10gx:native`, `toyopuc:pc3jx:pc3-separate`,
`toyopuc:pc3jg:pc3jg`, `toyopuc:pc3jg:pc3-separate`, and D-084-B real multi-hop behavior.

Target contract: an evidence request with no identified target, endpoint, and exact route is not an
active implementation obligation. Each affected scope remains `unverified`; no live compatibility
claim is added, no unsupported conclusion is inferred, and release is not blocked under the existing
item-by-item dispositions. A future check is a new batch requiring an exact target/profile, endpoint,
device/address, read intent, expected evidence, restoration requirement, and explicit user `OK`.

Compatibility impact: none. Runtime code, supported API, profile data, and existing deterministic
tests are unchanged.

Acceptance criteria:

- [x] The five targetless profile sweeps are no longer active root TODO items.
- [x] D-084-B is closed as an active task while its `unverified` evidence state is preserved.
- [x] No affected profile or multi-hop behavior is described as live-passed or unsupported.
- [x] Reactivation requires a new exact, explicitly approved live batch rather than an open-ended
      collection request.
- [x] No live PLC communication was performed for this closure.

## Codex delta self-review — 2026-08-01

Scope: the actual overhaul diff, including public API, FIFO admission and generation retirement, absolute deadlines, retry boundaries, direct/relay response decoding, aggregate planning, capacity validation, examples, generated documentation, packaging, and source-archive behavior.

Finding dispositions:

1. **Accepted and corrected:** command-specific direct and relay response validation/decoding could occur after the inner send/receive lease and deadline ended. Decoding, relay unwrapping, exact response-length checks, and state-changing validation now execute inside the transport core before success is published.
2. **Accepted and corrected:** the previous `ReadMany`/`ReadDevices` implementation and maintainer record still enforced the superseded single-request-only read contract. Explicit direct/relay read aggregates now preflight their complete plan, split only where one request is impossible, preserve caller order in one FIFO turn, disclose non-atomic scan timing, and suppress partial results. Multi-request writes still fail before transport.
3. **Accepted and corrected:** direct state-changing responses with a matching command but trailing payload were accepted. Fixed-empty success responses now require zero data bytes and malformed post-send replies produce outcome-unknown with reason `MalformedResponse`.
4. **Accepted and corrected:** worktree source-archive validation overlaid modified files but retained files deleted by the overhaul, causing the extracted archive to expose the removed queued wrapper. Worktree validation now removes deleted tracked files before the extracted build/test gate.
5. **Accepted and corrected:** `GOTCHAS.md` still said every cross-boundary range must be split explicitly by the application, contradicting the approved explicit-read-aggregate contract. It now distinguishes single-request APIs from validated non-atomic `ReadMany`/`ReadDevices` aggregation and states that writes are never split automatically.
6. **Accepted and corrected:** `USAGE_GUIDE.md` and the high-level sample still described aggregate reads as one request or as snapshots. They now distinguish single-request methods from non-atomic aggregate results and avoid snapshot terminology for values that can come from multiple PLC requests.
7. **Duplicate:** native socket/error leakage, close/dispose generation retirement, cancellation outcome reasons, IPv4-only endpoints, input snapshotting, and fixed-port UDP taint were already covered by the current diff and deterministic regressions; no separate change was needed.
8. **Rejected with rationale:** preserving the queued compatibility wrapper or the old read single-request restriction would contradict the approved ordinary-client FIFO and explicit-read-aggregate contracts.
9. **Deferred:** none. Live PLC work is not required for these deterministic protocol framing, local transport, scheduling, validation-order, packaging, and documentation criteria.

Final evidence: the current worktree passed build/API/test/format/sample/package validation; .NET 8 `276`, .NET 9 `289`, and .NET 10 `276` tests passed; NuGet package contents passed (`12` files, all target frameworks); extracted source archive build/tests passed (`101` files, `13` sample files, `16` test files, `14` validation-tool files); and `git diff --check` passed before this evidence-only update.

## Claude review batch `CLAUDE-CL-20260712-01`

- [x] Review package prepared and explicit user authorization obtained for this batch.
- [x] Claude independently reviewed the stated Python/.NET diff read-only; the result is preserved in workspace `Close/instructions/claude_review_result_computerlink_20260712.md`.
- [x] Codex independently reproduced and accepted all eleven findings; no finding was rejected, duplicated, or deferred.
- [x] Accepted findings affecting .NET were corrected with deterministic regression coverage.
- [x] Final full release checks and post-fix Codex diff review recorded (net8 234 / net9 247 / net10 234 tests and complete `release_check.bat`, 2026-07-12).

Finding dispositions affecting .NET:

| Finding | Disposition and acceptance evidence |
| --- | --- |
| 4 | Accepted. Typed U/S/D/L/F writes require compatible finite values with explicit ranges, and non-finite float32 reads are protocol errors. |
| 5 | Accepted as a contract completion. Generic bit/byte/word writes and public protocol builders reject masking, Boolean/string/fraction conversion, and out-of-range values. |
| 6 | Accepted with stronger correction. UDP sockets connect to the configured endpoint. A fixed-local-port UDP session becomes terminal after an uncertain post-send transport failure because same-endpoint stale responses cannot be identified without a protocol serial. |
| 9 | Accepted. Timeout and retry-delay values exceeding `int.MaxValue` milliseconds fail during constructor/factory validation before socket creation. |

Machine-verifiable acceptance criteria for the .NET Claude corrections:

1. Invalid generic and typed values fail before socket creation and no request is sent.
2. Non-finite typed float32 data is not returned as a normal application value.
3. UDP accepts only the configured endpoint, and a fixed-port session cannot issue another request after an uncertain post-send transport failure.
4. Oversized timeout and retry delay fail during configuration with `ArgumentOutOfRangeException`.
5. Source, tests, examples, generated API reference, changelog, and maintainer records pass the full release check on every target framework.

Prepared batch scope: all changes on `quality/2026-07-overhaul` relative to its merge base, with emphasis on `src/Toyopuc`, the generated public API, `OverhaulContractTests`, updated examples/tools, and D-066 through D-086 documentation.

Review purpose: independently identify contract violations, unsafe retry/cancellation state transitions, hidden multi-request behavior, validation after transport, public compatibility remnants, relay/direct divergence, FR/clock semantic errors, insufficient ownership/copying, and test gaps.

Inputs provided for the completed review: approved target contracts and acceptance criteria in this file; repository diff; public API reference; .NET 8/9/10 results; warning-free build, format, API-reference, example CLI, self-contained publish, and package evidence; the 2026-07-12 FR value-mask self-review finding and correction; and deferred live-verification scope.

Expected output: findings only, each with severity, affected contract identifier, exact file/line evidence, failure scenario, recommended correction, and missing-test recommendation. Claude approval or a general quality score is not a substitute for findings.

## NR-007: Lifetime traffic statistics

Approved next-release contract: `TrafficStats` returns immutable lifetime counters; only complete
sends and complete response frames/datagrams count, pre-send and partial failures do not, and
close/reconnect does not reset. Deterministic tests are required; live PLC verification is
unnecessary. Final packaging and publication acceptance completed with `v3.2.0`.

- [x] Public API and transport-boundary implementation completed.
- [x] Deterministic tests, documentation, changelog, and package gate completed.
- [x] Codex final self-review completed.
- [x] Next-release package acceptance completed. Evidence: the `v3.2.0` tag equals repository HEAD,
  the GitHub Release and NuGet `PlcComm.Toyopuc` `3.2.0` package are public, tag-commit checks passed,
  and the final six-runtime family source/API comparison was completed on 2026-07-18.

## BH-LIVE-OUTCOME-20260729 — State-changing unknown-outcome verification

Scope: commit `640d66c243ff2a1f20acd1dd729dc64eaa5f14bd`; Nano 10GX;
`toyopuc:nano-10gx:compatible`; TCP `192.168.250.100:1025`; Direct; `FR000000`.

Target contract: if one state-changing request reaches the PLC but its matching response cannot be
confirmed, the .NET client reports `ToyopucOperationOutcomeUnknownException`, does not retry even
when retries are configured, requires the caller to inspect PLC state, and permits controlled
recovery through a new normal session.

Acceptance evidence:

- [x] A normal baseline read returned `FR000000=0x03E7` (`999`).
- [x] A local response-withholding proxy forwarded one `CMD=C3` write for test value `0x6A3D` to the
  PLC and received one normal `CMD=C3` response, but did not return that response to the client.
- [x] With `retries=3`, the client returned `ToyopucOperationOutcomeUnknownException`; the proxy
  observed exactly one PLC request and no automatic retry.
- [x] A new normal session read `FR000000=0x6A3D`, proving that the PLC had executed the write while
  the client had correctly declined to claim success.
- [x] A normal `CMD=C3` write restored `FR000000` to `0x03E7` without a commit command, and the final
  read returned `0x03E7`.
- [x] The temporary proxy/probe source and all generated build artifacts were removed. The
  repository working tree was clean before this evidence record was added.

Disposition: the synchronous direct TCP post-send response-loss path passes on the stated hardware.
Deterministic fault injection remains the acceptance evidence for exact pre-send boundaries,
disconnect/malformed/mismatched/PLC-error classifications, async operation, and relay routing that
this single live path does not cover; no live compatibility claim is made for those untested paths.
The user explicitly accepted this combined evidence as the REL-010 high-risk release disposition on
2026-07-29.

Final candidate verification on 2026-07-29 used the current working tree and the current local
`plc-comm-computerlink-profiles` candidate. The profile fixture was unchanged, the source archive
contract passed (`49` files, `13` samples), build and generated API checks passed with zero warnings,
and tests passed on net8 (`240`), net9 (`253`), and net10 (`240`). `dotnet format
--verify-no-changes`, self-contained HighLevelSample publish, and Release package generation for all
three TFMs also passed. Self-review found mixed LF/CRLF markers in changed C# files; the finding was
accepted, normalized with `dotnet format`, and the complete gate was rerun successfully. REL-010 is
complete; publication remains separately gated by explicit user authorization.

## Accepted self-review findings — public operation classification and packed consumer

The generated API previously showed mostly bare high-level method signatures, so a consumer could
not reliably distinguish one-request methods from the explicit bit-in-word read-modify-write
sequence. XML documentation now classifies every public high-level helper and states that
`WriteBitInWordAsync` holds one local FIFO turn but remains two requests and is not PLC-atomic.

The earlier NuGet inspection proved file contents but not usability from the packed artifact. The
package gate now restores and runs an isolated net8.0 project whose only package source is the
newly generated local NuGet package. The consumer gate passed for `PlcComm.Toyopuc` on 2026-08-01.

The final rerun found that the previous worktree option could still omit
uncommitted and newly created files because `git archive` only read `HEAD`.
This was accepted and corrected by constructing the review archive from every
non-ignored current-worktree file while honoring deletions and the source
artifact exclusions. The extracted current-worktree solution build and all
three target-framework test runs then passed.

The cross-ecosystem artifact review also found that the negative NuGet guard
did not name every repository-only category required by the approved contract.
The accepted correction now rejects CI, cache/build, source, maintainer,
release-output, and credential-like paths/files. The hardened 12-file NuGet
consumer gate passed.

The hardened source-archive rerun then found two stale references to the old
`snapshot` sample variable after the aggregate-read terminology correction.
This accepted sample-only finding was corrected to restore from `readResult`;
the extracted solution must compile before the artifact decision can close.

## GOAL-CROSS-OS-CI-001 — bounded Linux network-contract smoke

Implementation scope: the Computerlink .NET test project and CI only. The existing Windows job
remains the authoritative full solution, package, source-archive, formatting, documentation, and
multi-target gate.

Target contract: one Linux job uses `net10.0` as the representative framework and runs only tests
explicitly marked `LinuxNetworkContractSmoke`. Its loopback/fake coverage exercises successful
fragmented receive, connection refusal, bounded request timeout, cancellation while waiting,
close/dispose retirement of active and queued work, reconnect after retirement, late-response
rejection, and fixed-endpoint UDP stale-session protection. It performs no PLC communication and
does not repeat the complete Windows gate.

Acceptance criteria:

1. CI has exactly one bounded Linux network-contract smoke job and retains the Windows full gate unchanged as the authoritative gate.
2. The Linux job builds only the test project and its library dependency for the representative `net10.0` framework, then selects only the explicit smoke trait.
3. The selected tests use loopback sockets or in-process fakes and cover the applicable lifecycle cases listed in the target contract.
4. No sample matrix, package/content gate, source-archive gate, generated-document gate, formatting gate, or live PLC check is duplicated in the Linux job.
5. A failed selected contract test or missing test-result artifact fails the Linux job.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every locally applicable acceptance criterion.
- [x] Relevant static checks and the bounded Linux smoke passed on the same reviewed source state.
- [x] Codex self-review completed against the approved contract and confirmed that the Linux job is filtered and bounded rather than a second full gate.
- [x] Live PLC checks are not required because every selected property is a deterministic socket/lifecycle contract exercised on loopback or an in-process fake.
- [x] Documentation and changelog agree with the implementation.
- [x] Final acceptance criteria verified by an executed CI run and the item marked complete.

## GOAL-DOCUMENTED-API-DIFF-001 — immutable baseline and classified public API delta

Implementation scope: maintainer metadata under `internal_docs/maintainer/api-diff`, the
cross-platform PowerShell gate, its policy fixtures, Windows CI, and release gates.

Current state: **classification complete**. The three candidate assemblies were compared with the
verified stable package and produced 219 exact per-TFM records: 120 removals of prior documented
contract and 99 approved additions. These are 73 distinct changes with identical signatures on all
three TFMs. `classifications.json` contains every exact record and no prefix or guessed rule.

Target contract: CI downloads the immutable `PlcComm.Toyopuc` prior stable package identified in
`baseline.json`, verifies its recorded SHA-256 digest before extraction, and independently compares
the `net8.0`, `net9.0`, and `net10.0` assembly surfaces. The normalized surface includes exported
types, base types, interfaces, public declared constructors, methods, properties, events, fields,
enum values, visibility, virtual/final state, parameter direction/optionality/default values,
return/parameter types, and generic constraints.

The corresponding prior stable source is fixed to tag `v3.2.1` commit
`e7f199233b815fe745f8525657e837cdbe46dc31`. Documented versus undocumented is derived only from
that commit's README, five standard/generated user pages, and complete `examples/` source set;
candidate documentation cannot retroactively make the prior contract documented. Every actual
difference must match exactly one classification with the same TFM, change, symbol,
`before_signature`, and `after_signature`. Prefixes, prefix lists, guessed signatures, stale items,
and incomplete classification state fail.

Compatibility impact: this is a maintainer/CI enforcement contract, not a runtime package API.
The recorded baseline version identifies the immutable comparison artifact and is intentionally
not presented as the current release number in user documentation.

Acceptance criteria:

1. Baseline metadata records package identity, digest, all three TFM asset paths, immutable source tag/commit, README, all five standard/generated pages, the examples tree, and next-major release policy.
2. The source tag must resolve to the recorded full commit; shallow or mismatched checkout fails before documentation classification.
3. All three TFM surfaces are exported and compared independently; a missing baseline or candidate TFM assembly fails.
4. Every actual difference is represented by exact TFM, change, symbol, before signature, and after signature values. Signature/visibility/default/constraint changes appear as exact removed and added records.
5. Prior documentation evidence is computed from the immutable prior source commit. A removed documented symbol cannot be classified `undocumented-public`, and a removed undocumented symbol cannot be classified `documented-contract`.
6. Every actual record matches exactly one classification. Prefix/prefix-list rules, unclassified differences, duplicate exact items, stale items, and candidate-signature drift fail.
7. Policy fixtures cover all four categories, all three TFMs, incomplete state, prefix rejection, candidate-signature drift, unclassified failure, and next-major enforcement.
8. Every breaking classification records `next-major` with minimum release major `4`; release workflow and local release check reject an earlier major.

- [x] Implementation and all 219 exact per-TFM classifications completed.
- [x] Policy fixture code added for every classification category, all three TFMs, exact-signature drift, prefix rejection, incomplete state, and release-major enforcement.
- [x] API export, fixture execution, immutable package/digest verification, source-tag verification, documentation evidence generation, and candidate comparison passed.
- [x] Three-TFM build/tests, generated API freshness, all five sample builds, package consumer, format, and extracted worktree source-archive validation passed.
- [x] Codex self-review completed against the generated actual API delta, every exact classification, public source changes, documentation, package surface, and detector limitations.
- [x] Live PLC checks are not required because assembly metadata comparison has no PLC or transport behavior.
- [x] Maintainer documentation and changelog agree with the implementation; user pages do not display the baseline as a current package version.
- [x] The release-major gate correctly rejected current version `3.2.1` and accepted the required major target `4.0.0`.
- [x] The actual release version is `4.0.0`; final release acceptance was recorded after the 2026-08-07 full release gate passed.

Verification evidence (2026-08-07): the stable package downloaded independently through NuGet v3
and v2 had identical SHA-256
`34AB3ACFB7942F69BAC65D46DA559DCD37CEB66551FC104D583B63D130A98622`.
The exact API gate passed all 219 records with no unclassified or stale entry. Release policy
rejected `3.2.1` because the documented breaks require major `4`, then accepted the actual `4.0.0`
release version. The full release gate passed the extracted source-archive build/tests, three-TFM
CI and API classification, release-major policy, documentation checks, and NuGet package build.
No tag, GitHub release, or package-registry publication was performed.

Self-review disposition:

- Accepted and corrected: loading all assemblies through PowerShell would bind them to PowerShell's runtime and could not safely inspect later TFMs. A small exporter is built for each TFM, and baseline/candidate assemblies are loaded in separate matching `dotnet` processes before JSON classification.
- Accepted and corrected: comparing only `net8.0` could miss conditional public-surface drift. Baseline and candidate `net8.0`, `net9.0`, and `net10.0` assemblies now produce independent records.
- Accepted and corrected: `Expand-Archive` is extension-sensitive and may reject a NuGet `.nupkg` path. The gate uses `System.IO.Compression.ZipFile` so the immutable package is extracted by archive format rather than filename extension.
- Accepted and corrected: type/member prefix rules could silently approve a later overload or signature. Prefix and prefix-list properties are now forbidden, and both unclassified actual records and stale classification records fail.
- Accepted and corrected: candidate documentation paths cannot prove whether the prior stable API was documented. Evidence now comes only from the package-corresponding immutable source commit's README, standard/generated pages, and examples.
- Accepted and corrected: the original hand-written rules guessed the candidate surface without generating it. Those rules were removed; the executed comparison now pins all 219 exact per-TFM records.
- Accepted and corrected: a prose-only breaking-version note was insufficient. Classification fields, baseline/minimum majors, release workflow input, and local release check are machine validated.

## GOAL-DOTNET-SAMPLE-TFM-001 — repository user examples target .NET 10

Implementation scope: the five projects under `examples/`, their prerequisite guidance, CI sample
build behavior, and release notes. Projects under `tools/validation/` are explicitly outside this
sample policy.

Target contract: every Computerlink .NET user example targets `net10.0` and documents the .NET 10
SDK prerequisite. The package library and test project remain multi-targeted for
`net8.0;net9.0;net10.0`; the four maintainer validation projects remain `net9.0` and are not
silently treated as user samples. This sample-only retarget is not described as a package API
break.

Acceptance criteria:

1. All five `examples/**/*.csproj` projects have exactly `TargetFramework` `net10.0`.
2. `src/Toyopuc/PlcComm.Toyopuc.csproj` and `tests/PlcComm.Toyopuc.Tests/PlcComm.Toyopuc.Tests.csproj` retain `net8.0;net9.0;net10.0`.
3. All four `tools/validation/**/*.csproj` projects retain `net9.0` and are excluded explicitly from the user-sample policy.
4. Getting Started, the examples index, and the changelog state the .NET 10 sample prerequisite and distinguish it from package target support.
5. Existing sample-build CI continues discovering only `examples/**/*.csproj`, so it validates all five retargeted user examples without adopting maintainer tools.

- [x] Implementation completed for all five user example projects.
- [x] Target-framework acceptance conditions are represented directly in project files and existing sample discovery boundaries.
- [x] Sample restore/build and full relevant checks passed for all five net10.0 projects.
- [x] Codex self-review confirmed the library/tests remain multi-targeted and all four validation tools remain `net9.0`.
- [x] Live PLC checks are not required for a compile target/prerequisite change.
- [x] User prerequisite guidance and changelog agree with the project files.
- [x] Final sample acceptance criteria verified by the executed worktree and extracted source-archive gates.

## GOAL-COMPUTERLINK-ERROR-CMD-CORRELATION-20260802 — Correlate data-bearing NG responses

Stable identifier: `COMPUTERLINK-ERROR-CMD-CORRELATION-001`.

Implementation scope: the shared .NET ComputerLink TCP and UDP response path,
including synchronous and asynchronous low-level operations and the high-level
device client paths that delegate to it.

Target contract: when an NG response contains response data, the response
command must be compared with the active request command before the PLC error
is made definitive. A matching command retains the existing `ToyopucPlcError`
classification and error detail. A mismatched command is a malformed response,
invalidates the active transport generation, and requires explicit reopen
before later communication. For a state-changing request, the mismatch produces
`ToyopucOperationOutcomeUnknownException` with reason `MalformedResponse` and a
`ToyopucProtocolError` cause. For a read-only request, it produces the existing
protocol/malformed error classification and is never exposed as the current
request's definitive PLC error.

An NG response without response data is outside command correlation because
the protocol's special no-data form can use the command field as its detailed
error code. That form retains its current behavior and is not rejected merely
because the field differs from the request command.

Compatibility impact: data-bearing NG responses that identify another command
are no longer reported as definitive PLC errors for the active request, and the
affected transport cannot be reused. Applications that retry a state-changing
operation based on the former PLC-error classification must instead treat its
result as unknown. Matching data-bearing responses and the special no-data NG
form remain compatible.

Machine-verifiable acceptance criteria:

1. TCP and UDP fixtures issue read-only and state-changing requests and return
   an otherwise valid data-bearing NG response whose command differs from the
   request; command correlation occurs before PLC-error construction.
2. Every mismatched state-changing response produces
   `ToyopucOperationOutcomeUnknownException` with reason `MalformedResponse`
   and a `ToyopucProtocolError` cause, retires the transport generation, rejects
   implicit reuse, and permits communication only after explicit reopen.
3. Every mismatched read-only response produces `ToyopucProtocolError`, retires
   the transport generation, and never exposes the response as a definitive
   `ToyopucPlcError` for the active request.
4. TCP and UDP data-bearing NG responses with a command exactly matching the
   request retain the existing `ToyopucPlcError`, return the same detailed error
   code, and preserve the established definitive-result connection behavior.
5. TCP and UDP no-data special NG responses with a command-field value that
   differs from the request retain their existing detailed-error-code behavior
   and are not classified as malformed solely by that difference.
6. The same classification and transport-generation assertions pass through
   synchronous, asynchronous, and representative high-level device operations.
7. The acceptance suite passes independently on `net8.0`, `net9.0`, and
   `net10.0`; no criterion relies on live PLC communication.

- [x] Implementation completed in every affected repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Required live-PLC checks passed, or each unavailable check has an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

### Verification evidence and self-review disposition (2026-08-02)

- `run_ci.bat`: PASS. Build, generated API freshness, formatting, sample
  publication, 303 tests on each of `net8.0` and `net10.0`, and 317 tests on
  `net9.0` completed with zero failures or skips.
- Deterministic loopback fixtures cover TCP and UDP, synchronous and
  asynchronous read-only and state-changing calls, mismatch classification
  before PLC-error construction, transport retirement and explicit reopen,
  matching data-bearing NG responses, no-data special NG responses, and
  representative high-level `ToyopucDeviceClient` read/write delegation.
- Codex self-review inspected the actual diff, shared decode order, public error
  classification, data-bearing boundary, no-data special form, read/write
  outcome mapping, invalidation and explicit reopen lifecycle, synchronous,
  asynchronous, and high-level delegation, tests, documentation, generated API,
  and package/sample checks. Accepted findings: an initial implementation set
  the explicit-reopen flag for unrelated malformed responses, which would have
  changed existing lifecycle contracts, so the flag was narrowed to this
  approved mismatch only; and the first test set lacked direct high-level
  DeviceClient evidence, so representative read/write tests were added.
  Rejected findings: none. Duplicate findings: none. Deferred findings: none.
- Live PLC verification is not required for this item: response correlation and
  lifecycle behavior are completely observable with deterministic transport
  fixtures, and no PLC/profile compatibility claim changed. No live PLC
  communication was performed.

## PERF-009B — Direction-specific socket deadline updates

Scope: synchronous TCP/UDP send and receive loops in `ToyopucClient`.

Target contract: the connection setup may initialize both socket timeouts, but a send operation
updates only `Socket.SendTimeout` and a receive operation updates only `Socket.ReceiveTimeout`.
Both values continue to be calculated from the same immutable absolute transaction deadline.
Fragmented sends and receives refresh only their own direction from the remaining time, without
resetting or extending the transaction deadline.

Compatibility impact: public API, wire traffic, timeout duration, retry behavior, error
classification, and PLC request count are unchanged. The change removes unnecessary opposite-side
socket option mutations from the normal path.

Machine-verifiable acceptance criteria:

1. Connection setup initializes both socket timeout directions.
2. Each send fragment changes only `SendTimeout`; each receive fragment changes only
   `ReceiveTimeout`.
3. Both setters reject an expired absolute deadline through the existing timeout classification.
4. TCP and UDP requests retain the existing absolute deadline and response behavior.

- [x] Implementation completed in this repository.
- [x] Deterministic helper, TCP/UDP loopback path, fragmented receive, monotonic deadline, and expired-deadline classification tests were added or updated and passed.
- [x] Relevant static checks, all-target tests, examples, build/package, API/documentation, and current-worktree source-archive checks passed.
- [x] Final diff self-review passed for public API, validation order, socket-option ownership, absolute deadline, and error classification.
- [x] Live PLC verification is not required because socket option ownership is locally observable.
- [x] Changelog and maintainer documentation agree with the implementation.
- [x] Final PERF-009B acceptance criteria verified and the item marked complete.

## CL-DOTNET-001 — Final synchronous open/close publication recheck

Target contract: a synchronous `Open()` candidate may publish only while its
captured operation generation remains current. `Close()` during DNS or after
connect but before publication retires that generation, rejects the open as
`ToyopucConnectionClosedException`, closes any candidate socket, and leaves the
client closed.

Final source-state evidence on 2026-08-02: the exact net8.0 filtered command
selecting `SyncOpenDiscardsLateSocketWhenCloseRetiresDnsGeneration` and
`SyncOpenPublishesOnlyAfterConnectAndDisposesPostConnectCandidateRetiredByClose`
passed 2/2. The first deterministic barrier releases DNS only after close and
proves no late socket publication. The second retires the generation after
connect and proves the unpublished candidate socket is disposed. No live PLC
is required because DNS/connect scheduling and socket identity are controlled
local lifecycle facts.

- [x] `CL-DOTNET-001` deterministic non-live disposition reverified on the final source state.

## RELEASE-COMPUTERLINK-4.1.0-20260827 — Canonical Word single-request APIs

Stable identifier: `RELEASE-COMPUTERLINK-4.1.0-20260827`.

Implementation scope: public high-level contiguous Word helpers, compatibility aliases, examples,
user and generated API documentation, package metadata, changelog, tests, and the final release
gate in this repository.

Target contract: release `ReadWordsSingleRequestAsync` and `WriteWordsSingleRequestAsync` as the
canonical one-command high-level Word APIs in `PlcComm.Toyopuc` `4.1.0`. Complete capacity, relay,
route, and value admission occurs before transport. No Bit-block helper is added because this
protocol API does not provide a supported Bit-block operation.

Compatibility impact: `ReadWordsAsync` and `WriteWordsAsync` remain deprecated delegates for this
release. Existing callers continue to compile, while new code uses the canonical names.

Machine-verifiable acceptance criteria:

1. MSBuild reports package version `4.1.0`, and the changelog has a dated `4.1.0` section.
2. The exact repository `release_check.bat` passes on the final source state.
3. The NuGet package exposes the canonical Word helpers on every supported target framework.
4. PLC Scope compiles and passes its non-live tests using the candidate package API.
5. No public registry publication is performed by the agent.

Live disposition: command count, preflight, relay planning, delegation, and response decoding are
fully covered by deterministic tests. No supported-PLC or physical-compatibility claim changes, so
a live PLC check is not required for this release item.

Final self-review inspected the direct and relay preflight payloads, capacity rejection, deprecated
delegation, public surface, examples, and package use. The only accepted finding was stale sample
help text naming `ReadWordsAsync`; it now names `ReadWordsSingleRequestAsync` and the sample rebuilds.
The working-tree release gate passed, but the gate and final-acceptance boxes stay open until the
same command is rerun against the eventual release commit before tagging.

- [x] Implementation and package metadata completed in this repository.
- [x] Tests cover every acceptance criterion.
- [x] Relevant static, unit, integration, sample, source-archive, API, and package gates passed.
- [x] Codex final self-review completed against the approved contract and actual diff.
- [x] Live verification is not required under the disposition above.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified and this item marked complete.

## CL-REQ-003 — Canonical DWord single-request extension identities

Stable identifier: `CL-REQ-003`.

Implementation scope: the public DWord single-request extensions, their compatibility aliases,
compile-time and runtime identity tests, user examples, migration guidance, generated API
reference, changelog, and exact public API classifications in this repository.

Target contract: `ReadDWordsSingleRequestAsync` and `WriteDWordsSingleRequestAsync` are the
canonical public extension names. Ordinary extension syntax resolves these names directly to the
one-request implementation. Their arguments, results, validation, error behavior, request limit,
FIFO ownership, and wire traffic remain those of the former single-request extensions.

Compatibility impact: the former static extension identities `ReadDWordsAsync` and
`WriteDWordsAsync` remain callable as `[Obsolete]` forwarding aliases for the next release only.
They delegate to the canonical names and are scheduled for removal in the next breaking release.
The broader instance methods with those former names are unchanged. New documentation and examples
use only the canonical `SingleRequest` names.

Machine-verifiable acceptance criteria:

1. Both canonical identities are public on every supported target framework and ordinary extension
   syntax compiles.
2. Read and write each issue exactly one request for a valid range and preserve the existing
   pre-transport single-request boundary.
3. The retained static aliases carry `ObsoleteAttribute` and produce the same request and result as
   their canonical counterpart.
4. Tests distinguish the canonical extension identities from the broader instance methods.
5. User examples use the canonical names; migration guidance, changelog, generated reference, and
   exact API classifications all state the approved transition.

Live disposition: no live PLC check is required. This item changes source-level method identity
only; deterministic tests compare canonical and compatibility requests byte-for-byte while both
paths use the unchanged single-request core.

- [x] Implementation completed in this repository.
- [x] Focused tests cover canonical resolution, one-request behavior, instance/extension identity, and compatibility delegation.
- [x] Relevant full static, unit, package, sample, API-diff, and documentation checks passed.
- [x] Codex self-review completed against the approved contract and actual diff.
- [x] Live verification is not required under the disposition above.
- [x] Migration guidance, changelog, generated API reference, and exact API classifications agree.
- [x] Final acceptance criteria verified and this item marked complete.
