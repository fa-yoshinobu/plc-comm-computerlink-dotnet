# Gotchas

## A range is rejected instead of being split

This is intentional. A range crossing a command group, request limit, or PC10/FR block boundary would require multiple requests and could mix values from different PLC times. Split it explicitly in application code only after deciding how to handle time differences and partial success.

## `:D` and `.D` are different

`P1-D0000:D` reads an unsigned 32-bit value. `P1-D0000.D` reads bit 13 of one word. Named addresses without a colon type or bit suffix are rejected.

## FR write success is not flash persistence

`WriteFrWorkArea` updates the work area only. `CommitFrBlock` is a separate request and reports request acceptance, not completion. Do not put flash commits in a frequent runtime write loop.

FR work-area words accept only integral values in `0..65535`. Boolean, fractional, string, negative, and overflowing values are rejected before communication rather than coerced or masked.

## Relay is never inferred

Both direct and relay sessions require an explicit route. Relay routes require valid hops with link `0..255` and station `1..65535`; invalid values are rejected rather than masked.

## UDP local port zero is not source port zero

`LocalPort = 0` asks the operating system to bind an available ephemeral port. A new session may receive a different port. Set a fixed UDP local port only when the network configuration requires it.

## The PLC clock has no century or timezone

Pass an explicit century to `AsDateTime(yearBase)` and clock writes. Use a timezone-unspecified `DateTime` representing PLC local time.

## A timeout discards the transport session

This prevents a late response from an expired request from being consumed as the response to a later request. A subsequent ordinary command may reconnect. Cancellation is stricter: after canceling an active operation, call `OpenAsync()` explicitly before reuse, and treat a canceled state-changing request as outcome-unknown when the library reports `ToyopucOperationOutcomeUnknownException`.

For shared wiring, setup, device ranges, and PLC response troubleshooting, use the Computerlink pages on the PLC Comm documentation site.
