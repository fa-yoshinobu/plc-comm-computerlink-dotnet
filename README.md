[![CI](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Toyopuc.svg)](https://www.nuget.org/packages/PlcComm.Toyopuc/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE)

# Computer Link Protocol for .NET

`PlcComm.Toyopuc` is a .NET library for JTEKT TOYOPUC Computer Link communication with TOYOPUC-Plus, Nano 10GX, PC10G, PC3JX, and PC3JG PLC profiles.

## Supported PLC profiles

| Profile string | Hardware | Notes |
| --- | --- | --- |
| `toyopuc:generic` | Any TOYOPUC Computer Link target | Generic source-defined area set with broad U, EB, FR, and upper-range PC10 addressing enabled. |
| `toyopuc:plus:standard` | TOYOPUC-Plus | Standard prefixed P/K/V/T/C/L/X/Y/M/S/N/R/D areas plus ES, EN, H, and extension bit areas. |
| `toyopuc:plus:extended` | TOYOPUC-Plus | Adds GM/GX/GY and U areas to the standard TOYOPUC-Plus profile. |
| `toyopuc:nano-10gx:native` | Nano 10GX | Native Nano 10GX profile with upper split ranges, U, EB, and FR. |
| `toyopuc:nano-10gx:compatible` | Nano 10GX | Compatibility profile using the same source-defined area set as native Nano 10GX mode. |
| `toyopuc:pc10g:standard-pc3jg` | PC10G | PC3JG-compatible standard profile with B, EB, U, GM/GX/GY, ES, EN, and H areas. |
| `toyopuc:pc10g:pc10` | PC10G | PC10 profile with upper split ranges, U, EB, FR, and PC10 addressing enabled. |
| `toyopuc:pc3jx:pc3-separate` | PC3JX | PC3 separate profile with prefixed basic areas, B, ES, EN, H, and U. |
| `toyopuc:pc3jx:plus-expansion` | PC3JX | Plus expansion profile with GM/GX/GY and U. |
| `toyopuc:pc3jg:pc3jg` | PC3JG | PC3JG profile with B, GM/GX/GY, U, and EB. |
| `toyopuc:pc3jg:pc3-separate` | PC3JG | PC3 separate profile with B, GM/GX/GY, U, and EB. |

## Supported device types

| Device family | Description |
| --- | --- |
| `D` | Data registers for ordinary word reads and writes. |
| `M`, `X`, `Y` | Internal, input, and output relay bit devices such as `P1-M0000`. |
| `T`, `C` | Timer and counter bit families with packed word/byte forms where supported. |
| `S`, `N`, `R` | Special, file, and register word families. |
| `ES`, `EN`, `H` | Direct extended word areas. |
| `U`, `EB` | Larger direct extended word areas, with range and frame behavior selected by profile. |
| `EP`, `EK`, `EV`, `ET`, `EC`, `EL`, `EX`, `EY`, `EM`, `GM`, `GX`, `GY` | Direct extended bit families with packed word/byte forms where supported. |
| `FR` | File-register flash words that use explicit FR read/write/commit helpers. |

See [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md) for the full table.
Use `ReadNamedAsync` when you want a mixed snapshot such as `P1-D0000` and `P1-M0000`.

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

## Documentation links

| Page | Link |
| --- | --- |
| Getting started | [docsrc/user/GETTING_STARTED.md](docsrc/user/GETTING_STARTED.md) |
| Usage guide | [docsrc/user/USAGE_GUIDE.md](docsrc/user/USAGE_GUIDE.md) |
| Supported registers | [docsrc/user/SUPPORTED_REGISTERS.md](docsrc/user/SUPPORTED_REGISTERS.md) |
| PLC profiles | [docsrc/user/PROFILES.md](docsrc/user/PROFILES.md) |
| Examples | [examples/README.md](examples/README.md) |

## Hardware verified

Physical communication has been verified with direct TOYOPUC-Plus, relay Nano 10GX, and direct PC10G PLCs.

## License and registry

Distributed under the [MIT License](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE).
Package registry: <https://www.nuget.org/packages/PlcComm.Toyopuc/>
