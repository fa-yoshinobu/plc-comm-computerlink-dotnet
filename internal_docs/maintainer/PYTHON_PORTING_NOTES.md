# Python Porting Notes

This document tracks behavior checks between this .NET implementation and the
upstream Python implementation.

## Resolved Cross-Implementation Defect

### PC10 packed-word `ReadMany` may return all zeros on non-consecutive lists

Discovered on `2026-03-12` during direct `Nano 10GX:Compatible mode` checks in
the .NET implementation (before library-side fix).

Resolution status:

- Resolved in Python and .NET.
- The direct and relay read/write paths were fixed so program packed-word
  addresses use the protocol's byte-address form where required.
- Live verification on `2026-06-12` confirmed that sparse packed-word reads no
  longer collapse to all-zero.
- Cross-verify frame parity was also checked, so the checklist below is kept as
  historical context rather than an active TODO.

#### Symptom

- Single/contiguous reads of packed word devices (for example `P1-V000W` range)
  return non-zero values.
- Non-consecutive packed-word `ReadMany` (especially lists spanning multiple
  address sub-ranges, such as `...V00xW` then `...V10xW`) may return all zeros.

#### Trigger Pattern

- Access kind: `pc10-word`
- Device style: packed word (`W` suffix)
- Request style: one `ReadMany` list containing gaps (not a single consecutive
  block)

#### Impact

- Monitoring UIs or batch polling can display false zeros.
- Readback-based validation can incorrectly report bad device state.

#### .NET-side mitigation already applied

- For `pc10-word` + packed-word list in `ReadMany` / `RelayReadMany`, the
  client now splits the list into consecutive segments and performs `PC10 block
  read (C2)` per segment instead of `PC10 multi read (C4)` over the full sparse
  list.

---

## Historical Checklist for the Original Python Program

This checklist was used to determine whether the same issue existed in Python.
It is complete and no longer represents open work.

### 1. Preconditions

- [x] PLC/profile where packed-word PC10 access is available (for example
      `Nano 10GX:Compatible mode` direct).
- [x] Frame logging enabled (to confirm command type and payload).
- [x] A packed-word area that has known non-zero points at the test time.

### 2. Reference Read (contiguous, expected-good)

- [x] Read a contiguous packed-word block (example: `P1-V000W` count `16`).
- [x] Confirm at least one value is non-zero.
- [x] Confirm transport uses normal PC10 block-read path for contiguous access.

Pass condition:
- contiguous read returns plausible values (not forced all-zero).

### 3. Sparse `ReadMany` Repro

- [x] Build one sparse packed-word device list containing a range gap.
- [x] Example list pattern:
      - `P1-V000W` .. `P1-V00FW` (16 points)
      - `P1-V100W` .. `P1-V12FW` (48 points)
      - total `64` points in one `ReadMany`.
- [x] Execute one-shot `ReadMany` with that list.

Pass condition:
- result is not uniformly zero when step 2 proved non-zero data exists.

Fail condition (suspected same defect):
- sparse `ReadMany` returns all zeros while contiguous read in step 2 returns
  non-zero.

### 4. Frame-Level Confirmation

- [x] Inspect captured request frames for sparse `ReadMany`.
- [x] Check whether Python sends one `PC10 multi-read (C4)` over sparse packed
      words.
- [x] Compare with segmented `PC10 block-read (C2)` behavior.

Interpretation:
- If sparse packed-word `ReadMany` uses `C4` and reproduces all-zero, defect is
  very likely shared.
- If Python already segments to `C2` and values are correct, this defect is not
  present (or already fixed) there.

### 5. Relay Variant (optional)

- [x] Repeat steps 2-4 through relay path (if relay is used in production).
- [x] Confirm behavior is consistent between direct and relay.

### 6. Regression Guard (recommended)

- [x] Add an automated test that compares:
      - contiguous packed-word read result
      - sparse packed-word `ReadMany` result
- [x] Assert that sparse read does not collapse to all-zero when contiguous
      read confirms non-zero in same cycle.
- [x] Assert frame strategy for sparse packed words:
      segmented block reads are preferred over one sparse multi-read.

---

## Quick Triage Conclusion Template

When reporting Python verification results, record:

- target/profile:
- direct or relay:
- contiguous packed read sample:
- sparse `ReadMany` sample:
- frame command type used (`C2` segmented / `C4` sparse):
- conclusion: `reproduced` / `not reproduced` / `inconclusive`
