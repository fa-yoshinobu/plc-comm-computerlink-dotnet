# Troubleshooting

Use this page for first-pass checks when your PLC does not respond as expected. For address-shape mistakes and helper-specific surprises, see [GOTCHAS.md](GOTCHAS.md).

## Connection checks

| Symptom | Check |
| --- | --- |
| Connection timeout | Confirm the PLC host is `192.168.250.100` or the address you configured. |
| TCP connection refused | Confirm Computerlink is enabled on your PLC and TCP port `1025` is open. |
| UDP requests do not return | Confirm you are using the PLC UDP port configured for your target; examples use `1035` for UDP. |
| Intermittent timeouts | Set `Timeout`, `Retries`, and `RetryDelay` on `ToyopucConnectionOptions`. |

## Addressing checks

| Symptom | Check |
| --- | --- |
| Unknown device area | Confirm the selected `PlcProfile` supports that family. |
| Address out of range | Compare the profile in [PROFILES.md](PROFILES.md) with the family table in [SUPPORTED_REGISTERS.md](SUPPORTED_REGISTERS.md). |
| Basic address rejected | Use `P1-`, `P2-`, or `P3-` for basic families such as `D`, `M`, `X`, `Y`, `T`, `C`, `L`, `N`, `R`, and `S`. |
| Dword read returns a bit | Use `:D` for dword access and `.D` only for bit 13 inside a word. |

## Write checks

| Symptom | Check |
| --- | --- |
| A write appears to work but changes the wrong value | Stop and confirm you are using a test address you control. |
| FR value does not survive power cycle | Stage with `WriteFrAsync` and then commit with `CommitFrAsync`. |
| Relay write or read does not reach the target PLC | Set `RelayHops` explicitly in `ToyopucConnectionOptions`. |
