# ComputerLink .NET quality overhaul — 2026-07-11

## Goal

The public contract is explicit, profile-bound, single-request by default, safe under retry and cancellation, and consistent with the approved ComputerLink Python contract. Compatibility aliases do not remain public when they preserve ambiguous or unsafe behavior.

Branch: `quality/2026-07-overhaul`

Verification baseline after implementation: `run_ci.bat` builds all projects without warnings, validates the generated API reference, runs tests on .NET 8/9/10, verifies formatting, and publishes the self-contained HighLevelSample.

Latest evidence: .NET 8 `234`, .NET 9 `247`, and .NET 10 `234` tests passed; the generated reference is current, build warnings are zero, formatting passed, the self-contained sample published, and NuGet/symbol packages were created by the post-Claude complete release gate on 2026-07-13. Both GitHub `build-test` jobs for cancellation fix `5f9de5b` passed, and `CLAUDE-CL-20260713-02` independently returned zero findings.

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

## D-068 — Three-second communication timeout

Scope: TCP/UDP connection, send, and receive attempts. Target: omitted timeout is three seconds; zero and negative values are invalid.

Breaking impact: `TimeSpan.Zero` no longer means “use the default” and timeout is immutable after construction.

Acceptance criteria: default and explicit positive values work; invalid values fail before transport; each retry attempt owns its timeout.

- [x] Implementation completed.
- [x] Constructor/default, token-omitted timeout, final-session disposal, late-response isolation, and new-session tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed for validation, per-attempt timeout, session disposal, and stale-response prevention.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; timeout default, validation, per-attempt cancellation, session disposal, and late-response isolation use controlled local fixtures).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

## D-069 — Safe retry contract

Scope: all direct/relay operations. Target: `Retries=0` by default; connection failures before request send may retry; explicitly retryable reads may retry; state-changing or raw requests do not retry after send.

Breaking impact: negative retry values are rejected and unsafe blanket retry is removed.

Acceptance criteria: distinguish pre-send, post-send, and retryable PLC response states; verify reads, writes, FR, clock, scan, relay, and raw paths at retry counts 0/1.

- [x] Implementation complete in every path; direct and relay reads use the explicit retryable path while state-changing and raw paths do not retry after send.
- [x] Direct/relay/FR/clock retryable reads and raw/write/FR commit/FR write/clock write/scan/relay scan post-send no-retry tests pass at retry counts `0/1` as applicable.
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

Scope: connection options, factory, and queued client. Target: `ToyopucRoute.Direct` or `ToyopucRoute.Relay(hops)` is a required construction argument.

Breaking impact: null/omitted relay hops no longer imply direct.

Acceptance criteria: direct, one/multiple relay hops, null/empty/invalid hops, and route preservation in queued operations.

- [x] Implementation completed.
- [x] Required constructor shape and strict route tests added; existing relay wire tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required for the required-route API decision; direct/relay type construction, hop validation, queued preservation, and zero-send rejection are deterministic, while configured relay hardware remains separately unverified under D-084).
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

## D-077 — Separate one-value and many-value reads

Scope: direct/relay/FR/word sync and async APIs. Target: `ReadOne` returns one value; `ReadMany(device,count)` requires count and always returns an array; `ReadDevices` is sparse; all are single-request only.

Breaking impact: `Read(device,count=1)` and count-dependent result types are removed publicly.

Acceptance criteria: one, many count 1/boundary/invalid/crossing, direct/relay/FR/bit/word, no transport on rejection.

- [x] Implementation completed.
- [x] Single-request, boundary-before-transport, return-shape, and public-surface tests pass.
- [x] All automated checks passed.
- [x] Codex self-review completed against the final diff, public surface, validation order, tests, documentation, and the approved cross-language contract.
- [x] Claude review completed (`CLAUDE-CL-20260712-01`; result recorded).
- [x] Claude findings dispositioned (`CLAUDE-CL-20260712-01`).
- [x] Live/release disposition recorded (no live PLC required; scalar/array shape, strict count, one-request enforcement, and no-transport rejection use mock/frame evidence).
- [x] Documentation/API reference agree.
- [x] Final acceptance verified.

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
| D-080 | Nano 10GX TUC-1157; `toyopuc:nano-10gx:compatible`; `192.168.250.100:1025` TCP; Direct; `FR000000` | `pass`（2026-07-12）。Python／.NETで元値`999`、work-area test `0x7811`、readback `30737`、write command `0xC3`、CA未実行を確認。 | 両実装とも元値`999`をC3で復元しreadback済み。.NET一時project／生成物削除済み。 |
| D-081 | Nano 10GX TUC-1157; `toyopuc:nano-10gx:compatible`; `192.168.250.100:1025` TCP; Direct; `FR000000` | `pass`（2026-07-12）。再起動直後の元値`999`からPythonで`0x74E6`を書込み・readbackしCAを1回実行。明示A0 readで約1秒後に書込み中解除・異常なし。再起動後.NETが`0x74E6`を確認した。 | .NETでC3により`999`へ戻してCAを1回実行し、明示A0 readで約1.5秒後に書込み中解除・異常なし。再々起動後Python／.NETとも`999`。libraryによるpoll／retry／fallbackなし。一時.NET project／生成物削除済み。 |
| D-083 | Nano 10GX TUC-1157; `toyopuc:nano-10gx:compatible`; `192.168.250.100:1025` TCP; Direct; PLC clock | `pass`（2026-07-12）。Python／.NETとも明示century付きwrite、確認read、元時刻復元に成功。 | Profile固有の短い反映差はAPI契約へ一般化しない。自動wait／reconnect／retryなし。一時.NET project／生成物削除済み。 |
| D-084-A | Nano 10GX; `toyopuc:nano-10gx:compatible`; TCP `192.168.250.100:1025`; `P1-L1:N2`; `P1-D0000` | `pass`（2026-07-12）。Python／.NETとも`0xFFFF → 0x3DA4 → 0xFFFF`、outer `CMD=60`、link `0x11`、station `0x0002`、ENQ `0x05`、response unwrap成功。 | 両実装とも元値復元・readback済み。一時.NET project／生成物削除済み。 |
| D-084-B | Real multi-hop topology, endpoint, route, target PLC/device unavailable | `unverified; release permitted`（2026-07-12）。Exact nested-frame, all-hop validation, and response-unwrap tests pass; D-084-A one-hop live behavior passes in Python/.NET. No multi-hop live-pass claim is made. | TODO remains until exact hardware/topology exists. Explicit hops are mandatory; no route discovery, route mutation, or fallback. Any future live command still requires the exact route/target and user `OK`. |

If hardware is unavailable, each item needs an explicit release disposition. The proposed dispositions are recorded in workspace `quality_overhaul_goal_20260711.md`; no proposal is approved merely by being documented.

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
