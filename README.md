[![CI](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Toyopuc.svg)](https://www.nuget.org/packages/PlcComm.Toyopuc/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE)

# TOYOPUC Computerlink for .NET

`PlcComm.Toyopuc` is a .NET library for TOYOPUC Computerlink PLC communication.

## PLC Comm Family

This library is part of the plc-comm family. See the [package matrix](https://fa-yoshinobu.github.io/plc-comm-docs-site/package-matrix/) for protocol, language, registry, and install-command mapping.

## Supported PLC profiles

The maintained profile table is in [PLC profiles](https://fa-yoshinobu.github.io/plc-comm-docs-site/computerlink/dotnet/PROFILES/). Choose one exact canonical PLC profile from that table.

## Supported device types

The shared device and range tables are in the [Computerlink Device Ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/computerlink/device-ranges/) page. Use that page for supported device families, address syntax, and profile-specific notes.

## Installation

```powershell
dotnet add package PlcComm.Toyopuc
```

## Quick example

```csharp
using System;
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions(
    "192.168.250.100",
    1025,
    ToyopucTransportMode.Tcp,
    "toyopuc:plus:extended",
    ToyopucRoute.Direct);
await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("P1-D0000", "U");
Console.WriteLine($"P1-D0000 = {value}");
```

## Documentation

| Page | Use it for |
| --- | --- |
| [Full documentation site](https://fa-yoshinobu.github.io/plc-comm-docs-site/) | Unified docs for all PLC communication libraries. |
| [Getting started](https://fa-yoshinobu.github.io/plc-comm-docs-site/computerlink/dotnet/GETTING_STARTED/) | Install the package, connect to your PLC, and run your first read/write. |
| [Usage guide](https://fa-yoshinobu.github.io/plc-comm-docs-site/computerlink/dotnet/USAGE_GUIDE/) | Use the high-level API and common Computerlink workflows. |
| [API reference](https://fa-yoshinobu.github.io/plc-comm-docs-site/computerlink/dotnet/API_REFERENCE/) | Browse generated public .NET signatures and XML documentation comments. |
| [PLC profiles](https://fa-yoshinobu.github.io/plc-comm-docs-site/computerlink/dotnet/PROFILES/) | Choose the canonical TOYOPUC profile for the target PLC. |
| [Computerlink Device Ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/computerlink/device-ranges/) | Check shared device families, address notation, and model range notes. |
| [Computerlink Troubleshooting & Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/computerlink/troubleshooting-codes/) | Troubleshoot common connection, address, write, relay, and PLC error-code symptoms. |
| [Gotchas](https://fa-yoshinobu.github.io/plc-comm-docs-site/computerlink/dotnet/GOTCHAS/) | Check Computerlink helper-specific caveats. |
| [Examples](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/examples/README.md) | Run maintained .NET examples. |

## License and registry

| Item | Value |
| --- | --- |
| License | [MIT](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE) |
| Registry | [NuGet](https://www.nuget.org/packages/PlcComm.Toyopuc/) |
| Package | `PlcComm.Toyopuc` |

## Commercial support

If you plan to embed this library in a paid or commercial product, please consider a separate support agreement or supporting the project as a sponsor.

Contact: <https://fa-labo.com/contact.html>
