# Getting started

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

TCP and UDP are never inferred. For UDP, `LocalPort = 0` asks the operating system to allocate an ephemeral local port:

```csharp
var options = new ToyopucConnectionOptions(
    "192.168.250.100",
    1035,
    ToyopucTransportMode.Udp,
    "toyopuc:plus:extended",
    ToyopucRoute.Direct)
{
    LocalPort = 0,
};
```

Relay sessions require one or more validated hops. Direct is also explicit:

```csharp
var route = ToyopucRoute.Relay("P1-L2:N4,P1-L2:N2");
```

The communication timeout defaults to three seconds per attempt. `Retries` defaults to zero. Reads may be retried only when a positive retry count is explicitly configured; state-changing operations are not automatically resent after an uncertain result.

Use only addresses and writes that are safe for the connected PLC. Shared setup, device ranges, and troubleshooting are maintained on the PLC Comm documentation site.
