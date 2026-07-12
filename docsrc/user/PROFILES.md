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
