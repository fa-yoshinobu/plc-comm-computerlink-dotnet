# TODO: Toyopuc Computer Link .NET

This file tracks the remaining tasks and known issues for the Toyopuc Computer Link .NET library.

## 1. Active Follow-Up

- [ ] **Smoke / Soak Coverage**: Re-run the example applications against current hardware and capture the results as reproducible reports.

## 2. Completed Recently

- [x] **Align the high-level helper surface**: The public entry points are aligned with the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [x] **Promote reusable address helpers**: Device parse/normalize/format helpers are exposed through `ToyopucAddress`.
- [x] **Define a stable connection-options model**: Profile selection, relay hops, local port, retries, and retry delay are carried by `ToyopucConnectionOptions`.
- [x] **Preserve semantic atomicity by default**: Explicit `*SingleRequestAsync` and `*ChunkedAsync` helpers keep protocol-defined boundary splitting visible to the caller.
