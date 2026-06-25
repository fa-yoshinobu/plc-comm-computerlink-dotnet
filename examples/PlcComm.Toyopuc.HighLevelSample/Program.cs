using System.Globalization;
using PlcComm.Toyopuc;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase)
    || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

var host = args.ElementAtOrDefault(0) ?? "192.168.250.100";
var port = TryParseInt32(args.ElementAtOrDefault(1), 1025);
var transport = TryParseTransport(args.ElementAtOrDefault(2), ToyopucTransportMode.Tcp);
var profileName = args.ElementAtOrDefault(3);
if (string.IsNullOrWhiteSpace(profileName))
{
    Console.Error.WriteLine("profile is required. Specify it explicitly; no PLC profile is inferred from defaults.");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

// Resolve the profile first so unsupported profile names fail before any PLC traffic starts.
var profile = ToyopucAddressingOptions.FromProfile(profileName);

// Create the direct client used by this cookbook sample.
await using var client = new ToyopucDeviceClient(
    host,
    port,
    transport: transport,
    retries: 2,
    retryDelay: TimeSpan.FromMilliseconds(200),
    addressingOptions: profile,
    plcProfile: profileName);

// The selected profile controls which P1-/P2-/P3- and extension addresses are accepted.
await client.OpenAsync();

Console.WriteLine($"connect  : {transport.ToString().ToLowerInvariant()}://{host}:{port}");
Console.WriteLine($"profile  : {profileName}");
Console.WriteLine("scenario : high-level cookbook");
// Normalize an address to show the canonical text used by helper output and diagnostics.
Console.WriteLine($"normalize: {ToyopucAddress.Normalize("p1-d0100", profile: profileName)}");

Console.WriteLine();
Console.WriteLine("1. ReadAsync / WriteAsync");
// Read one word by high-level device string.
var d100 = await client.ReadAsync("P1-D0100");
Console.WriteLine($"P1-D0100 = {FormatValue(d100)}");
// Write only to test addresses you control.
var originalD101 = await client.ReadAsync("P1-D0101");
var originalM0000 = await client.ReadAsync("P1-M0000");
try
{
    await client.WriteAsync("P1-D0101", 1234);
    await client.WriteAsync("P1-M0000", 1);
    Console.WriteLine("Wrote 1234 -> P1-D0101 and 1 -> P1-M0000");
}
finally
{
    await client.WriteAsync("P1-M0000", originalM0000);
    await client.WriteAsync("P1-D0101", originalD101);
}

Console.WriteLine();
Console.WriteLine("2. ReadManyAsync / WriteManyAsync");
// Read several unrelated device strings with one helper call.
var snapshot = await client.ReadManyAsync(["P1-D0100", "P1-D0101", "P1-M0000"]);
Console.WriteLine($"snapshot = [{string.Join(", ", snapshot.Select(FormatValue))}]");
// Write several unrelated device strings as one high-level batch.
try
{
    await client.WriteManyAsync(
    [
        new KeyValuePair<object, object>("P1-D0100", 10),
        new KeyValuePair<object, object>("P1-D0101", 20),
        new KeyValuePair<object, object>("P1-M0000", 0),
    ]);
    Console.WriteLine("Wrote {P1-D0100=10, P1-D0101=20, P1-M0000=0}");
}
finally
{
    await client.WriteManyAsync(
    [
        new KeyValuePair<object, object>("P1-D0100", snapshot[0]),
        new KeyValuePair<object, object>("P1-D0101", snapshot[1]),
        new KeyValuePair<object, object>("P1-M0000", snapshot[2]),
    ]);
}

Console.WriteLine();
Console.WriteLine("3. ReadTypedAsync / WriteTypedAsync");
// Read typed word views; use ":" notation in named reads and dtype strings here.
var typedU = await client.ReadTypedAsync("P1-D0100", "U");
var typedF = await client.ReadTypedAsync("P1-D0300", "F");
var typedL = await client.ReadTypedAsync("P1-D0200", "L");
Console.WriteLine($"P1-D0100:U = {typedU}");
Console.WriteLine($"P1-D0300:F = {typedF}");
// Write typed values to word registers. DWord, long, and float values occupy two words.
try
{
    await client.WriteTypedAsync("P1-D0200", "L", -500);
    await client.WriteTypedAsync("P1-D0300", "F", 3.14f);
    Console.WriteLine("Wrote -500 -> P1-D0200:L and 3.14 -> P1-D0300:F");
}
finally
{
    await client.WriteTypedAsync("P1-D0300", "F", typedF);
    await client.WriteTypedAsync("P1-D0200", "L", typedL);
}

Console.WriteLine();
Console.WriteLine("4. ReadWordsSingleRequestAsync / ReadDWordsSingleRequestAsync / chunked helpers");
// Single-request helpers fail rather than silently changing protocol groups.
ushort[] words = await client.ReadWordsSingleRequestAsync("P1-D0000", 4);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("P1-D0200", 2);
// Chunked helpers split only because the caller supplied an explicit chunk size.
ushort[] chunkedWords = await client.ReadWordsChunkedAsync("P1-D0000", 32, maxWordsPerRequest: 8);
uint[] chunkedDwords = await client.ReadDWordsChunkedAsync("P1-D0200", 8, maxDwordsPerRequest: 4);
Console.WriteLine($"words        = [{string.Join(", ", words)}]");
Console.WriteLine($"dwords       = [{string.Join(", ", dwords)}]");
Console.WriteLine($"chunkedWords = [{string.Join(", ", chunkedWords)}]");
Console.WriteLine($"chunkedDword = [{string.Join(", ", chunkedDwords)}]");

Console.WriteLine();
Console.WriteLine("5. WriteBitInWordAsync / ReadNamedAsync");
// See GOTCHAS.md: dot notation means a bit inside a word, not a typed dword suffix.
var originalBitSnapshot = await client.ReadNamedAsync(["P1-D0100.3"]);
var originalBit = Convert.ToBoolean(originalBitSnapshot["P1-D0100.3"], System.Globalization.CultureInfo.InvariantCulture);
try
{
    await client.WriteBitInWordAsync("P1-D0100", bitIndex: 3, value: true);
    // ReadNamedAsync keeps each requested address string as the result key.
    var named = await client.ReadNamedAsync(
    [
        "P1-D0100",
        "P1-D0200:L",
        "P1-D0300:F",
        "P1-D0100.3",
    ]);
    foreach (var pair in named)
    {
        Console.WriteLine($"{pair.Key,-12} = {FormatValue(pair.Value)}");
    }
}
finally
{
    await client.WriteBitInWordAsync("P1-D0100", bitIndex: 3, value: originalBit);
}

Console.WriteLine();
Console.WriteLine("6. PollAsync");
var pollCount = 0;
// PollAsync repeatedly yields named snapshots until the loop is stopped or cancelled.
await foreach (var pollSnapshot in client.PollAsync(["P1-D0100", "P1-D0300:F", "P1-D0100.3"], TimeSpan.FromMilliseconds(500)))
{
    pollCount++;
    Console.WriteLine($"poll[{pollCount}] = {string.Join(", ", pollSnapshot.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}"))}");
    if (pollCount >= 2)
    {
        break;
    }
}

Console.WriteLine();
Console.WriteLine("7. ReadFrAsync / WriteFrAsync / CommitFrAsync");
// FR reads use dedicated helpers because FR is a flash-backed word area.
var frValue = await client.ReadFrAsync("FR000000");
Console.WriteLine($"FR000000 = {FormatValue(frValue)}");
// See GOTCHAS.md: FR writes are staged until CommitFrAsync flushes them to flash.
// This sample restores the RAM-staged value and intentionally does not commit.
try
{
    await client.WriteFrAsync("FR000000", 0x1234, commit: false);
    Console.WriteLine("Wrote 0x1234 -> FR000000 (RAM only, not committed)");
}
finally
{
    await client.WriteFrAsync("FR000000", frValue, commit: false);
    Console.WriteLine("Restored original FR000000 value (RAM only)");
}
// Uncomment only when the staged FR value is intentionally persistent.
// await client.CommitFrAsync("FR000000", wait: false);

Console.WriteLine();
Console.WriteLine("Done.");

static int TryParseInt32(string? value, int fallback)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return fallback;
    }

    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    return int.Parse(value, CultureInfo.InvariantCulture);
}

static ToyopucTransportMode TryParseTransport(string? value, ToyopucTransportMode fallback)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return fallback;
    }

    return Enum.Parse<ToyopucTransportMode>(value, ignoreCase: true);
}

static string FormatValue(object value)
{
    return value switch
    {
        bool bit => bit ? "1" : "0",
        byte b => $"0x{b:X2}",
        ushort u16 => $"0x{u16:X4}",
        short s16 => s16.ToString(CultureInfo.InvariantCulture),
        uint u32 => $"0x{u32:X8}",
        int s32 => s32.ToString(CultureInfo.InvariantCulture),
        float f32 => f32.ToString("F6", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}

static void PrintUsage()
{
    Console.WriteLine("Toyopuc high-level sample");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.HighLevelSample -- [host] [port] [tcp|udp] <profile>");
    Console.WriteLine();
    Console.WriteLine("Profile is required; the sample does not infer a PLC profile.");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1025 tcp \"toyopuc:plus:extended\"");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1035 udp \"toyopuc:plus:extended\"");
    Console.WriteLine("  dotnet run --project examples\\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1025 tcp \"toyopuc:pc10g:pc10\"");
    Console.WriteLine();
    Console.WriteLine("This sample demonstrates ReadAsync, WriteAsync, ReadManyAsync, WriteManyAsync,");
    Console.WriteLine("ReadTypedAsync, WriteTypedAsync, ReadWordsSingleRequestAsync, ReadDWordsSingleRequestAsync,");
    Console.WriteLine("ReadWordsChunkedAsync, ReadDWordsChunkedAsync, WriteBitInWordAsync,");
    Console.WriteLine("ReadNamedAsync, PollAsync, and FR helpers.");
}
