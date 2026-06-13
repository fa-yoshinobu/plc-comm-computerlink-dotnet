# TODO: Toyopuc Computer Link .NET

This file tracks the remaining tasks and known issues for the Toyopuc Computer Link .NET library.

## 1. Active Follow-Up

- [x] **Smoke / Soak Coverage**: Re-run the example applications against current hardware and capture the results as reproducible reports.

## 2. Cross-Library API Alignment

- [ ] **Unify PLC profile naming across libraries**: Review `DeviceProfile` as the public Computer Link selector and align it with the cross-library `PlcProfile` naming policy where practical. Standard saved/displayed profile names should converge on one canonical form, with legacy names accepted only as input aliases.
