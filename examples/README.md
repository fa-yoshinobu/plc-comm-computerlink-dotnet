# Examples

## What is in this directory

This directory contains small runnable programs for first reads, high-level API exploration, long-running polling, hardware smoke tests, write-limit checks, and packed-bit behavior probes. Start with `PlcComm.Toyopuc.MinimalRead` or `PlcComm.Toyopuc.HighLevelSample` before using the validation utilities.

## How to run

```powershell
dotnet run --project examples\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1025 tcp P1-D0000 "toyopuc:plus:extended"
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1025 tcp "toyopuc:plus:extended"
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.SoakMonitor -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:nano-10gx:compatible" --devices P1-D0000,P1-M0000 --interval 2s --duration 1m
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.SmokeTest -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:plus:extended" --device P1-D0000
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.WriteLimitProbe -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:pc10g:pc10"
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.BitPatternProbe -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:pc10g:pc10"
```

## Simulator

Use the sibling Python repository as the simulator source:

```powershell
cd <path-to-plc-comm-computerlink-python>
python scripts\sim_server.py --host 127.0.0.1 --port 15000
```

Then run the smoke test against the simulator:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp --profile "toyopuc:generic" --device P1-D0000 --skip-status-read --skip-clock-read
```

## Example index

| Project/File | What it demonstrates |
| --- | --- |
| `examples/PlcComm.Toyopuc.MinimalRead` | Minimal status, clock, and one-device read. |
| `examples/PlcComm.Toyopuc.HighLevelSample` | High-level reads, writes, typed helpers, snapshots, block helpers, polling, and FR helpers. |
| `examples/PlcComm.Toyopuc.SoakMonitor` | Long-running polling with reconnect behavior and optional logs. |
| `examples/PlcComm.Toyopuc.SmokeTest` | Hardware validation, read/write restore checks, relay checks, FR checks, and profile suites. |
| `examples/PlcComm.Toyopuc.WriteLimitProbe` | Safe write-limit confirmation for selected word ranges. |
| `examples/PlcComm.Toyopuc.BitPatternProbe` | Packed word and byte readback behavior for bit families. |
| `examples/run_validation.ps1` | Wrapper for profile-specific smoke validation targets. |
| `examples/probe_direct_length_limits.ps1` | Wrapper for direct write-limit probing. |
| `examples/probe_relay_length_limits.ps1` | Wrapper for relay read/write length probing. |
| `examples/run_fr_range_change_proof.ps1` | FR range before/after dump and change proof workflow. |
