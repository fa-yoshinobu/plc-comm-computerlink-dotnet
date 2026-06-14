# Library PLC Profile Specification

This document describes the machine presets and range rules implemented by the
.NET library.

This is an internal implementation document.
For protocol background and upstream model research, see:

- [`plc-comm-computerlink-python/docsrc/user/MODEL_RANGES.md`](https://github.com/fa-yoshinobu/plc-comm-computerlink-python/blob/main/docsrc/user/MODEL_RANGES.md)
- [`plc-comm-computerlink-python/internal_docs/maintainer/TESTING_GUIDE.md`](https://github.com/fa-yoshinobu/plc-comm-computerlink-python/blob/main/internal_docs/maintainer/TESTING_GUIDE.md)
- [`PYTHON_PORTING_NOTES.md`](PYTHON_PORTING_NOTES.md)

## Source Of Truth

The source of truth is code, not UI logic.

| Concern | Source |
| --- | --- |
| profile definitions | [`../../src/Toyopuc/ToyopucPlcProfiles.cs`](../../src/Toyopuc/ToyopucPlcProfiles.cs) |
| profile data types | [`../../src/Toyopuc/ToyopucPlcProfile.cs`](../../src/Toyopuc/ToyopucPlcProfile.cs) |
| lookup API | [`../../src/Toyopuc/ToyopucDeviceCatalog.cs`](../../src/Toyopuc/ToyopucDeviceCatalog.cs) |
| addressing option switches | [`../../src/Toyopuc/ToyopucAddressingOptions.cs`](../../src/Toyopuc/ToyopucAddressingOptions.cs) |
| range definitions | [`../../src/Toyopuc/ToyopucPlcProfiles.cs`](../../src/Toyopuc/ToyopucPlcProfiles.cs) |

Applications built on `PlcComm.Toyopuc` must not maintain their own area
tables or model-specific upper bounds.

## Exposed Profiles

The library currently exposes these canonical profile names:

- `Generic`
- `toyopuc:plus:standard`
- `toyopuc:plus:extended`
- `toyopuc:nano-10gx:native`
- `toyopuc:nano-10gx:compatible`
- `toyopuc:pc10g:standard-pc3jg`
- `toyopuc:pc10g:pc10`
- `toyopuc:pc3jx:pc3-separate`
- `toyopuc:pc3jx:plus-expansion`
- `toyopuc:pc3jg:pc3jg`
- `toyopuc:pc3jg:pc3-separate`

The exact per-area matrix is implemented in
[`../../src/Toyopuc/ToyopucPlcProfiles.cs`](../../src/Toyopuc/ToyopucPlcProfiles.cs).

## Data Model

`ToyopucAreaDescriptor` supports disjoint ranges.

| Field | Meaning |
| --- | --- |
| `Area` | area name such as `M`, `D`, `U`, `FR` |
| `DirectRanges` | supported direct ranges |
| `PrefixedRanges` | supported `P1/P2/P3` ranges |
| `DirectRange` | convenience property when the direct side has exactly one range |
| `PrefixedRange` | convenience property when the prefixed side has exactly one range |
| `SupportsPackedWord` | whether packed `W` rows such as `M000W` are allowed |
| `AddressWidth` | displayed address width |
| `SuggestedStartStep` | dropdown generation step for UI start addresses |

Interpretation rules:

- `DirectRanges.Count == 0` means direct access is not supported.
- `PrefixedRanges.Count == 0` means `P1/P2/P3` access is not supported.
- If `DirectRanges.Count > 1` or `PrefixedRanges.Count > 1`, the area has disjoint segments.
- In the current profile set, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` are modeled as `P1/P2/P3` only. High-level device resolution requires the `P1-`, `P2-`, or `P3-` prefix for these families.

## Library API Surface

Applications are expected to query model information through
`ToyopucDeviceCatalog`.

| API | Purpose |
| --- | --- |
| `ToyopucPlcProfiles.GetNames()` | list exposed profile names |
| `ToyopucPlcProfiles.FromName(profile)` | resolve a canonical profile name |
| `ToyopucDeviceCatalog.GetAreas(prefixed, profile)` | list usable areas for direct or prefixed access |
| `ToyopucDeviceCatalog.GetAreaDescriptor(area, profile)` | get metadata for one area |
| `ToyopucDeviceCatalog.GetSupportedRanges(area, prefixed, profile)` | get all implemented ranges for one area |
| `ToyopucDeviceCatalog.GetSupportedRange(area, prefixed, profile)` | get one range only when the area is not disjoint |
| `ToyopucDeviceCatalog.IsSupportedIndex(area, index, prefixed, profile)` | validate one numeric index |
| `ToyopucDeviceCatalog.GetSuggestedStartAddresses(area, prefix, profile)` | generate UI-friendly start candidates |

`GetSuggestedStartAddresses(...)` iterates every supported segment and appends
segment-end candidates when needed, so disjoint ranges such as
`0000-01FF,1000-17FF` remain reachable from the UI.

## Addressing Options

`ToyopucAddressingOptions.FromProfile(...)` maps the profiles to switching flags
used by the resolver and high-level client.

| Profile | `UseUpperUPc10` | `UseEbPc10` | `UseFrPc10` | `UseUpperBitPc10` | `UseUpperMBitPc10` |
| --- | --- | --- | --- | --- | --- |
| `Generic` | `true` | `true` | `true` | `true` | `true` |
| `toyopuc:plus:standard` | `false` | `false` | `false` | `false` | `false` |
| `toyopuc:plus:extended` | `false` | `false` | `false` | `false` | `false` |
| `toyopuc:nano-10gx:native` | `true` | `true` | `true` | `true` | `true` |
| `toyopuc:nano-10gx:compatible` | `true` | `true` | `true` | `true` | `true` |
| `toyopuc:pc10g:standard-pc3jg` | `false` | `true` | `false` | `false` | `false` |
| `toyopuc:pc10g:pc10` | `true` | `true` | `true` | `true` | `true` |
| `toyopuc:pc3jx:pc3-separate` | `false` | `false` | `false` | `false` | `false` |
| `toyopuc:pc3jx:plus-expansion` | `false` | `false` | `false` | `false` | `false` |
| `toyopuc:pc3jg:pc3jg` | `false` | `true` | `false` | `false` | `false` |
| `toyopuc:pc3jg:pc3-separate` | `false` | `false` | `false` | `false` | `false` |

## Area Metadata

| Area family | Areas | Address width | Packed `W` | Suggested step |
| --- | --- | --- | --- | --- |
| basic bit | `P K V T C L X Y M` | 4 | yes | `0x10` |
| basic word | `S N R D B` | 4 | no | `0x10` |
| ext bit | `EP EK EV ET EC EL EX EY EM GM GX GY` | 4 | yes | `0x10` |
| ext word | `ES EN H U EB` | 5 | no | `0x100` |
| FR word | `FR` | 6 | no | `0x1000` |

## Notes On Current Profile Intent

These are short reminders only. For exact ranges, use the code.

- `Generic` is the library superset. Basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` are prefixed-only, while `B` and extended families stay direct.
- `toyopuc:plus:standard` keeps prefixed basic families and lower extended families, but does not expose `B`, `U`, `GM/GX/GY`, `EB`, or `FR`.
- `toyopuc:plus:extended` keeps prefixed basic families and adds `GM/GX/GY` and lower `U`.
- `toyopuc:nano-10gx:native` and `toyopuc:nano-10gx:compatible` currently expose the same matrix in the library.
- On the verified `Nano 10GX` relay target, packed-word access aliases `ET` with `EC`, `EX` with `EY`, and `GX` with `GY`.
- `toyopuc:pc10g:pc10` keeps upper `P/V/T/C/L/M/S/N` segments.
- `toyopuc:pc3jx:pc3-separate` keeps `B` but does not expose `GM/GX/GY`.
- `toyopuc:pc3jg:pc3-separate` exposes `EB` without exposing `U`.

## Monitor Behavior Rules

Applications using the high-level API rely on these profile definitions for:

| UI behavior | Depends on profile spec |
| --- | --- |
| device dropdown contents | yes |
| address dropdown contents | yes |
| selected range validation | yes |
| scroll window stop conditions | yes |

That means:

- If a profile does not expose an area, the monitor must not offer it.
- If a profile has disjoint ranges, the monitor must stay inside the segment
  that contains the selected start address.
- The application must not duplicate upper bounds locally. All range queries
  must come from `ToyopucDeviceCatalog`.

## Change Rules

When changing supported ranges:

| Step | Action |
| --- | --- |
| 1 | update `ToyopucPlcProfiles` |
| 2 | verify `ToyopucAddressingOptions` still matches the profile intent |
| 3 | update [`../../tests/PlcComm.Toyopuc.Tests/AddressAndResolverTests.cs`](../../tests/PlcComm.Toyopuc.Tests/AddressAndResolverTests.cs) |
| 4 | update this document |
| 5 | if behavior is externally visible, update [`../../README.md`](../../README.md) |

