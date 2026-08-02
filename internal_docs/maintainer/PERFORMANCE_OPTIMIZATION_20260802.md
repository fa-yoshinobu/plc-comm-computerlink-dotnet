# ComputerLink performance optimization decisions (2026-08-02)

## PERF2-003: prepared aggregate read segments

Scope: `ReadMany`, `ReadDevices`, and direct/relay async equivalents.

Target contract: validate and encode every segment before the first send, retain each exact direct or relay request, validate its complete response, and decode directly into the final result array. Compatibility impact: wire bytes, split order, errors, and public result shapes are unchanged.

Acceptance criteria:

1. Each segment request and relay wrapper is encoded once.
2. Every segment is preflighted before transport and a local later-segment error sends nothing.
3. A validated response writes directly to its final offset without a segment result array.
4. Failure returns no partial public result and preserves FIFO/deadline/transport classification.

## PERF2-004: private typed response views

Scope: typed direct and relay response decode; public `ResponseFrame`, trace, and diagnostics remain owned.

Target contract: typed decode borrows `ReadOnlyMemory<byte>` from the complete owned receive frame for one operation turn. Nested relay layers advance views without copying inner frames and validate response depth and route before value decode. Compatibility impact: no public API or wire change.

Acceptance criteria:

1. Typed success does not copy the complete response payload into an intermediate array.
2. Frame, return code, command, relay depth/route, and expected data length are validated before result construction.
3. Public raw responses own their mutable payload arrays and cannot mutate receive history.
4. Malformed responses preserve retirement and outcome-unknown rules.

## PERF2-009: one async input snapshot

Scope: .NET async collection APIs and aggregate high-level reads/writes.

Target contract: caller-owned inputs are materialized once before FIFO admission and passed to private prepared execution without calling the synchronous public aggregate API again. Compatibility impact: none.

Acceptance criteria:

1. Caller mutation after admission cannot change request bytes.
2. Prepared aggregate plans execute once as a native async sequence; normalized write tuples are reused during any adapter replay.
3. Validation order, request bytes, FIFO, cancellation, and errors match synchronous execution.

## PERF2-011 / PERF2-012: native async transport and shared DNS resolver

Scope: TCP/UDP asynchronous I/O plus synchronous/asynchronous hostname resolution.

Target contract: native `Socket` async waits run under one FIFO turn and one absolute deadline. Numeric IPv4 bypasses DNS. Hostnames use the same cancellation-aware IPv4 resolver core for sync and async opens, without library-owned DNS workers, timeout races, or cache. Compatibility impact: a synchronous `Open()` override is no longer invoked by `OpenAsync()`; derived clients must override the async method explicitly.

Acceptance criteria:

1. Async DNS/connect/send/receive contain no `Task.Run` socket wait and share one absolute deadline after FIFO activation.
2. Sync/async hostname open use `HostAddressResolver`; numeric IPv4 creates no resolver task or timer.
3. Queued cancellation sends nothing; active cancellation/close retires the generation; post-send state change reports outcome unknown.
4. Fixed-port UDP uncertainty taints the client and late data is never adopted by a later operation.
5. `OpenAsync()` has explicit breaking migration documentation and override coverage.

## Self-review disposition

- Accepted and fixed: the first native-async adapter replayed its synchronous action after each exchange. Aggregate requests were already encoded once outside that action, but completed segments were decoded again and the final array was reallocated. Direct and relay async aggregates now use one native prepared sequence; the regression test requires two payload builds, two exchanges, and exactly one sequence invocation.

## Evidence checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every locally exercisable acceptance criterion.
- [x] Final-source verification passed: `dotnet format --verify-no-changes`; Release solution build including examples/tools with zero warnings; tests on net8.0 (`311`), net10.0 (`311`), and net9.0 (`325`); API generator tests (`1` and `3`), generated API freshness, NuGet package creation, and `git diff --check`.
- [x] Codex self-review completed against the approved contracts and ComputerLink Python consistency; the accepted aggregate-replay finding above was corrected and the complete final-source gate was rerun.
- [x] Live PLC communication is not required: request bytes and response/lifecycle behavior are deterministically verified with vectors, loopback TCP/UDP, controlled DNS, and malformed-frame tests; no PLC profile or supported wire behavior changes.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the final implementation.
- [x] Final acceptance criteria verified and this record marked complete.
