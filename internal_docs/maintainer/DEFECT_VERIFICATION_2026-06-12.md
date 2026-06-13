# Toyopuc Defect Verification - 2026-06-12

This file keeps the maintainer-level conclusions from the 2026-06-12
Toyopuc/ComputerLink investigation. It replaces the temporary workspace-level
report.

## Release Status

No release-blocking .NET Toyopuc defect remains from this investigation.

Related detailed records:

- `TESTRESULTS.md`
- `PYTHON_PORTING_NOTES.md`
- `AUTOMATED_TEST_PLAN.md`

## Fixed and Verified

- Program packed-word sparse `ReadMany` all-zero behavior.
- Program packed-word sparse write address encoding.
- `CMD=A0` CPU status frame layout.
- `FR` `CMD=C2/C3` exact limit and helper split behavior.
- Protocol max-count guards and fail-fast behavior.
- `EB` extended-No route limit for `CMD=94..99`.
- Relay `P1-L1:N2` read/write/stress behavior.

## Program Packed-Word Sparse Access

Root cause:

- `CMD=98/99` word-point multi access requires monitor byte addresses.
- The old sparse route used the word address used by `CMD=94/95`.
- Correct `P1-V000W` request uses byte address `0x00A0`, not `0x0050`.

Representative corrected sparse read:

```text
request : 00 00 0A 00 98 00 00 02 01 A0 00 01 A4 00
response: 80 00 05 00 98 50 00 40 00
```

Result:

- contiguous `P1-V000W..P1-V003W`: `0x0050, 0x0000, 0x0040, 0x0100`
- sparse `P1-V000W,P1-V002W`: `0x0050, 0x0040`
- direct and relay paths are fixed
- cross-verify frame parity is fixed

## Manual Audit Items

### `CMD=A0`

- Correct request: `A0 00 11 00`
- Old request: `A0 01 10`
- Old request returned `rc=0x10 / error_code=0x24`
- Correct request returned normal CPU status data

### `FR` `CMD=C2/C3`

- Manual limit: `0x03F0` bytes
- Helper chunk limit: `0x01F8` words
- `0x01F9` word helper requests split explicitly
- direct low-level over-limit frames fail before sending

### Max-Count Semantics

Automatic segmentation is not used as a hidden convenience feature for normal
read/write calls. Over-limit single-telegram requests fail before sending. Only
explicit helper routes whose contract already includes chunking may split.

Reason:

- read snapshots can split across time
- writes can partially succeed or fail
- changing one logical request into multiple telegrams silently is a bug risk

### `EB` Extended-No Guard

For `CMD=94..99`, `EB` extended-No access is restricted to
`EB00000..EB1FFFF`. Wider `EB` access belongs to the PC10 route when the
selected profile supports it.

## Nano 10GX `FR` Verification

Target:

- `192.168.250.100:1025/tcp`
- Nano 10GX with `FR` visible

Verified:

- `FR000000` read
- exact max `CMD=C2` read at `0x03F0` bytes
- `CMD=C2` `0x03F1` byte guard
- `ReadFrWords(..., 0x01F8)`
- `ReadFrWords(..., 0x01F9)` split helper
- `WriteFr(..., commit:false)` write/readback/restore
- exact max `CMD=C3` write/readback/restore
- split `CMD=C3` write/readback/restore
- `CMD=CA` commit/wait/readback
- restore original values and commit
- power cycle / CPU reset persistence

Persistence result:

- committed marker `0xCA10..0xCA13` survived reset
- original values `0x57AB,0x57AC,0x57AD,0x57AE` were restored and committed

## Relay `P1-L1:N2` Verification

Target:

- `192.168.250.100:1025/tcp`
- `192.168.250.100:1027/udp`
- relay target: Nano 10GX
- hops: `P1-L1:N2`
- profile: `toyopuc:nano-10gx:compatible`

Verified:

- CPU status read
- `P1-D0000` read
- `P1-D0000` write/readback
- count probe `1/8/16/32/64/128/256`
- single TCP client sequential read-only stress: 100 iterations
- single UDP client sequential read-only stress: 500 iterations with PC local
  port `12000`
- TCP + UDP simultaneous read-only stress: 500 iterations each
- 30-minute relay write/readback soak:
  - `P1-D0000`
  - alternating `0x1111` / `0x2222`
  - 1029 iterations
  - 0 failures
  - final restore to `0x270F` verified

Observed target condition:

- two simultaneous TCP clients against the same relay hop can produce a socket
  error on one client
- immediate single-client post-check succeeds
- classify as same-hop simultaneous-use contention, not a library frame bug

## Remaining Work

Release blockers:

- none known

Future coverage only:

- deferred profile read-only sweeps:
  - `toyopuc:plus:standard`
  - `toyopuc:nano-10gx:native`
  - `toyopuc:pc3jx:pc3-separate`
  - `toyopuc:pc3jg:pc3jg`
  - `toyopuc:pc3jg:pc3-separate`
