using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.Toyopuc;

public static class ToyopucRelay
{
    private static readonly Regex PreferredPattern = new(
        @"^P([0-9]+)[-:]L([0-9]+)\s*:\s*N([0-9]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ComponentPattern = new(
        @"^([0-9]+)-([0-9]+):([0-9]+)$",
        RegexOptions.Compiled);

    private static readonly Regex DirectPattern = new(
        @"^([0-9]+):([0-9]+)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<(int LinkNo, int StationNo)> ParseRelayHops(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("at least one hop is required", nameof(text));

        var hops = new List<(int LinkNo, int StationNo)>();
        foreach (var part in text.Split(','))
        {
            var item = part.Trim();
            if (string.IsNullOrEmpty(item))
            {
                throw new ArgumentException("empty relay hop is not allowed", nameof(text));
            }

            var preferred = PreferredPattern.Match(item);
            if (preferred.Success)
            {
                var page = ParseDecimal(preferred.Groups[1].Value, nameof(text));
                var line = ParseDecimal(preferred.Groups[2].Value, nameof(text));
                ValidateComponent(page, nameof(text));
                ValidateComponent(line, nameof(text));
                var station = ParseDecimal(preferred.Groups[3].Value, nameof(text));
                hops.Add(ValidateHop((page << 4) | line, station, nameof(text)));
                continue;
            }

            var component = ComponentPattern.Match(item);
            if (component.Success)
            {
                var page = ParseDecimal(component.Groups[1].Value, nameof(text));
                var line = ParseDecimal(component.Groups[2].Value, nameof(text));
                ValidateComponent(page, nameof(text));
                ValidateComponent(line, nameof(text));
                var station = ParseDecimal(component.Groups[3].Value, nameof(text));
                hops.Add(ValidateHop((page << 4) | line, station, nameof(text)));
                continue;
            }

            var direct = DirectPattern.Match(item);
            if (!direct.Success)
            {
                throw new ArgumentException("each hop must be LINK:STATION or P1-L2:N2", nameof(text));
            }
            hops.Add(ValidateHop(
                ParseDecimal(direct.Groups[1].Value, nameof(text)),
                ParseDecimal(direct.Groups[2].Value, nameof(text)),
                nameof(text)));
        }

        if (hops.Count == 0)
        {
            throw new ArgumentException("at least one hop is required", nameof(text));
        }

        return hops;
    }

    public static IReadOnlyList<(int LinkNo, int StationNo)> NormalizeRelayHops(object hops)
    {
        return hops switch
        {
            string text => ParseRelayHops(text),
            IEnumerable<(int LinkNo, int StationNo)> typed => NormalizeRelayHops(typed),
            _ => throw new ArgumentException("hops must be a relay string or an enumerable of (link, station) tuples", nameof(hops)),
        };
    }

    public static IReadOnlyList<(int LinkNo, int StationNo)> NormalizeRelayHops(IEnumerable<(int LinkNo, int StationNo)> hops)
    {
        (int LinkNo, int StationNo)[] normalized;
        if (hops is ICollection<(int LinkNo, int StationNo)> collection)
        {
            normalized = new (int LinkNo, int StationNo)[collection.Count];
            var index = 0;
            foreach (var hop in hops)
            {
                normalized[index++] = ValidateHop(hop.LinkNo, hop.StationNo, nameof(hops));
            }
        }
        else
        {
            var list = new List<(int LinkNo, int StationNo)>();
            foreach (var hop in hops)
            {
                list.Add(ValidateHop(hop.LinkNo, hop.StationNo, nameof(hops)));
            }

            normalized = list.ToArray();
        }

        if (normalized.Length == 0)
        {
            throw new ArgumentException("at least one hop is required", nameof(hops));
        }

        return normalized;
    }

    public static string FormatRelayHop(int linkNo, int stationNo)
    {
        (linkNo, stationNo) = ValidateHop(linkNo, stationNo, nameof(linkNo));
        return $"P{(linkNo >> 4) & 0x0F}-L{linkNo & 0x0F}:N{stationNo}";
    }

    public static (ResponseFrame Response, byte[] Padding) ParseRelayInnerResponse(byte[] innerRaw)
    {
        if (innerRaw.Length < 3)
        {
            throw new ToyopucProtocolError("Inner relay response too short");
        }

        var innerLength = innerRaw[0] | (innerRaw[1] << 8);
        var expected = 2 + innerLength;
        if (innerRaw.Length < expected)
        {
            throw new ToyopucProtocolError(
                $"Inner relay response truncated: expected {expected} bytes, got {innerRaw.Length} bytes");
        }

        var innerFrame = new byte[2 + expected];
        innerFrame[0] = ToyopucProtocol.FtResponse;
        innerFrame[1] = 0x00;
        Buffer.BlockCopy(innerRaw, 0, innerFrame, 2, expected);
        var padding = new byte[innerRaw.Length - expected];
        Buffer.BlockCopy(innerRaw, expected, padding, 0, padding.Length);
        return (ToyopucProtocol.ParseResponse(innerFrame), padding);
    }

    public static (IReadOnlyList<RelayLayer> Layers, ResponseFrame? FinalResponse) UnwrapRelayResponseChain(ResponseFrame response)
    {
        var layers = new List<RelayLayer>();
        var current = response;

        while (current.Cmd == 0x60)
        {
            if (current.Data.Length < 4)
            {
                throw new ToyopucProtocolError("Relay response data too short");
            }

            var linkNo = current.Data[0];
            var stationNo = current.Data[1] | (current.Data[2] << 8);
            var ack = current.Data[3];
            var innerRaw = new byte[current.Data.Length - 4];
            Buffer.BlockCopy(current.Data, 4, innerRaw, 0, innerRaw.Length);

            if (ack != 0x06)
            {
                layers.Add(new RelayLayer(linkNo, stationNo, ack, innerRaw));
                return (layers, null);
            }

            var (innerResponse, padding) = ParseRelayInnerResponse(innerRaw);
            layers.Add(new RelayLayer(linkNo, stationNo, ack, innerRaw, padding));
            current = innerResponse;
        }

        return (layers, current);
    }

    private static int ParseDecimal(string value, string parameterName)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentOutOfRangeException(parameterName, "relay values must fit in a signed 32-bit decimal integer");
        }
        return parsed;
    }

    private static void ValidateComponent(int value, string parameterName)
    {
        if (value is < 0 or > 15)
            throw new ArgumentOutOfRangeException(parameterName, "relay P and L components must be in the range 0-15");
    }

    private static (int LinkNo, int StationNo) ValidateHop(int linkNo, int stationNo, string parameterName)
    {
        if (linkNo is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "relay link number must be in the range 0-255");
        if (stationNo is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "relay station number must be in the range 1-65535");
        return (linkNo, stationNo);
    }
}
