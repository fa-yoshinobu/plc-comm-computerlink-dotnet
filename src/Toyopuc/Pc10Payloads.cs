namespace PlcComm.Toyopuc;

internal static class Pc10Payloads
{
    private const int Pc10MultiReadMaxPoints = 0x7F;
    private const int Pc10MultiWriteMaxPayloadBytes = 0x0200;

    private static void RequireMultiReadCount(int count)
    {
        if (count < 1 || count > Pc10MultiReadMaxPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"CMD=C4 PC10 multi-read point count must be 1..0x{Pc10MultiReadMaxPoints:X} ({Pc10MultiReadMaxPoints})");
        }
    }

    private static void RequireMultiWritePayload(byte[] payload)
    {
        if (payload.Length < 1 || payload.Length > Pc10MultiWriteMaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                $"CMD=C5 PC10 multi-write payload must be 1..0x{Pc10MultiWriteMaxPayloadBytes:X} ({Pc10MultiWriteMaxPayloadBytes}) bytes");
        }
    }

    public static byte[] PackWordValues(IEnumerable<int> values)
    {
        var items = values as int[] ?? values.ToArray();
        var data = new byte[items.Length * 2];
        for (var i = 0; i < items.Length; i++)
        {
            RequireWordValue(items[i]);
            WriteU16LittleEndian(data, i * 2, items[i]);
        }

        return data;
    }

    public static byte[] BuildMultiWordReadPayload(IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        RequireMultiReadCount(items.Length);
        var payload = new byte[4 + (items.Length * 4)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i]);
        }

        return payload;
    }

    public static byte[] PackMultiWordPayload(IEnumerable<(int Address32, int Value)> addressValues)
    {
        var items = addressValues.ToArray();
        var payload = new byte[4 + (items.Length * 4) + (items.Length * 2)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i].Address32);
        }

        var valuesOffset = 4 + (items.Length * 4);
        for (var i = 0; i < items.Length; i++)
        {
            RequireWordValue(items[i].Value);
            WriteU16LittleEndian(payload, valuesOffset + (i * 2), items[i].Value);
        }

        RequireMultiWritePayload(payload);
        return payload;
    }

    public static byte[] PackMultiBitPayload(IEnumerable<(int Address32, int Value)> addressValues)
    {
        var items = addressValues.ToArray();
        var bitBytesOffset = 4 + (items.Length * 4);
        var payload = new byte[bitBytesOffset + ((items.Length + 7) / 8)];
        payload[0] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].Value is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(addressValues), "Bit values must be 0 or 1.");
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i].Address32);
            if (items[i].Value == 1)
            {
                payload[bitBytesOffset + (i / 8)] |= (byte)(1 << (i % 8));
            }
        }

        RequireMultiWritePayload(payload);
        return payload;
    }

    public static byte[] BuildMultiBitReadPayload(IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        RequireMultiReadCount(items.Length);
        var payload = new byte[4 + (items.Length * 4)];
        payload[0] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i]);
        }

        return payload;
    }

    private static void WriteU16LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void RequireWordValue(int value)
    {
        if (value is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Word values must be in the range 0..65535.");
    }

    private static void WriteAddress32LittleEndian(byte[] buffer, int offset, int address32)
    {
        WriteU16LittleEndian(buffer, offset, address32 & 0xFFFF);
        WriteU16LittleEndian(buffer, offset + 2, (address32 >> 16) & 0xFFFF);
    }
}
