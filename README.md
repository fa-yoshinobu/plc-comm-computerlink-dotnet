[![CI](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Toyopuc.svg)](https://www.nuget.org/packages/PlcComm.Toyopuc/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE)

# TOYOPUC Computer Link for .NET

`PlcComm.Toyopuc` is a .NET library for TOYOPUC Computer Link PLC communication.

## Supported PLC profiles

The maintained profile table is in [PLC profiles](docsrc/user/PROFILES.md). Choose one exact canonical PLC profile from that table.

## Supported device types

The maintained device and range tables are in [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md). Use that page for supported device families, address syntax, and profile-specific notes.

## Installation

```powershell
dotnet add package PlcComm.Toyopuc
```

## Quick example

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

## Documentation

| Page | Use it for |
| --- | --- |
| Getting started | [docsrc/user/GETTING_STARTED.md](docsrc/user/GETTING_STARTED.md) |
| Usage guide | [docsrc/user/USAGE_GUIDE.md](docsrc/user/USAGE_GUIDE.md) |
| Supported registers | [docsrc/user/SUPPORTED_REGISTERS.md](docsrc/user/SUPPORTED_REGISTERS.md) |
| PLC profiles | [docsrc/user/PROFILES.md](docsrc/user/PROFILES.md) |
| Examples | [examples/README.md](examples/README.md) |
| Full documentation site | [plc-comm-docs-site](https://fa-yoshinobu.github.io/plc-comm-docs-site/) |

## Hardware verified

Live-device verification is maintained in [Latest communication verification](docsrc/user/LATEST_COMMUNICATION_VERIFICATION.md).
See that page for verified PLC models, transports, dates, limitations, and retained validation notes.

## License and registry

| Item | Value |
| --- | --- |
| License | [MIT](LICENSE) |
| Registry | [NuGet](https://www.nuget.org/packages/PlcComm.Toyopuc/) |
| Package | `PlcComm.Toyopuc` |
