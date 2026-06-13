# Getting started

## Start here

Use this page to install `PlcComm.Toyopuc`, open one queued Computer Link client, read `P1-D0000`, and write one safe test word. You need a reachable TOYOPUC PLC and a profile string that matches your PLC.

## Prerequisites

| Requirement | Value |
| --- | --- |
| .NET SDK | .NET 9 SDK or newer |
| PLC network access | TCP access to `192.168.250.100:1025` |
| Package | `PlcComm.Toyopuc` |
| First profile used here | `toyopuc:plus:extended` |

## Install

```powershell
dotnet add package PlcComm.Toyopuc
```

## Choose your PLC profile

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    PlcProfile = "toyopuc:plus:extended",
};
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
Console.WriteLine(client.PlcProfile);
```

`PlcProfile` on `ToyopucConnectionOptions` is the only required connection-time selector. The value must match one canonical profile string from [PROFILES.md](PROFILES.md).

## First read (step by step)

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

Expected output:

```text
P1-D0000 = 0
```

The number depends on your PLC state. A different numeric value is still a successful read.

## First write

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
Console.WriteLine($"P1-D0001 = {value}");
```

Only write to a test address you control. `P1-D0001` is a word register, not a bit or timer.

## Confirm success

1. The connection opens without a timeout.
2. `ReadTypedAsync("P1-D0000", "U")` returns a numeric value.
3. Your test write returns without a PLC error.
4. Reading the same word after the write returns the value you wrote.

## If it does not work

| Check | What to verify |
| --- | --- |
| Wrong host or port | The default TCP port used here is `1025`. |
| Wrong profile string | The profile must match exactly; see [PROFILES.md](PROFILES.md). |
| Read shape | Start with plain word reads before typed values or bit-in-word syntax. |
| Address prefix | Basic area families such as D, M, X, Y, T, C, L, N, R, and S require `P1-`, `P2-`, or `P3-`. |

## Next pages

| Page | Link |
| --- | --- |
| Usage guide | [USAGE_GUIDE.md](USAGE_GUIDE.md) |
| Supported registers | [SUPPORTED_REGISTERS.md](SUPPORTED_REGISTERS.md) |
