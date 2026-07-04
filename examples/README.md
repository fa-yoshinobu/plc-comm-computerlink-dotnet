# Examples

## What is in this directory

This directory contains small runnable programs for first reads and high-level API exploration. Maintainer validation tools live under `tools/validation/`.

Use only test addresses that are safe for your PLC program before you run any write example.

## How to run

```powershell
dotnet run --project examples\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1025 tcp P1-D0000 "toyopuc:plus:extended"
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1025 tcp "toyopuc:plus:extended"
```

```powershell
dotnet run --project examples\PlcComm.Toyopuc.PollingReconnectSample -- 192.168.250.100 1025 tcp "toyopuc:plus:extended" P1-D0100 U 1
```

## Simulator

Use the sibling Python repository as the simulator source. If it is checked out
elsewhere, set `$pythonRepo` to that path, replacing `<path-to-plc-comm-computerlink-python>`.

```powershell
$pythonRepo = "..\plc-comm-computerlink-python"
Set-Location $pythonRepo
python scripts\sim_server.py --host 127.0.0.1 --port 15000
```

Then run the minimal read example against the simulator:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.MinimalRead -- 127.0.0.1 15000 tcp P1-D0000 "toyopuc:generic"
```

## Example index

| Project/File | What it demonstrates |
| --- | --- |
| `examples/PlcComm.Toyopuc.MinimalRead` | Minimal status, clock, and one-device read. |
| `examples/PlcComm.Toyopuc.HighLevelSample` | High-level reads, writes, typed helpers, snapshots, block helpers, polling, and FR helpers. |
| `examples/PlcComm.Toyopuc.PollingReconnectSample` | Read-only polling loop with automatic reconnect and backoff after transport loss. |
