# Gotchas

## Single-request ranges are not split

Methods documented as one protocol request reject a range that crosses a command group, request limit, or PC10/FR block boundary. Explicit read aggregates such as `ReadMany` and `ReadDevices` are different: they validate the complete plan first and may split only between complete caller-declared entries while retaining one client FIFO turn. Such results are not PLC-atomic and can contain values from different PLC scan times; a failed later request returns an error without publishing partial results. Writes are never split automatically.

## `:D` and `.D` are different

`P1-D0000:D` reads an unsigned 32-bit value. `P1-D0000.D` reads bit 13 of one word. Named addresses without a colon type or bit suffix are rejected.

## FR write success is not flash persistence

`WriteFrWorkArea` updates the work area only. `CommitFrBlock` is a separate request and reports request acceptance, not completion. Do not put flash commits in a frequent runtime write loop.

FR work-area words accept only integral values in `0..65535`. Boolean, fractional, string, negative, and overflowing values are rejected before communication rather than coerced or masked.

The same no-coercion rule applies to generic writes: semantic bit writes accept only Boolean values (integer `0`/`1` is rejected), bytes accept `0..255`, words accept `0..65535`, and typed dword/float writes reject incompatible or non-finite values before communication.

Raw frame and payload builders are the wire layer and therefore use validated integer `0`/`1` bit fields rather than semantic Boolean values.

## Bit-in-word updates are not PLC-atomic

`WriteBitInWordAsync` reserves one FIFO turn on its local client, but performs
one word read followed by one word write. PLC logic and other connections can
still modify the word between those requests. Use a PLC-side ownership or
handshake contract when that race is unacceptable, and treat a post-send
failure as outcome-unknown until the PLC state is reconciled.

## Relay is never inferred

Both direct and relay sessions require an explicit route. Relay routes require valid hops with link `0..255` and station `1..65535`; invalid values are rejected rather than masked.

## UDP local port zero is not source port zero

`LocalPort = 0` asks the operating system to bind an available ephemeral port. A new session may receive a different port. Set a fixed UDP local port only when the network configuration requires it.

## Connections are IPv4-only

TCP and UDP accept an IPv4 literal or a hostname that resolves to IPv4. An IPv6 literal, including an IPv4-mapped IPv6 literal such as `::ffff:192.0.2.1`, raises `ArgumentException` before a socket is created. When a hostname has multiple results, the library uses the first IPv4 result in resolver order; a hostname with no IPv4 result fails the connection without falling back to IPv6.

## The PLC clock has no century or timezone

Pass an explicit century to `AsDateTime(yearBase)` and clock writes. Use a timezone-unspecified `DateTime` representing PLC local time.

## A timeout discards the transport session

This prevents a late response from an expired request from being consumed as the response to a later request. A subsequent ordinary command may reconnect for TCP or ephemeral-port UDP. A fixed-local-port UDP client is terminal after an uncertain post-send failure: Computerlink has no request serial that can distinguish a late response from a later request to the same endpoint. Create a new client only after late responses can no longer be present, and prefer `LocalPort = 0` unless a fixed source port is required.

Connection timeouts, retry delays, and polling intervals have a common inclusive maximum of `2,147,483,647` milliseconds (`2,147,483.647` seconds, about 24.86 days). Timeouts and polling intervals must be greater than zero; retry delay may be zero. Invalid values raise `ArgumentOutOfRangeException` before communication or waiting starts.

Cancellation is also strict: after canceling an active operation, call `OpenAsync()` explicitly before reuse, and treat a canceled state-changing request as outcome-unknown when the library reports `ToyopucOperationOutcomeUnknownException`.

For shared wiring, setup, device ranges, and PLC response troubleshooting, use the Computerlink pages on the PLC Comm documentation site.
