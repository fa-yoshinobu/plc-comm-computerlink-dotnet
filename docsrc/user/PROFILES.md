# PLC profiles

A canonical PLC profile is required for every high-level client and standalone semantic address operation. The profile determines device syntax and route selection; manual addressing overrides are not part of the normal runtime API.

```csharp
var options = new ToyopucConnectionOptions(
    "192.168.250.100",
    1025,
    ToyopucTransportMode.Tcp,
    "toyopuc:pc10g:pc10",
    ToyopucRoute.Direct);
```

Use `ToyopucPlcProfiles.All` to enumerate canonical profile descriptors and `ToyopucPlcProfiles.FromName(name)` to validate a configured name before communication.

The maintained profile matrix and shared device-range tables are published on the PLC Comm documentation site. A failing address does not by itself prove that an entire device family or route is unsupported; distinguish profile rules, configured hardware, route selection, address validity, and request limits.

## Verified hardware available for validation

The maintainer owns the hardware listed below. Communication has been verified
on this hardware, and it is available for focused reproduction and validation
when a problem is reported.

"Verified" does not mean that every library feature has been tested on every
listed PLC or module. Exhaustively testing every combination would require a prohibitive
amount of work.

| PLC family or unit group | Hardware owned by the maintainer |
| --- | --- |
| TOYOPUC Nano | `Nano 10GX`, `Nano 2ET` |
| TOYOPUC Plus | `Plus CPU`, `Plus EX2` |
| TOYOPUC PC10G | `PC10G-1SP`, `PC10G`, `EF10`, `2PORT-EFR` |
| TOYOPUC PC3J | `PC3JX-D`, `PC3JG` |
