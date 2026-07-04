# Usage guide

## Recommended entry points

| Name | Use it for |
| --- | --- |
| `ToyopucDeviceClientFactory.OpenAndConnectAsync` | Create and open the queued client from explicit options. |
| `ReadTypedAsync` | Read one typed value from a word address. |
| `WriteTypedAsync` | Write one typed value to a word address. |
| `ReadNamedAsync` | Read one named word, typed, or bit-in-word address. |
| `PollAsync` | Read the same named address repeatedly. |
| `ReadWordsSingleRequestAsync` | Read contiguous words as one protocol request or fail. |
| `ReadDWordsSingleRequestAsync` | Read contiguous 32-bit unsigned values as one logical request or fail. |
| `WriteWordsSingleRequestAsync` | Write contiguous words as one protocol request or fail. |
| `WriteDWordsSingleRequestAsync` | Write contiguous 32-bit unsigned values as one logical request or fail. |
| `ReadWordsChunkedAsync` | Read large word ranges with explicit chunking. |
| `ReadDWordsChunkedAsync` | Read large dword ranges with explicit chunking. |
| `WriteWordsChunkedAsync` | Write large word ranges with explicit chunking. |
| `WriteDWordsChunkedAsync` | Write large dword ranges with explicit chunking. |
| `WriteBitInWordAsync` | Change one bit inside a word register. |
| `ReadFrAsync` | Read FR flash words through `ExecuteAsync`. |
| `WriteFrAsync` | Stage or commit FR flash word writes through `ExecuteAsync`. |
| `CommitFrAsync` | Commit staged FR writes through `ExecuteAsync`. |

## Connection

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
    Timeout = TimeSpan.FromSeconds(3),
    Retries = 2,
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"{client.Host}:{client.Port} {client.PlcProfile}");
```

Use `ToyopucConnectionOptions` when you want host, port, transport, timeout, retry, profile, and relay settings in one place.

## Connection reuse and concurrent requests

Keep one `QueuedToyopucDeviceClient` open for repeated reads, writes, and polling.
The factory returns a queued client, so multiple async callers can share that
client without interleaving protocol requests on the same PLC connection.

Do not call the low-level `InnerClient` concurrently. When custom access is
needed, use `ExecuteAsync` so the operation stays inside the same queue. After a
persistent socket or protocol connection failure, dispose the current client and
create a new one with the same `ToyopucConnectionOptions`.

## Read a single value

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
Console.WriteLine($"P1-D0000:U = {value}");
```

| Type suffix | Meaning | Words |
| --- | --- | --- |
| `U` | unsigned 16-bit word | 1 |
| `S` | signed 16-bit word | 1 |
| `D` | unsigned 32-bit dword | 2 |
| `L` | signed 32-bit long | 2 |
| `F` | 32-bit floating point value | 2 |

`ReadTypedAsync` takes the data type as a separate `dtype` argument.

## Write a single value

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var original = await client.ReadTypedAsync("P1-D0001", "U");
try
{
    await client.WriteTypedAsync("P1-D0001", "U", 1234);
    var value = await client.ReadTypedAsync("P1-D0001", "U");
    Console.WriteLine($"P1-D0001:U = {value}");
}
finally
{
    await client.WriteTypedAsync("P1-D0001", "U", original);
}
```

Use a matched read/write pair while commissioning so you can prove that your test word is the address you intended.

## Named snapshot read

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var snapshot = await client.ReadNamedAsync(["P1-D0100:D"]);
Console.WriteLine(snapshot["P1-D0100:D"]);
```

`ReadNamedAsync` accepts one named address per call and preserves the original address string as the dictionary key.

## Batching and request boundaries

`ReadMany` and `WriteMany` execute only when all requested devices can be represented by one compatible protocol request. They throw `ToyopucProtocolError` before communication when the request would need multiple protocol requests, such as incompatible protocol groups, PC10 block boundary crossings, or helper paths that would fall back to individual requests.

`ReadNamedAsync` accepts one named address per call. Use explicit repeated calls when multiple named reads are intentional.

For contiguous word ranges, use `ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync`, `WriteWordsSingleRequestAsync`, or `WriteDWordsSingleRequestAsync`. These helpers also fail if the requested range cannot be represented as one compatible protocol request. Use the `*ChunkedAsync` helpers, or separate explicit calls, only when splitting is intentional and partial completion is acceptable.

## Contiguous block reads

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var words = await client.ReadWordsSingleRequestAsync("P1-D0000", 8);
var dwords = await client.ReadDWordsSingleRequestAsync("P1-D0100", 4);
var chunkedWords = await client.ReadWordsChunkedAsync("P1-D0200", 64, maxWordsPerRequest: 16);
var chunkedDwords = await client.ReadDWordsChunkedAsync("P1-D0300", 16, maxDwordsPerRequest: 4);
Console.WriteLine($"{words.Length} words, {dwords.Length} dwords, {chunkedWords.Length} chunked words, {chunkedDwords.Length} chunked dwords");
```

Single-request helpers fail if the range cannot stay in one compatible protocol group. Chunked helpers split only because you explicitly choose a chunk size.

## Write contiguous blocks

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
await client.WriteWordsSingleRequestAsync("P1-D0002", new ushort[] { 10, 20, 30 });
await client.WriteDWordsSingleRequestAsync("P1-D0100", new uint[] { 1000, 2000 });
await client.WriteWordsChunkedAsync("P1-D0200", new ushort[] { 1, 2, 3, 4, 5, 6 }, maxWordsPerRequest: 2);
await client.WriteDWordsChunkedAsync("P1-D0300", new uint[] { 7, 8, 9, 10 }, maxDwordsPerRequest: 2);
Console.WriteLine("Block writes completed");
```

Use chunked writes only for test ranges you control.

## Bit in word

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var before = await client.ReadNamedAsync(["P1-D0100.3"]);
var originalBit = Convert.ToBoolean(before["P1-D0100.3"]);
try
{
    await client.WriteBitInWordAsync("P1-D0100", bitIndex: 3, value: true);
    var snapshot = await client.ReadNamedAsync(["P1-D0100.3"]);
    Console.WriteLine($"P1-D0100.3 = {snapshot["P1-D0100.3"]}");
}
finally
{
    await client.WriteBitInWordAsync("P1-D0100", bitIndex: 3, value: originalBit);
}
```

Dot notation such as `P1-D0100.3` means one bit inside a word. Use colon notation such as `P1-D0100:D` for a 32-bit dword view.

## Polling

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var polls = 0;
await foreach (var snapshot in client.PollAsync(["P1-D0000"], TimeSpan.FromSeconds(1)))
{
    Console.WriteLine(snapshot["P1-D0000"]);
    polls++;
    if (polls >= 3)
    {
        break;
    }
}
```

`PollAsync` yields the same one-address dictionary shape as `ReadNamedAsync` on every interval.

## Operational recipes

The examples include two read-only operational recipes for repeatable
collection:

- `PlcComm.Toyopuc.MultiPlcMonitorSample` monitors multiple PLC endpoints at
  the same time. Each PLC has its own task, connection, and reconnect loop, so
  one offline PLC does not block healthy PLC reads.
- `PlcComm.Toyopuc.ConfigPollingSample` runs periodic collection from a JSON
  config file and can append long-form CSV rows as
  `timestamp,plc,tag,value`.

Both examples use the same reconnect states as the polling reconnect sample:
`connected`, `lost`, `reconnecting`, and `recovered`, with 1 second initial
backoff, exponential delay, and a 30 second default maximum. YAML config is
available only in the Python sample; the .NET sample uses JSON.

```powershell
dotnet run --project examples/PlcComm.Toyopuc.MultiPlcMonitorSample -- --plc line-a=192.168.250.100,toyopuc:plus:extended,1025,tcp --plc line-b=192.168.250.101,toyopuc:plus:extended,1025,tcp --tag d0100=P1-D0100:U
dotnet run --project examples/PlcComm.Toyopuc.ConfigPollingSample -- --config examples/PlcComm.Toyopuc.ConfigPollingSample/config_polling.example.json --dry-run
```

## FR file-register helpers

### Read current FR values

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:pc10g:pc10",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var value = await client.ExecuteAsync(inner => inner.ReadFrAsync("FR000000"));
Console.WriteLine($"FR000000 = {value}");
```

Use `ReadFrAsync` through `ExecuteAsync` so the queued client still serializes the operation.

### Stage a write

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:pc10g:pc10",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var original = await client.ExecuteAsync(inner => inner.ReadFrAsync("FR000000"));
try
{
    await client.ExecuteAsync(inner => inner.WriteFrAsync("FR000000", 0x1234, commit: false));
    Console.WriteLine("FR write staged");
}
finally
{
    await client.ExecuteAsync(inner => inner.WriteFrAsync("FR000000", original, commit: false));
}
```

`commit: false` stages the value without flushing it to flash.

### Commit

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:pc10g:pc10",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
// Commit only after staging an intended value to a test FR address.
// Committed FR writes survive PLC power cycles.
// await client.ExecuteAsync(inner => inner.CommitFrAsync("FR000000", wait: true));
Console.WriteLine("FR commit intentionally not executed by this sample");
```

> **Caution:** FR writes are two-phase. Staging without committing leaves the value in RAM. Calling `CommitFrAsync` flushes the staged value to flash, so use it only when persistence is intended.

## Relay helpers (multi-hop)

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

Relay topology is not auto-discovered. Set `RelayHops` explicitly with the correct hop list for your PLC network.

## Address reference table

| Form | Example | Meaning |
| --- | --- | --- |
| Plain word | `P1-D0100` | Unsigned 16-bit word. |
| `:U` | `P1-D0100:U` | Unsigned 16-bit word. |
| `:S` | `P1-D0100:S` | Signed 16-bit word. |
| `:D` | `P1-D0100:D` | Unsigned 32-bit dword. |
| `:L` | `P1-D0100:L` | Signed 32-bit long. |
| `:F` | `P1-D0100:F` | 32-bit floating point value. |
| `.n` | `P1-D0100.3` | Bit `n` inside the word, where `n` is `0` through `F`. |
| `W` | `P1-M0010W` | Packed 16-bit word from a bit-area address. |
| `H` | `P1-M0010H` | High byte from a bit-area address. |
| `L` | `P1-M0010L` | Low byte from a bit-area address. |
