# PLC profiles

Canonical profile names are part of the public configuration contract. The
library rejects missing, blank, alias, and abbreviated profile strings
immediately. Use `ToyopucPlcProfiles.GetProfileDescriptors()` when a UI needs
canonical names, display labels, connection eligibility, and base-profile
metadata. Store the canonical profile string, not the display name.

Device-family notation, type suffixes, practical range notes, and model-specific
writable range summaries are shared across the Computerlink libraries. Use the
common [Computerlink Device Ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/computerlink/device-ranges/)
page for those details.

Verified hardware available for focused validation is maintained once in the
shared [TOYOPUC Computer Link profile catalog](https://github.com/fa-yoshinobu/plc-comm-computerlink-profiles#verified-hardware-available-for-validation).

## Explicit selection is required

Always pass one exact canonical profile name through
`ToyopucConnectionOptions.PlcProfile` or the `plcProfile` constructor argument.

- No profile is inferred from the PLC model, CPU status, address string, host,
  port, or transport.
- `toyopuc:generic` is not applied automatically when the profile is omitted.
- Old names, short names, aliases, and case variants are rejected.
- Address and data-type inputs may be normalized for convenience, but profile
  names are not.

## Canonical profiles

| Canonical profile | Hardware | Profile-specific cautions |
| --- | --- | --- |
| `toyopuc:generic` | Any TOYOPUC Computerlink | Broad range set; prefer a hardware-specific profile when the model is known. |
| `toyopuc:plus:standard` | TOYOPUC-Plus | U, EB, FR, GM, GX, and GY are not in the standard range. |
| `toyopuc:plus:extended` | TOYOPUC-Plus | Recommended for first examples; U, GM, GX, and GY are available. |
| `toyopuc:nano-10gx:native` | Nano 10GX | Native addressing; relay hops are still explicit. |
| `toyopuc:nano-10gx:compatible` | Nano 10GX | Compatible mode; pass relay hops manually when relaying. |
| `toyopuc:pc10g:standard-pc3jg` | PC10G | FR is not included. |
| `toyopuc:pc10g:pc10` | PC10G | PC10 addressing; FR is available. |
| `toyopuc:pc3jx:pc3-separate` | PC3JX | GM, GX, GY, EB, and FR are not included. |
| `toyopuc:pc3jx:plus-expansion` | PC3JX | B, EB, and FR are not included. |
| `toyopuc:pc3jg:pc3jg` | PC3JG | FR is not included. |
| `toyopuc:pc3jg:pc3-separate` | PC3JG | FR is not included. |

## How to select a profile

```csharp
using PlcComm.Toyopuc;

var profile = ToyopucPlcProfiles.FromName("toyopuc:plus:extended");
Console.WriteLine(profile.Name);
```

## Connection snippet

```csharp
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions(
    "192.168.250.100",
    1025,
    ToyopucTransportMode.Tcp,
    "toyopuc:plus:extended",
    ToyopucRoute.Direct);

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("P1-D0000", "U");
Console.WriteLine(value);
```

## Selection table

| If your PLC is | Start with |
| --- | --- |
| TOYOPUC-Plus with U or GM/GX/GY access | `toyopuc:plus:extended` |
| TOYOPUC-Plus without extended ranges | `toyopuc:plus:standard` |
| Nano 10GX native configuration | `toyopuc:nano-10gx:native` |
| Nano 10GX compatible configuration | `toyopuc:nano-10gx:compatible` |
| PC10G PC10 addressing | `toyopuc:pc10g:pc10` |
| PC10G PC3JG-compatible standard addressing | `toyopuc:pc10g:standard-pc3jg` |
| PC3JX PC3 separate addressing | `toyopuc:pc3jx:pc3-separate` |
| PC3JX plus expansion addressing | `toyopuc:pc3jx:plus-expansion` |
| PC3JG mode | `toyopuc:pc3jg:pc3jg` |
| Unknown hardware during exploration | `toyopuc:generic` |
