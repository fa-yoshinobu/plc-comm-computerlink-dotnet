# Supported registers

This page lists device families supported by the .NET (C#) high-level API.

## Bit device families

| Family | Kind | Example | Notes |
| --- | --- | --- | --- |
| `P` | prefixed bit | `P1-P0000` | Shared relay family; profile ranges may include upper split ranges. |
| `K` | prefixed bit | `P1-K0000` | Keep relay family. |
| `V` | prefixed bit | `P1-V0000` | V relay family; some hardware treats low ranges as target-specific. |
| `T` | prefixed bit | `P1-T0000` | Timer bit family. |
| `C` | prefixed bit | `P1-C0000` | Counter bit family. |
| `L` | prefixed bit | `P1-L0000` | Link relay family; profile ranges may include upper split ranges. |
| `X` | prefixed bit | `P1-X0000` | Input relay family. |
| `Y` | prefixed bit | `P1-Y0000` | Output relay family. |
| `M` | prefixed bit | `P1-M0000` | Internal relay family; profile ranges may include upper split ranges. |
| `EP` | direct extension bit | `EP0000` | Extended P bit family. |
| `EK` | direct extension bit | `EK0000` | Extended K bit family. |
| `EV` | direct extension bit | `EV0000` | Extended V bit family. |
| `ET` | direct extension bit | `ET0000` | Extended T bit family. |
| `EC` | direct extension bit | `EC0000` | Extended C bit family. |
| `EL` | direct extension bit | `EL0000` | Extended L bit family. |
| `EX` | direct extension bit | `EX0000` | Extended X bit family. |
| `EY` | direct extension bit | `EY0000` | Extended Y bit family. |
| `EM` | direct extension bit | `EM0000` | Extended M bit family. |
| `GM` | direct extension bit | `GM0000` | Global M bit family where the selected profile enables it. |
| `GX` | direct extension bit | `GX0000` | Global X bit family where the selected profile enables it. |
| `GY` | direct extension bit | `GY0000` | Global Y bit family where the selected profile enables it. |

## Word device families

| Family | Kind | Example | Notes |
| --- | --- | --- | --- |
| `S` | prefixed word | `P1-S0000` | Special register family. |
| `N` | prefixed word | `P1-N0000` | File register word family. |
| `R` | prefixed word | `P1-R0000` | Register word family. |
| `D` | prefixed word | `P1-D0000` | Data register family. |
| `B` | direct word | `B0000` | Direct word area where the selected profile enables it. |
| `ES` | direct extension word | `ES00000` | Extended special register family. |
| `EN` | direct extension word | `EN00000` | Extended file register family. |
| `H` | direct extension word | `H00000` | Extended H word family. |
| `U` | direct extension word | `U00000` | Extended U word family; profile and addressing options select standard or PC10 routing. |
| `EB` | direct extension word | `EB00000` | Extended block word family where the selected profile enables it. |
| `FR` | direct FR word | `FR000000` | File-register flash area with two-phase write semantics. |

## Type suffixes

| Form | Example | Meaning |
| --- | --- | --- |
| `:U` | `P1-D0100:U` | Unsigned 16-bit word. |
| `:S` | `P1-D0100:S` | Signed 16-bit word. |
| `:D` | `P1-D0100:D` | Unsigned 32-bit dword from two words. |
| `:L` | `P1-D0100:L` | Signed 32-bit long from two words. |
| `:F` | `P1-D0100:F` | 32-bit floating point value from two words. |
| `.n` | `P1-D0100.3` | Bit `n` inside a word, where `n` is `0` through `F`. |

## Addressing notes

- Basic families (D/M/X/Y/T/C/L/N/R/S, plus P/K/V where available) require a `P1-`, `P2-`, or `P3-` prefix.
- `FR` is a separate flash storage area with two-phase write semantics.
- `ES` and `EN` are extension areas and do not require a prefix.
- `.D` on a word address means bit 13. Use `:D` with a colon for 32-bit dword access.
- `W`, `H`, and `L` appended to bit-area addresses are packed word, high byte, and low byte forms.

See [PROFILES.md](PROFILES.md) for per-profile range limits.
