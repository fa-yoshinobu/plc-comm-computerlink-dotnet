# Gotchas

Each entry starts with the symptom, then gives the root cause and a complete C# fix. The examples use `192.168.250.100:1025` and a canonical TOYOPUC profile.

## P1-D0000 not just D0000

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `D0000` raises an invalid-address or unknown-device error. | Basic area families require a `P1-`, `P2-`, or `P3-` program prefix. | Use `P1-D0000`, `P2-D0000`, or `P3-D0000` for the intended program area. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("P1-D0000", "U");
Console.WriteLine($"P1-D0000 = {value}");
```

## .D vs :D

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `P1-D0100.D` reads one bit instead of a 32-bit value. | Dot notation means bit-in-word access. `.D` is hexadecimal bit 13. | Use colon notation for typed views, such as `P1-D0100:D`. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var snapshot = await client.ReadNamedAsync(["P1-D0100:D", "P1-D0100.D"]);
Console.WriteLine($"dword={snapshot["P1-D0100:D"]}, bit13={snapshot["P1-D0100.D"]}");
```

## FR values revert after power cycle

| Symptom | Root cause | Fix |
| --- | --- | --- |
| An `FR` value changes during runtime but returns to the old value after a power cycle. | FR writes are two-phase. `WriteFrAsync(..., commit: false)` stages RAM only. | Call `CommitFrAsync()` after staging, or pass `commit: true` only when flash persistence is intended. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:pc10g:pc10",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
// Use only a test FR address. CommitFrAsync persists the staged value
// to flash and does not restore the previous value automatically.
await client.ExecuteAsync(inner => inner.WriteFrAsync("FR000000", 0x1234, commit: false));
await client.ExecuteAsync(inner => inner.CommitFrAsync("FR000000", wait: true));
Console.WriteLine("FR write committed");
```

## Non-canonical profile string fails immediately

| Symptom | Root cause | Fix |
| --- | --- | --- |
| Opening the connection throws before any PLC request is sent. | `PlcProfile` is required and accepts only exact canonical strings from source. Aliases and blank values are rejected. | Copy one exact string from [PROFILES.md](PROFILES.md), such as `toyopuc:plus:extended`. |

```csharp
using System;
using PlcComm.Toyopuc;

var profile = ToyopucPlcProfiles.NormalizeName("toyopuc:plus:extended");
var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = profile,
};

Console.WriteLine(options.PlcProfile);
```

## Relay hops not probed automatically

| Symptom | Root cause | Fix |
| --- | --- | --- |
| Relay reads or writes do not reach the expected PLC. | Relay topology is not probed automatically because automatic route guessing can hide configuration mistakes. | Set `RelayHops` explicitly in `ToyopucConnectionOptions`. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:nano-10gx:compatible",
    RelayHops = "P1-L2:N4,P1-L2:N6,P1-L2:N2",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("P1-D0000", "U");
Console.WriteLine($"Relay P1-D0000 = {value}");
```

## Packed bit notation P1-M0010W

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `P1-M0010W`, `P1-M0010H`, or `P1-M0010L` is mistaken for a typed suffix. | `W`, `H`, and `L` appended to a bit-area address mean packed word, high byte, or low byte. They are not `:D` or `:F` type suffixes. | Use packed notation only for bit-area packed views, and use colon notation for typed word values. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var packed = await client.ExecuteAsync(inner => inner.ReadAsync("P1-M0010W"));
var dword = await client.ReadTypedAsync("P1-D0100", "D");
Console.WriteLine($"packed={packed}, dword={dword}");
```

## FR write through a generic write path fails

| Symptom | Root cause | Fix |
| --- | --- | --- |
| A generic high-level write to `FR000000` fails. | Generic FR writes are intentionally blocked so persistence is never implicit. | Use `WriteFrAsync` and `CommitFrAsync` explicitly. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:pc10g:pc10",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
// Use only a test FR address. CommitFrAsync persists the staged value
// to flash and does not restore the previous value automatically.
await client.ExecuteAsync(inner => inner.WriteFrAsync("FR000000", 0x1234, commit: false));
await client.ExecuteAsync(inner => inner.CommitFrAsync("FR000000", wait: true));
Console.WriteLine("FR helper write completed");
```

## Single-request block reads fail across a boundary

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `ReadWordsSingleRequestAsync` reports an incompatible protocol group. | The requested range crosses a boundary that cannot be represented as one compatible protocol request. | Use `ReadWordsChunkedAsync` with an explicit maximum chunk size. |

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var values = await client.ReadWordsChunkedAsync("P1-D0000", 64, maxWordsPerRequest: 16);
Console.WriteLine($"Read {values.Length} words");
```
