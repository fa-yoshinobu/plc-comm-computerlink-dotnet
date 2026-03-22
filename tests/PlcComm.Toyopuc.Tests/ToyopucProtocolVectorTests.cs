using System.Text.Json;
using PlcComm.Toyopuc;

namespace PlcComm.Toyopuc.Tests;

/// <summary>
/// Cross-language spec compliance: verifies that .NET produces identical binary
/// frames and parses responses identically to Python, as defined in
/// computerlink_frame_vectors.json (shared with Python tests).
/// </summary>
public sealed class ToyopucProtocolVectorTests
{
    private static readonly string VectorsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "vectors", "computerlink_frame_vectors.json");

    private static JsonDocument LoadDoc() => JsonDocument.Parse(File.ReadAllText(VectorsPath));

    public static IEnumerable<object[]> FrameVectors()
    {
        using var doc = LoadDoc();
        foreach (var v in doc.RootElement.GetProperty("frame_vectors").EnumerateArray())
        {
            yield return [v.Clone()];
        }
    }

    public static IEnumerable<object[]> ResponseVectors()
    {
        using var doc = LoadDoc();
        foreach (var v in doc.RootElement.GetProperty("response_vectors").EnumerateArray())
        {
            yield return [v.Clone()];
        }
    }

    public static IEnumerable<object[]> BcdVectors()
    {
        using var doc = LoadDoc();
        foreach (var v in doc.RootElement.GetProperty("bcd_vectors").EnumerateArray())
        {
            yield return [v.Clone()];
        }
    }

    private static byte[] BuildFrame(JsonElement vec)
    {
        var fn = vec.GetProperty("function").GetString()!;
        return fn switch
        {
            "build_clock_read" => ToyopucProtocol.BuildClockRead(),
            "build_cpu_status_read" => ToyopucProtocol.BuildCpuStatusRead(),
            "build_word_read" => ToyopucProtocol.BuildWordRead(
                vec.GetProperty("addr").GetInt32(),
                vec.GetProperty("count").GetInt32()),
            "build_byte_read" => ToyopucProtocol.BuildByteRead(
                vec.GetProperty("addr").GetInt32(),
                vec.GetProperty("count").GetInt32()),
            "build_bit_read" => ToyopucProtocol.BuildBitRead(
                vec.GetProperty("addr").GetInt32()),
            "build_bit_write" => ToyopucProtocol.BuildBitWrite(
                vec.GetProperty("addr").GetInt32(),
                vec.GetProperty("value").GetInt32()),
            _ => throw new InvalidOperationException($"Unknown function: {fn}")
        };
    }

    [Theory]
    [MemberData(nameof(FrameVectors))]
    public void BuildFrame_MatchesVector(JsonElement vec)
    {
        var id = vec.GetProperty("id").GetString()!;
        var expected = Convert.FromHexString(vec.GetProperty("hex").GetString()!);
        var actual = BuildFrame(vec);
        Assert.True(expected.SequenceEqual(actual),
            $"[{id}] expected {vec.GetProperty("hex").GetString()}, got {Convert.ToHexString(actual).ToLowerInvariant()}");
    }

    [Theory]
    [MemberData(nameof(ResponseVectors))]
    public void ParseResponse_MatchesVector(JsonElement vec)
    {
        var id = vec.GetProperty("id").GetString()!;
        var raw = Convert.FromHexString(vec.GetProperty("hex").GetString()!);
        var frame = ToyopucProtocol.ParseResponse(raw);

        Assert.Equal(vec.GetProperty("ft").GetInt32(), (int)frame.Ft);
        Assert.Equal(vec.GetProperty("rc").GetInt32(), (int)frame.Rc);
        Assert.Equal(vec.GetProperty("cmd").GetInt32(), (int)frame.Cmd);

        var expectedData = Convert.FromHexString(vec.GetProperty("data_hex").GetString()!);
        Assert.True(expectedData.SequenceEqual(frame.Data),
            $"[{id}] data mismatch: expected {vec.GetProperty("data_hex").GetString()}, got {Convert.ToHexString(frame.Data).ToLowerInvariant()}");
    }

    [Theory]
    [MemberData(nameof(BcdVectors))]
    public void PackBcd_MatchesVector(JsonElement vec)
    {
        var value = vec.GetProperty("value").GetInt32();
        var expected = vec.GetProperty("bcd_decimal").GetInt32();
        Assert.Equal(expected, ToyopucProtocol.PackBcd(value));
    }
}
