# Usage guide

## Recommended entry points

| Name | Use it for |
| --- | --- |
| `ToyopucDeviceClientFactory.OpenAndConnectAsync` | Create and open the queued client from explicit options. |
| `ReadTypedAsync` | Read one typed value from a word address. |
| `WriteTypedAsync` | Write one typed value to a word address. |
| `ReadNamedAsync` | Read a named snapshot of mixed word, typed, and bit-in-word addresses. |
| `PollAsync` | Read the same named snapshot repeatedly. |
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
await client.WriteTypedAsync("P1-D0001", "U", 1234);
var value = await client.ReadTypedAsync("P1-D0001", "U");
Console.WriteLine($"P1-D0001:U = {value}");
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
var snapshot = await client.ReadNamedAsync(
[
    "P1-D0000",
    "P1-D0001:S",
    "P1-D0100:D",
    "P1-D0102:F",
    "P1-D0100.3",
]);
Console.WriteLine(snapshot["P1-D0100:D"]);
```

`ReadNamedAsync` preserves the original address strings as dictionary keys.

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
await client.WriteBitInWordAsync("P1-D0100", bitIndex: 3, value: true);
var snapshot = await client.ReadNamedAsync(["P1-D0100.3"]);
Console.WriteLine($"P1-D0100.3 = {snapshot["P1-D0100.3"]}");
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
await foreach (var snapshot in client.PollAsync(["P1-D0000", "P1-D0100:F", "P1-D0100.3"], TimeSpan.FromSeconds(1)))
{
    Console.WriteLine(snapshot["P1-D0000"]);
    polls++;
    if (polls >= 3)
    {
        break;
    }
}
```

`PollAsync` yields the same shape as `ReadNamedAsync` on every interval.

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
await client.ExecuteAsync(inner => inner.WriteFrAsync("FR000000", 0x1234, commit: false));
Console.WriteLine("FR write staged");
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
await client.ExecuteAsync(inner => inner.CommitFrAsync("FR000000", wait: true));
Console.WriteLine("FR commit completed");
```

> **Caution:** FR writes are two-phase. Staging without committing leaves the value in RAM. A power cycle will revert it to the last committed value.

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
