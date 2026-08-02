# Testing Guide

This document describes the current test structure for `PlcComm.Toyopuc`.

## Unit / Integration Tests

The automated test suite is under `tests/PlcComm.Toyopuc.Tests/`.

Run with:

```powershell
dotnet test PlcComm.Toyopuc.sln -v normal
```

Expected result: all tests pass, 0 warnings.

## Simulator

`scripts/sim_server.py` in `plc-comm-computerlink-python` is a development helper.

Currently supported commands in the simulator:

- basic single-point: `CMD=1C/1D/1E/1F/20/21`
- basic multi-point: `CMD=22/23/24/25`
- extended single/contiguous: `CMD=94/95/96/97`
- extended multi-point: `CMD=98/99`
- PC10 block/multi: `CMD=C2/C3/C4/C5`
- relay: `CMD=60`
- clock: `CMD=32 70 00` / `CMD=32 71 00`
- CPU status: `CMD=32 11 00`

Not modeled accurately enough to treat as hardware-equivalent:

- FR commit behavior
- Hardware-specific NAK / error responses

## Cross-Library Parity

The .NET library is kept in sync with `plc-comm-computerlink-python`.

When adding or changing a method, verify:

1. The equivalent Python method exists and has the same semantics.
2. The `.Async` counterpart exists in `ToyopucClient.Async.cs`.
3. The relay variant exists where applicable.

## CI

CI runs on every push and pull request via `.github/workflows/ci.yml`. The
Windows job checks no-auto-publish policy, the exported source archive,
restore/build/test, the generated API reference, documented API differences,
NuGet package contents, and formatting. A separate Linux job runs the bounded
network-contract smoke tests.
