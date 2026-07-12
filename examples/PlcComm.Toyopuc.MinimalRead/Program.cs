using System.Globalization;
using PlcComm.Toyopuc;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase)
    || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

if (args.Length < 5)
{
    Console.Error.WriteLine("host, port, transport, and device are required; profile is required explicitly.");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

var host = args[0];
var port = ParseInt32(args[1]);
var transport = Enum.Parse<ToyopucTransportMode>(args[2], ignoreCase: true);
var device = args[3].ToUpperInvariant();
var profileName = args[4];
if (string.IsNullOrWhiteSpace(profileName))
{
    Console.Error.WriteLine("profile is required. Specify it explicitly; no PLC profile is inferred from defaults.");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

// Resolve the profile first so invalid model strings fail before any PLC traffic starts.
_ = ToyopucPlcProfiles.FromName(profileName);

// Create the direct client for this minimal sample.
using var plc = new ToyopucDeviceClient(
    host,
    port,
    transport,
    profileName);

// The selected profile controls address validation for the read below.
// The transport connection opens lazily on the first request.
var status = plc.ReadCpuStatus();
// Read the PLC clock after the status read proves the session is responding.
var clock = plc.ReadClock().AsDateTime(2000);
// Read one high-level device string; start with P1-D0000 before trying typed views.
var value = plc.ReadOne(device);

Console.WriteLine($"connect    : {transport.ToString().ToLowerInvariant()}://{host}:{port}");
Console.WriteLine($"profile    : {profileName}");
Console.WriteLine("scenario   : minimal high-level read");
Console.WriteLine($"cpu-status : {status.RawBytesHex}");
Console.WriteLine($"clock      : {clock:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"{device,-10}: {FormatValue(value)}");

static string FormatValue(object value)
{
    return value switch
    {
        bool bit => bit ? "1" : "0",
        byte b => $"0x{b:X2}",
        int word => $"0x{word:X4}",
        _ => value.ToString() ?? string.Empty,
    };
}

static int ParseInt32(string value)
{
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    return int.Parse(value, CultureInfo.InvariantCulture);
}

static void PrintUsage()
{
    Console.WriteLine("Toyopuc minimal read example");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.MinimalRead -- <host> <port> <tcp|udp> <device> <profile>");
    Console.WriteLine();
    Console.WriteLine("Endpoint, transport, device, and profile are required; the sample does not infer them.");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1025 tcp P1-D0000 \"toyopuc:plus:extended\"");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1035 udp P1-D0000 \"toyopuc:plus:extended\"");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1025 tcp P1-M0000 \"toyopuc:pc10g:pc10\"");
    Console.WriteLine();
    Console.WriteLine("This sample reads CPU status, PLC clock, and one high-level device address.");
}
