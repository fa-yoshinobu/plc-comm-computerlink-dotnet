# Error Codes

This page summarizes TOYOPUC Computerlink response errors that users commonly
see through this library. It is not a complete manufacturer code table; use the
JTEKT TOYOPUC manuals for formal definitions.

When the PLC returns a response error, `ToyopucError` includes text such as
`error_code=0x40`. Timeouts use `ToyopucTimeoutError`, and malformed frames use
`ToyopucProtocolError`.

## Common PLC Error Codes

| Code | Typical cause | First check |
| --- | --- | --- |
| `0x40` | Address or address plus count is outside the CPU range. This is also the common result when FR is not exposed on the tested unit. | Check the selected profile, address range, and count. |
| `0x24` | Subcommand is not supported by the CPU or routed target. | Check whether the feature exists on that CPU; for example some targets reject `A0`. |
| `0x23` | Command code is not supported. | Check the selected helper and profile. |
| `0x31` | Write or function call is prohibited while the sequence is running. | Check PLC run/write settings before retrying. |
| `0x34` | Access is prohibited by configuration. | Check PLC communication and protection settings. |
| `0x41` | Word or byte count is outside the permitted range. | Split the request or reduce the count. |
| `0x52` | Timer/counter set-value and current-value command type do not match. | Check whether the helper targets a preset or current value. |
| `0x66`, `0x70`, `0x72` | Relay link module did not answer or could not execute the request. | Check relay hops and the target PLC path. |
| `0x73` | Relay command collision on the same link module; retry is appropriate. | Retry after a short delay or reduce concurrent relay access. |
| `0x11` | CPU module hardware failure. | Check the PLC CPU status before continuing. |

## Library Exceptions

| Exception | Meaning |
| --- | --- |
| `ToyopucError` | Base error, including PLC response errors. |
| `ToyopucProtocolError` | A response frame was malformed or unexpected. |
| `ToyopucTimeoutError` | Send/receive timed out. |

See [Gotchas](GOTCHAS.md) and [Troubleshooting](TROUBLESHOOTING.md) for common
addressing, relay, FR, and write-permission symptoms.
