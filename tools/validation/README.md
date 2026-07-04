# Computerlink .NET Validation Tools

These projects and scripts are maintainer lab validation tools, not ordinary user examples.
They can write many PLC devices, run long polling loops, or create large evidence logs.

Run them from the repository root against a prepared test PLC only.

```powershell
dotnet run --project tools\validation\PlcComm.Toyopuc.SmokeTest -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:plus:extended" --device P1-D0000
dotnet run --project tools\validation\PlcComm.Toyopuc.SoakMonitor -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:nano-10gx:compatible" --devices P1-D0000,P1-M0000 --interval 2s --duration 1m
dotnet run --project tools\validation\PlcComm.Toyopuc.WriteLimitProbe -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:pc10g:pc10"
dotnet run --project tools\validation\PlcComm.Toyopuc.BitPatternProbe -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "toyopuc:pc10g:pc10"
```

Wrapper scripts:

```powershell
powershell -ExecutionPolicy Bypass -File tools\validation\run_validation.ps1 -Target plus
powershell -ExecutionPolicy Bypass -File tools\validation\probe_direct_length_limits.ps1 -Profile "toyopuc:pc10g:pc10"
powershell -ExecutionPolicy Bypass -File tools\validation\probe_relay_length_limits.ps1 -Profile "toyopuc:nano-10gx:compatible"
powershell -ExecutionPolicy Bypass -File tools\validation\run_fr_range_change_proof.ps1
```

Keep normal user-facing examples in `examples/`.
