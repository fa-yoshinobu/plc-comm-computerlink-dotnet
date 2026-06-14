# TODO: Toyopuc Computerlink .NET

This file tracks the remaining tasks and known issues for the Toyopuc Computerlink .NET library.

## 1. Active Follow-Up

- [x] **Smoke / Soak Coverage**: Re-run the example applications against current hardware and capture the results as reproducible reports.

## 2. Cross-Library API Alignment

- [x] **Unify PLC profile naming across libraries**: `PlcProfile` is now the public Computerlink selector. Profile definitions use `ToyopucPlcProfile(s)` and canonical `toyopuc:<model>:<mode>` names only; legacy display names and short aliases are intentionally rejected.
