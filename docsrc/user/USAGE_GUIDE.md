# Usage guide

## Connection contract

`ToyopucConnectionOptions` requires host, port, TCP/UDP, canonical PLC profile, and `ToyopucRoute.Direct` or `ToyopucRoute.Relay(...)`. Invalid combinations such as TCP with a UDP local port are rejected before socket creation. Timeout is optional and defaults to three seconds.

## Read shapes

Read one value with `ReadOne`/`ReadOneAsync`. Read a continuous range with `ReadMany(device, count)`/`ReadManyAsync`; `count` is required and the result is always an array, including `count = 1`.

```csharp
var one = await client.ReadOneAsync("P1-D0000");
var words = await client.ReadManyAsync("P1-D0000", 8);
var sparse = await client.ReadDevicesAsync(["P1-D0000", "P1-D0002"]);
```

Every range and sparse read must fit one Computerlink request. A protocol-group or block boundary crossing is rejected before transport. The library does not expose chunked helpers and does not combine values observed at different PLC times into one apparent snapshot.

Typed contiguous helpers follow the same rule:

```csharp
ushort[] words = await client.ReadWordsAsync("P1-D0000", 8);
uint[] dwords = await client.ReadDWordsAsync("P1-D0100", 4);
await client.WriteWordsAsync("P1-D0000", new ushort[] { 10, 20 });
await client.WriteDWordsAsync("P1-D0100", new uint[] { 1000, 2000 });
```

If an application intentionally needs multiple requests, it must issue each request explicitly and manage ordering, time differences, partial writes, and retry policy itself.

## Named and typed values

Named addresses require a data type. Use `:U` or `:S` for a word, and `:D`, `:L`, or `:F` for a 32-bit value. `.0` through `.F` select a bit inside a word. Bare named addresses are rejected.

```csharp
var word = await client.ReadNamedAsync(["P1-D0000:U"]);
var dword = await client.ReadNamedAsync(["P1-D0100:D"]);
var bit13 = await client.ReadNamedAsync(["P1-D0000.D"]);
```

`:D` is an unsigned dword; `.D` is bit 13.

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

Cancellation tokens are optional. A token cancels its own gate wait or operation; canceling a queued operation does not close a different operation already using the client. The three-second communication timeout remains active when no token is supplied.

An active cancellation closes that transport session and requires an explicit `OpenAsync()` before it is reused. If a state-changing request may already have been sent, cancellation raises `ToyopucOperationOutcomeUnknownException`; do not assume that the PLC did or did not apply it. A transport timeout also discards the session so a late response cannot be mistaken for the next request; the next ordinary command may establish a new session.
