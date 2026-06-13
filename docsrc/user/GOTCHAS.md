# Gotchas

## Address prefix missing

If your read raises "invalid address" or "unknown device": the basic area families require a `P1-`, `P2-`, or `P3-` prefix. `D0000` alone is rejected by the library.

Fix:

```csharp
var value = await client.ReadTypedAsync("P1-D0000", "U");
```

## `.D` returns bit 13 instead of a 32-bit value

If reading `P1-D0100.D` returns a single bit value instead of a 32-bit integer: `.D` on a word address means bit offset 13, not a 32-bit dword view.

Fix:

```csharp
var value = await client.ReadNamedAsync(["P1-D0100:D"]);
```

## FR value reverts after power cycle

If an FR write does not survive a power cycle: FR writes are staged in RAM and require an explicit commit to persist to flash.

Fix:

```csharp
await client.ExecuteAsync(inner => inner.CommitFrAsync("FR000000", wait: true));
```

## Relay reads fail silently

If relay reads return no data and no error: relay hops are not probed automatically.

Fix:

```csharp
var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:nano-10gx:compatible",
    RelayHops = "P1-L2:N4,P1-L2:N6,P1-L2:N2",
};
```

## Packed bit notation misread as a type suffix

If `P1-M0010W` or similar is parsed unexpectedly: `W`, `H`, and `L` appended to a bit-area address are packed-word notation, not type suffixes.

Fix:

```text
P1-M0010W  full 16-bit word from the bit area
P1-M0010H  high byte from the bit area
P1-M0010L  low byte from the bit area
```

## FR write through `WriteAsync` fails

If writing `FR000000` through a generic write call fails: FR has dedicated two-phase helpers and is blocked from generic high-level writes.

Fix:

```csharp
await client.ExecuteAsync(inner => inner.WriteFrAsync("FR000000", 0x1234, commit: false));
await client.ExecuteAsync(inner => inner.CommitFrAsync("FR000000", wait: true));
```

## Single-request block reads fail across a boundary

If `ReadWordsSingleRequestAsync` reports an incompatible protocol group: the requested range crosses a boundary that cannot be represented as one compatible protocol request.

Fix:

```csharp
var values = await client.ReadWordsChunkedAsync("P1-D0000", 64, maxWordsPerRequest: 16);
```
