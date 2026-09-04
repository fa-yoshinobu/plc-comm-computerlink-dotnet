# Getting started

Use the .NET 8, 9, or 10 SDK when consuming the package. Building or running this repository's user examples requires the .NET 10 SDK because every project under `examples/` targets `net10.0`; the maintainer validation tools are outside that sample policy.

Install the package:

```powershell
dotnet add package PlcComm.Toyopuc
```

Create one validated session by explicitly selecting the endpoint, transport, PLC profile, and route:

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

The factory returns the ordinary `ToyopucDeviceClient`. That client already owns the one required FIFO queue; no queued wrapper is needed. The selected route is retained for the client lifetime, so the same high-level methods work for direct and relay sessions.

TCP and UDP are never inferred. For UDP, configure the PC IP address and a
fixed PC port in the PLC's Other Node Table, then set `LocalPort` to that same
port. This example assumes the PLC peer port is `12000`:

```csharp
var options = new ToyopucConnectionOptions(
    "192.168.250.100",
    1035,
    ToyopucTransportMode.Udp,
    "toyopuc:plus:extended",
    ToyopucRoute.Direct)
{
    LocalPort = 12000,
};
```

Relay sessions require one or more validated hops. Direct is also explicit:

```csharp
var route = ToyopucRoute.Relay("P1-L2:N4,P1-L2:N2");
```

The communication timeout defaults to three seconds for one admitted transaction, including lazy connection, send, receive, and response decoding. `Retries` defaults to zero. A positive retry count applies only while failure is proven to have occurred before any request could have been sent. Once sending may have started, neither reads nor state-changing operations are automatically resent.

Use only addresses and writes that are safe for the connected PLC. Shared setup, device ranges, and troubleshooting are maintained on the PLC Comm documentation site.
