# Usage guide

## Connection contract

`ToyopucConnectionOptions` requires host, port, TCP/UDP, canonical PLC profile, and `ToyopucRoute.Direct` or `ToyopucRoute.Relay(...)`. Invalid combinations such as TCP with a UDP local port are rejected before socket creation. Timeout is optional and defaults to three seconds.

`ToyopucDeviceClientFactory.OpenAndConnectAsync` returns an ordinary `ToyopucDeviceClient` whose immutable `Route` is used by ordinary high-level async methods. A relay-configured client therefore uses `ReadOneAsync`, `WriteAsync`, typed helpers, clock/status methods, and polling without a second wrapper API.

## Concurrent async calls

Each ordinary client admits public asynchronous live operations to one arrival-order FIFO queue and runs at most one wire transaction at a time. Collections, payloads, relay hops, device/profile identity, and the effective route are snapshotted when the call is admitted. Mutating a caller-owned list or array while the call waits cannot change the later request.

Canceling a call that is still waiting removes only that call and sends nothing. Queue waiting does not consume the transaction timeout; the timeout starts when the call owns the transport. Compound helpers reserve the client without deadlocking their nested protocol calls, while separate client instances progress independently.

`QueuedToyopucDeviceClient` was removed. Replace its type with `ToyopucDeviceClient`, remove the wrapper constructor, and keep using the same ordinary high-level methods. The factory now returns the ordinary client directly.

## Read shapes

Read one value with `ReadOne`/`ReadOneAsync`. Read a continuous range with `ReadMany(device, count)`/`ReadManyAsync`; `count` is required and the result is always an array, including `count = 1`.

A `ResolvedDevice` is bound to the exact canonical PLC profile that resolved it. Passing it to a client configured for any other profile is rejected before request construction or transport activity, even when both profiles share addressing rules. Resolve the device again through the destination client instead of reusing it across profiles.

```csharp
var one = await client.ReadOneAsync("P1-D0000");
var words = await client.ReadManyAsync("P1-D0000", 8);
var sparse = await client.ReadDevicesAsync(["P1-D0000", "P1-D0002"]);
```

Methods explicitly documented as single-request reads must fit one Computerlink request and reject a protocol-group or block-boundary crossing before transport. The aggregate methods shown above instead validate the complete plan and may split as described below. Sparse entries are encoded in their declared order and results retain the same input mapping; the library does not sort addresses to change read timing.

Typed contiguous helpers follow the same rule:

```csharp
ushort[] words = await client.ReadWordsAsync("P1-D0000", 8);
uint[] dwords = await client.ReadDWordsAsync("P1-D0100", 4);
await client.WriteWordsAsync("P1-D0000", new ushort[] { 10, 20 });
await client.WriteDWordsAsync("P1-D0100", new uint[] { 1000, 2000 });
```

Explicit read aggregates (`ReadMany`, `ReadDevices`, their relay forms, and async equivalents) compile and validate the complete request plan before communication. They use one client FIFO turn, preserve caller-declared entry order, and split only when one protocol request cannot represent the aggregate. The calls are not PLC-atomic: different split requests can observe different PLC scan instants. If a later split request fails, the aggregate raises an error and does not return partial results.

Writes are never implicitly divided into multiple protocol requests. When a write aggregate cannot fit one compatible request, it is rejected before transport; applications that intentionally need several writes must issue them explicitly and own the ordering, partial-write, and recovery policy.

## Named and typed values

Named addresses require a data type. Use `:U` or `:S` for a word, and `:D`, `:L`, or `:F` for a 32-bit value. `.0` through `.F` select a bit inside a word. Bare named addresses are rejected.

```csharp
var word = await client.ReadNamedAsync(["P1-D0000:U"]);
var dword = await client.ReadNamedAsync(["P1-D0100:D"]);
var bit13 = await client.ReadNamedAsync(["P1-D0000.D"]);
```

`:D` is an unsigned dword; `.D` is bit 13.

Use `WriteBitInWordAsync` only when an explicit read-modify-write is intended.
It holds one FIFO turn on this client across the read and write, but the PLC
still receives two requests. The operation is not PLC-atomic: PLC logic,
another connection, or another controller can change the word between those
requests. A post-send write failure can be outcome-unknown, so reconcile the
PLC state before deciding whether another write is safe.

## FR work area and commit

FR work-area update and nonvolatile commit are separate operations:

```csharp
var original = await client.ReadFrOneAsync("FR000000");
await client.WriteFrWorkAreaAsync("FR000000", 0x1234);
// Explicitly persist the containing block only when intended:
await client.CommitFrBlockAsync("FR000000");
```

`WriteFrWorkAreaAsync` performs one write request and never commits. `CommitFrBlockAsync` sends one block commit request and returns when that request is accepted; it does not poll for flash completion. Applications that need completion monitoring must explicitly read the profile-selected CPU status and control their own interval and deadline.

FR work-area values must be integral values in `0..65535`. The library rejects negative, overflowing, Boolean, fractional, and string values before transport instead of masking or converting them.

## PLC clock century

The PLC transmits only a two-digit year. Supply the century when converting or writing:

```csharp
var localClock = (await client.ReadClockAsync()).AsDateTime(2000);
await client.WriteClockAsync(
    new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Unspecified),
    2000);
```

Clock values are timezone-unspecified PLC local time. UTC or offset conversion is never inferred.

## Cancellation

Cancellation tokens are optional. A token cancels its own FIFO wait or active operation; canceling a waiting operation does not close a different operation already using the client. The three-second communication timeout remains active when no token is supplied.

An active cancellation closes that transport session and requires an explicit `OpenAsync()` before it is reused. If a state-changing request may already have been sent, cancellation raises `ToyopucOperationOutcomeUnknownException`; do not assume that the PLC did or did not apply it. A transport timeout also discards the session so a late response cannot be mistaken for the next request; the next ordinary command may establish a new session.

Calling `Close`/`CloseAsync` rejects active and waiting operations from that transport generation with `ToyopucConnectionClosedException` and prevents any previously queued call from sending. Disposal is terminal and reports `ObjectDisposedException` instead.

## Traffic statistics

Read `client.TrafficStats` for cumulative `RequestCount`, `TxBytes`, and `RxBytes`.
