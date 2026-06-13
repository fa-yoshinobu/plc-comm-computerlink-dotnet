# Profiles

## Intro

Each profile selects frame type, access rules, and device ranges.

## Profiles table

| Profile string | Hardware | Frame/mode | Key devices available | Notes |
| --- | --- | --- | --- | --- |
| `toyopuc:generic` | Any TOYOPUC Computer Link target | Generic default | P/K/V/T/C/L/X/Y/M, S/N/R/D, B, EP/EK/EV/ET/EC/EL/EX/EY/EM/GM/GX/GY, ES/EN/H/U/EB, FR | Broadest source-defined profile. |
| `toyopuc:plus:standard` | TOYOPUC-Plus | Standard | P/K/V/T/C/L/X/Y/M, S/N/R/D, EP/EK/EV/ET/EC/EL/EX/EY/EM, ES/EN/H | Standard TOYOPUC-Plus address set. |
| `toyopuc:plus:extended` | TOYOPUC-Plus | Extended | Standard plus GM/GX/GY and U | The getting-started examples use this profile. |
| `toyopuc:nano-10gx:native` | Nano 10GX | Native | Upper split P/V/T/C/L/M, S/N, R/D, U/EB, FR, GM/GX/GY | Supports larger U, EB, and FR ranges from source. |
| `toyopuc:nano-10gx:compatible` | Nano 10GX | Compatible | Same source-defined area set as native Nano 10GX | Use this when your route or hardware is configured for compatible access. |
| `toyopuc:pc10g:standard-pc3jg` | PC10G | Standard PC3JG-compatible | P/K/V/T/C/L/X/Y/M, S/N/R/D, B, U, EB, GM/GX/GY, ES/EN/H | FR is not included in this profile. |
| `toyopuc:pc10g:pc10` | PC10G | PC10 | Upper split P/V/T/C/L/M, S/N, R/D, B, U/EB, FR, GM/GX/GY | PC10 addressing is enabled for upper U, EB, FR, and upper bit ranges. |
| `toyopuc:pc3jx:pc3-separate` | PC3JX | PC3 separate | P/K/V/T/C/L/X/Y/M, S/N/R/D, B, ES/EN/H/U | GM/GX/GY, EB, and FR are not included. |
| `toyopuc:pc3jx:plus-expansion` | PC3JX | Plus expansion | P/K/V/T/C/L/X/Y/M, S/N/R/D, GM/GX/GY, ES/EN/H/U | B, EB, and FR are not included. |
| `toyopuc:pc3jg:pc3jg` | PC3JG | PC3JG | P/K/V/T/C/L/X/Y/M, S/N/R/D, B, GM/GX/GY, ES/EN/H/U/EB | FR is not included. |
| `toyopuc:pc3jg:pc3-separate` | PC3JG | PC3 separate | P/K/V/T/C/L/X/Y/M, S/N/R/D, B, GM/GX/GY, ES/EN/H/U/EB | Uses the PC3 separate addressing options. |

## How to select

```csharp
using PlcComm.Toyopuc;
var options = new ToyopucConnectionOptions("192.168.250.100")
    { PlcProfile = "toyopuc:plus:extended" };
```

## Profile-specific cautions

| Profile string | Caution |
| --- | --- |
| `toyopuc:generic` | This profile enables broad source-defined ranges. Use a hardware-specific profile when you know the PLC model. |
| `toyopuc:plus:standard` | U, EB, FR, GM, GX, and GY are not in the standard TOYOPUC-Plus source area set. |
| `toyopuc:plus:extended` | U is available only through the source-defined TOYOPUC-Plus extended range, not the larger PC10 U range. |
| `toyopuc:nano-10gx:native` | Relay and direct routes still require explicit connection and hop configuration. |
| `toyopuc:nano-10gx:compatible` | Relay topology is not auto-discovered; configure `RelayHops` when you need multi-hop access. |
| `toyopuc:pc10g:standard-pc3jg` | FR is not available in this profile; choose `toyopuc:pc10g:pc10` for FR access. |
| `toyopuc:pc10g:pc10` | Profile-specific range limits matter for upper split ranges such as P1-D2FFF and U/EB high ranges. |
| `toyopuc:pc3jx:pc3-separate` | GM/GX/GY, EB, and FR are not in the source area set. |
| `toyopuc:pc3jx:plus-expansion` | B, EB, and FR are not in the source area set. |
| `toyopuc:pc3jg:pc3jg` | FR is not in the source area set. |
| `toyopuc:pc3jg:pc3-separate` | FR is not in the source area set. |
