using System.Buffers.Binary;

namespace PlcComm.Toyopuc;

public static class ToyopucProtocol
{
    public const byte FtCommand = 0x00;
    public const byte FtResponse = 0x80;

    private const int ContinuousWordMax = 0x0200;
    private const int ContinuousByteMax = 0x0400;
    private const int BasicMultiMaxPoints = 0x0080;
    private const int ExtMultiMaxPoints = 0x00B0;
    private const int ExtMultiMaxDataBytes = 0x0080;
    private const int Pc10BlockMaxBytes = 0x03F0;
    private const int Pc10MultiMaxPayloadBytes = 0x0200;

    private static int RequireCount(string label, int count, int maxCount)
    {
        if (count < 1 || count > maxCount)
        {
            throw new ArgumentOutOfRangeException(label, $"{label} count must be 1..0x{maxCount:X} ({maxCount})");
        }

        return count;
    }

    private static void RequireLength(string label, int length, int maxLength)
    {
        if (length < 1 || length > maxLength)
        {
            throw new ArgumentOutOfRangeException(label, $"{label} length must be 1..0x{maxLength:X} ({maxLength}) bytes");
        }
    }

    private static byte RequireWireByte(string label, int value)
    {
        if (value is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(label, $"{label} must be in the range 0..255.");
        }

        return (byte)value;
    }

    private static void RequirePc10BlockRange(int address32, int byteCount)
    {
        var offset = address32 & 0xFFFF;
        if (offset + byteCount > 0x10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteCount),
                "PC10 block access must not cross the 16-bit block boundary");
        }
    }

    private static void RequireExtMultiReadLimits(int bitCount, int byteCount, int wordCount)
    {
        var total = bitCount + byteCount + wordCount;
        RequireCount("CMD=98 point", total, ExtMultiMaxPoints);
        var dataBytes = ((bitCount + 7) / 8) + byteCount + (wordCount * 2);
        if (dataBytes > ExtMultiMaxDataBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                $"CMD=98 response data would exceed 0x{ExtMultiMaxDataBytes:X} ({ExtMultiMaxDataBytes}) bytes");
        }
    }

    private static void RequireExtMultiWriteLimits(int bitCount, int byteCount, int wordCount)
    {
        var total = bitCount + byteCount + wordCount;
        RequireCount("CMD=99 point", total, ExtMultiMaxPoints);
        var dataBytes = bitCount + byteCount + (wordCount * 2);
        if (dataBytes > ExtMultiMaxDataBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                $"CMD=99 write data would exceed 0x{ExtMultiMaxDataBytes:X} ({ExtMultiMaxDataBytes}) bytes");
        }
    }

    private static byte[] CreateCommandFrame(int cmd, int dataLength)
    {
        if (cmd is < byte.MinValue or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Command code must be in the range 0-255.");
        if (dataLength is < 0 or > 65534)
            throw new ArgumentOutOfRangeException(nameof(dataLength), "Command data must fit the 16-bit frame length field.");
        var frame = new byte[5 + dataLength];
        var length = 1 + dataLength;
        frame[0] = FtCommand;
        frame[1] = 0x00;
        frame[2] = (byte)(length & 0xFF);
        frame[3] = (byte)((length >> 8) & 0xFF);
        frame[4] = (byte)cmd;
        return frame;
    }

    private static void WriteU16(byte[] buffer, int offset, int value)
    {
        if (value is < ushort.MinValue or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Unsigned 16-bit value must be in the range 0..65535.");
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), (ushort)value);
    }

    private static byte[] MaterializeByteValues(IEnumerable<int> values)
    {
        if (values is ICollection<int> collection)
        {
            var bytes = new byte[collection.Count];
            var index = 0;
            foreach (var value in values)
            {
                if (value is < byte.MinValue or > byte.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(values), "Byte values must be in the range 0..255.");
                bytes[index++] = (byte)value;
            }

            return bytes;
        }

        var list = new List<byte>();
        foreach (var value in values)
        {
            if (value is < byte.MinValue or > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(values), "Byte values must be in the range 0..255.");
            list.Add((byte)value);
        }

        return list.ToArray();
    }

    internal static byte[] BuildCommand(int cmd, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var frame = CreateCommandFrame(cmd, data.Length);
        if (data.Length > 0)
        {
            Buffer.BlockCopy(data, 0, frame, 5, data.Length);
        }

        return frame;
    }

    public static ResponseFrame ParseResponse(byte[] frame)
        => ParseResponseView(frame).ToOwned();

    internal static ResponseFrameView ParseResponseView(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Length < 5)
        {
            throw new ToyopucProtocolError("Response too short");
        }

        var ft = frame[0];
        var rc = frame[1];
        var length = frame[2] | (frame[3] << 8);
        var expected = 4 + length;
        if (frame.Length != expected)
        {
            throw new ToyopucProtocolError($"Invalid length: expected {expected} bytes, got {frame.Length} bytes");
        }

        return new ResponseFrameView(ft, rc, frame[4], frame.AsMemory(5));
    }

    public static byte[] PackU16LittleEndian(int value)
    {
        var buffer = new byte[2];
        WriteU16(buffer, 0, value);
        return buffer;
    }

    public static int[] UnpackU16LittleEndian(byte[] data)
        => UnpackU16LittleEndian(data.AsSpan());

    internal static int[] UnpackU16LittleEndian(ReadOnlySpan<byte> data)
    {
        if (data.Length % 2 != 0)
        {
            throw new ToyopucProtocolError("Word data length must be even");
        }

        var values = new int[data.Length / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * 2, 2));
        }

        return values;
    }

    public static int PackExtBitSpec(int number, int bit)
    {
        if (number is < 0 or > 0x0F)
        {
            throw new ToyopucProtocolError("Extended bit No must fit in 4 bits");
        }

        if (bit is < 0 or > 0x0F)
        {
            throw new ToyopucProtocolError("Extended bit position must fit in 4 bits");
        }

        return ((bit & 0x0F) << 4) | (number & 0x0F);
    }

    public static int PackBcd(int value)
    {
        if (value is < 0 or > 99)
        {
            throw new ToyopucProtocolError("BCD value out of range");
        }

        return ((value / 10) << 4) | (value % 10);
    }

    public static int UnpackBcd(int value)
    {
        var high = (value >> 4) & 0x0F;
        var low = value & 0x0F;
        if (high > 9 || low > 9)
        {
            throw new ToyopucProtocolError($"Invalid BCD byte: 0x{value:X2}");
        }

        return (high * 10) + low;
    }

    public static byte[] BuildClockRead()
    {
        var frame = CreateCommandFrame(0x32, 2);
        frame[5] = 0x70;
        frame[6] = 0x00;
        return frame;
    }

    public static byte[] BuildCpuStatusRead()
    {
        var frame = CreateCommandFrame(0x32, 2);
        frame[5] = 0x11;
        frame[6] = 0x00;
        return frame;
    }

    public static byte[] BuildCpuStatusReadA0()
    {
        var frame = CreateCommandFrame(0xA0, 3);
        frame[5] = 0x00;
        frame[6] = 0x11;
        frame[7] = 0x00;
        return frame;
    }

    internal static byte[] BuildProgramTimerCounterRead(int programNumber, int address)
        => BuildProgramTimerCounterCommand(programNumber, 0x40, address, Array.Empty<int>());

    internal static byte[] BuildProgramTimerCounterWriteBoth(int programNumber, int address, int preset, int current)
        => BuildProgramTimerCounterCommand(programNumber, 0x41, address, [preset, current]);

    internal static byte[] BuildProgramTimerCounterWritePreset(int programNumber, int address, int preset)
        => BuildProgramTimerCounterCommand(programNumber, 0x42, address, [preset]);

    internal static byte[] BuildProgramTimerCounterWriteCurrent(int programNumber, int address, int current)
        => BuildProgramTimerCounterCommand(programNumber, 0x43, address, [current]);

    private static byte[] BuildProgramTimerCounterCommand(
        int programNumber,
        int selector,
        int address,
        IReadOnlyList<int> values)
    {
        if (programNumber is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(programNumber), "programNumber must be 1-3.");
        if (address is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-65535.");
        var data = new byte[5 + (values.Count * 2)];
        data[0] = (byte)programNumber;
        data[1] = (byte)selector;
        data[2] = 0x00;
        WriteU16(data, 3, address);
        for (var index = 0; index < values.Count; index++)
        {
            WriteU16(data, 5 + (index * 2), values[index]);
        }
        return BuildCommand(0xA0, data);
    }

    public static byte[] BuildScanResume()
    {
        var frame = CreateCommandFrame(0x32, 2);
        frame[5] = 0x01;
        frame[6] = 0x00;
        return frame;
    }

    public static byte[] BuildScanStop()
    {
        var frame = CreateCommandFrame(0x32, 3);
        frame[5] = 0x02;
        frame[6] = 0x00;
        frame[7] = 0x01;
        return frame;
    }

    public static byte[] BuildScanStopRelease()
    {
        var frame = CreateCommandFrame(0x32, 3);
        frame[5] = 0x02;
        frame[6] = 0x00;
        frame[7] = 0x00;
        return frame;
    }

    public static byte[] BuildClockWrite(
        int second,
        int minute,
        int hour,
        int day,
        int month,
        int year2Digit,
        int weekday)
    {
        if (weekday is < 0 or > 6)
        {
            throw new ToyopucProtocolError("Weekday must be in range 0-6");
        }

        var frame = CreateCommandFrame(0x32, 9);
        frame[5] = 0x71;
        frame[6] = 0x00;
        frame[7] = (byte)PackBcd(second);
        frame[8] = (byte)PackBcd(minute);
        frame[9] = (byte)PackBcd(hour);
        frame[10] = (byte)PackBcd(day);
        frame[11] = (byte)PackBcd(month);
        frame[12] = (byte)PackBcd(year2Digit);
        frame[13] = (byte)PackBcd(weekday);
        return frame;
    }

    public static ClockData ParseClockData(byte[] data)
        => ParseClockData(data.AsSpan());

    internal static ClockData ParseClockData(ReadOnlySpan<byte> data)
    {
        if (data.Length != 9 || data[0] != 0x70 || data[1] != 0x00)
        {
            throw new ToyopucProtocolError("Clock read response must be 9 bytes starting with 70 00");
        }

        return new ClockData(
            UnpackBcd(data[2]),
            UnpackBcd(data[3]),
            UnpackBcd(data[4]),
            UnpackBcd(data[5]),
            UnpackBcd(data[6]),
            UnpackBcd(data[7]),
            UnpackBcd(data[8]));
    }

    public static CpuStatusData ParseCpuStatusData(byte[] data)
        => ParseCpuStatusData(data.AsSpan());

    internal static CpuStatusData ParseCpuStatusData(ReadOnlySpan<byte> data)
    {
        if (data.Length != 10 || data[0] != 0x11 || data[1] != 0x00)
        {
            throw new ToyopucProtocolError("CPU status response must be 10 bytes starting with 11 00");
        }

        return new CpuStatusData(data[2], data[3], data[4], data[5], data[6], data[7], data[8], data[9]);
    }

    public static CpuStatusData ParseCpuStatusDataA0(byte[] data)
        => ParseCpuStatusDataA0(data.AsSpan());

    internal static CpuStatusData ParseCpuStatusDataA0(ReadOnlySpan<byte> data)
    {
        if (data.Length != 11 || data[0] != 0x00 || data[1] != 0x11 || data[2] != 0x00)
        {
            throw new ToyopucProtocolError("A0 CPU status response must be 11 bytes starting with 00 11 00");
        }

        return new CpuStatusData(data[3], data[4], data[5], data[6], data[7], data[8], data[9], data[10]);
    }

    internal static TimerCounterValues ParseProgramTimerCounterValues(ReadOnlySpan<byte> data, int programNumber)
    {
        if (data.Length != 7
            || data[0] != programNumber
            || data[1] != 0x40
            || data[2] != 0x00)
        {
            throw new ToyopucProtocolError(
                $"A0 timer/counter response must be 7 bytes starting with {programNumber:X2} 40 00");
        }

        return new TimerCounterValues(
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(3, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(5, 2)));
    }

    internal static void ValidateProgramTimerCounterWriteResponse(
        ReadOnlySpan<byte> data,
        int programNumber,
        int selector)
    {
        if (data.Length != 3
            || data[0] != programNumber
            || data[1] != selector
            || data[2] != 0x00)
        {
            throw new ToyopucProtocolError(
                $"A0 timer/counter write response must be 3 bytes starting with {programNumber:X2} {selector:X2} 00");
        }
    }

    public static byte[] ParseCpuStatusDataA0Raw(byte[] data)
        => ParseCpuStatusDataA0Raw(data.AsSpan());

    internal static byte[] ParseCpuStatusDataA0Raw(ReadOnlySpan<byte> data)
        => ParseCpuStatusDataA0(data).RawBytes;

    public static byte[] BuildWordRead(int address, int count)
    {
        count = RequireCount("CMD=1C word-read", count, ContinuousWordMax);
        var frame = CreateCommandFrame(0x1C, 4);
        WriteU16(frame, 5, address);
        WriteU16(frame, 7, count);
        return frame;
    }

    public static byte[] BuildWordWrite(int address, IEnumerable<int> values)
    {
        var words = values as int[] ?? values.ToArray();
        RequireCount("CMD=1D word-write", words.Length, ContinuousWordMax);
        var frame = CreateCommandFrame(0x1D, 2 + (words.Length * 2));
        WriteU16(frame, 5, address);
        for (var i = 0; i < words.Length; i++)
        {
            WriteU16(frame, 7 + (i * 2), words[i]);
        }

        return frame;
    }

    public static byte[] BuildByteRead(int address, int count)
    {
        count = RequireCount("CMD=1E byte-read", count, ContinuousByteMax);
        var frame = CreateCommandFrame(0x1E, 4);
        WriteU16(frame, 5, address);
        WriteU16(frame, 7, count);
        return frame;
    }

    public static byte[] BuildByteWrite(int address, IEnumerable<int> values)
    {
        var bytes = MaterializeByteValues(values);
        RequireCount("CMD=1F byte-write", bytes.Length, ContinuousByteMax);
        var frame = CreateCommandFrame(0x1F, 2 + bytes.Length);
        WriteU16(frame, 5, address);
        if (bytes.Length > 0)
        {
            Buffer.BlockCopy(bytes, 0, frame, 7, bytes.Length);
        }

        return frame;
    }

    public static byte[] BuildBitRead(int address)
    {
        var frame = CreateCommandFrame(0x20, 2);
        WriteU16(frame, 5, address);
        return frame;
    }

    public static byte[] BuildBitWrite(int address, int value)
    {
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Bit value must be 0 or 1.");
        var frame = CreateCommandFrame(0x21, 3);
        WriteU16(frame, 5, address);
        frame[7] = (byte)value;
        return frame;
    }

    public static byte[] BuildMultiWordRead(IEnumerable<int> addresses)
    {
        var items = addresses as int[] ?? addresses.ToArray();
        RequireCount("CMD=22 multi-word-read", items.Length, BasicMultiMaxPoints);
        var frame = CreateCommandFrame(0x22, items.Length * 2);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 2), items[i]);
        }

        return frame;
    }

    public static byte[] BuildMultiWordWrite(IEnumerable<(int Address, int Value)> pairs)
    {
        var items = pairs as (int Address, int Value)[] ?? pairs.ToArray();
        RequireCount("CMD=23 multi-word-write", items.Length, BasicMultiMaxPoints);
        var frame = CreateCommandFrame(0x23, items.Length * 4);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 4), items[i].Address);
            WriteU16(frame, 7 + (i * 4), items[i].Value);
        }

        return frame;
    }

    public static byte[] BuildMultiByteRead(IEnumerable<int> addresses)
    {
        var items = addresses as int[] ?? addresses.ToArray();
        RequireCount("CMD=24 multi-byte-read", items.Length, BasicMultiMaxPoints);
        var frame = CreateCommandFrame(0x24, items.Length * 2);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 2), items[i]);
        }

        return frame;
    }

    public static byte[] BuildMultiByteWrite(IEnumerable<(int Address, int Value)> pairs)
    {
        var items = pairs as (int Address, int Value)[] ?? pairs.ToArray();
        RequireCount("CMD=25 multi-byte-write", items.Length, BasicMultiMaxPoints);
        var frame = CreateCommandFrame(0x25, items.Length * 3);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 3), items[i].Address);
            if (items[i].Value is < byte.MinValue or > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(pairs), "Byte values must be in the range 0..255.");
            frame[7 + (i * 3)] = (byte)items[i].Value;
        }

        return frame;
    }

    public static byte[] BuildExtWordRead(int number, int address, int count)
    {
        count = RequireCount("CMD=94 ext-word-read", count, ContinuousWordMax);
        var frame = CreateCommandFrame(0x94, 5);
        frame[5] = RequireWireByte(nameof(number), number);
        WriteU16(frame, 6, address);
        WriteU16(frame, 8, count);
        return frame;
    }

    public static byte[] BuildExtWordWrite(int number, int address, IEnumerable<int> values)
    {
        var words = values as int[] ?? values.ToArray();
        RequireCount("CMD=95 ext-word-write", words.Length, ContinuousWordMax);
        var frame = CreateCommandFrame(0x95, 3 + (words.Length * 2));
        frame[5] = RequireWireByte(nameof(number), number);
        WriteU16(frame, 6, address);
        for (var i = 0; i < words.Length; i++)
        {
            WriteU16(frame, 8 + (i * 2), words[i]);
        }

        return frame;
    }

    public static byte[] BuildExtByteRead(int number, int address, int count)
    {
        count = RequireCount("CMD=96 ext-byte-read", count, ContinuousByteMax);
        var frame = CreateCommandFrame(0x96, 5);
        frame[5] = RequireWireByte(nameof(number), number);
        WriteU16(frame, 6, address);
        WriteU16(frame, 8, count);
        return frame;
    }

    public static byte[] BuildExtByteWrite(int number, int address, IEnumerable<int> values)
    {
        var bytes = MaterializeByteValues(values);
        RequireCount("CMD=97 ext-byte-write", bytes.Length, ContinuousByteMax);
        var frame = CreateCommandFrame(0x97, 3 + bytes.Length);
        frame[5] = RequireWireByte(nameof(number), number);
        WriteU16(frame, 6, address);
        if (bytes.Length > 0)
        {
            Buffer.BlockCopy(bytes, 0, frame, 8, bytes.Length);
        }

        return frame;
    }

    public static byte[] BuildExtMultiRead(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints)
    {
        var bits = bitPoints as (int No, int Bit, int Address)[] ?? bitPoints.ToArray();
        var bytes = bytePoints as (int No, int Address)[] ?? bytePoints.ToArray();
        var words = wordPoints as (int No, int Address)[] ?? wordPoints.ToArray();
        RequireExtMultiReadLimits(bits.Length, bytes.Length, words.Length);
        var frame = CreateCommandFrame(0x98, 3 + (bits.Length * 3) + (bytes.Length * 3) + (words.Length * 3));
        frame[5] = (byte)(bits.Length & 0xFF);
        frame[6] = (byte)(bytes.Length & 0xFF);
        frame[7] = (byte)(words.Length & 0xFF);
        var offset = 8;

        foreach (var (number, bit, address) in bits)
        {
            frame[offset++] = (byte)PackExtBitSpec(number, bit);
            WriteU16(frame, offset, address);
            offset += 2;
        }

        foreach (var (number, address) in bytes)
        {
            frame[offset++] = RequireWireByte(nameof(bytePoints), number);
            WriteU16(frame, offset, address);
            offset += 2;
        }

        foreach (var (number, address) in words)
        {
            frame[offset++] = RequireWireByte(nameof(wordPoints), number);
            WriteU16(frame, offset, address);
            offset += 2;
        }

        return frame;
    }

    public static byte[] BuildExtMultiWrite(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints)
    {
        var bits = bitPoints as (int No, int Bit, int Address, int Value)[] ?? bitPoints.ToArray();
        var bytes = bytePoints as (int No, int Address, int Value)[] ?? bytePoints.ToArray();
        var words = wordPoints as (int No, int Address, int Value)[] ?? wordPoints.ToArray();
        RequireExtMultiWriteLimits(bits.Length, bytes.Length, words.Length);
        var frame = CreateCommandFrame(0x99, 3 + (bits.Length * 4) + (bytes.Length * 4) + (words.Length * 5));
        frame[5] = (byte)(bits.Length & 0xFF);
        frame[6] = (byte)(bytes.Length & 0xFF);
        frame[7] = (byte)(words.Length & 0xFF);
        var offset = 8;

        foreach (var (number, bit, address, value) in bits)
        {
            if (value is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(bitPoints), "Bit values must be 0 or 1.");
            frame[offset++] = (byte)PackExtBitSpec(number, bit);
            WriteU16(frame, offset, address);
            offset += 2;
            frame[offset++] = (byte)value;
        }

        foreach (var (number, address, value) in bytes)
        {
            if (value is < byte.MinValue or > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(bytePoints), "Byte values must be in the range 0..255.");
            frame[offset++] = RequireWireByte(nameof(bytePoints), number);
            WriteU16(frame, offset, address);
            offset += 2;
            frame[offset++] = (byte)value;
        }

        foreach (var (number, address, value) in words)
        {
            frame[offset++] = RequireWireByte(nameof(wordPoints), number);
            WriteU16(frame, offset, address);
            offset += 2;
            WriteU16(frame, offset, value);
            offset += 2;
        }

        return frame;
    }

    public static byte[] BuildPc10BlockRead(int address32, int count)
    {
        count = RequireCount("CMD=C2 PC10 block-read", count, Pc10BlockMaxBytes);
        RequirePc10BlockRange(address32, count);
        var frame = CreateCommandFrame(0xC2, 6);
        WriteU16(frame, 5, address32 & 0xFFFF);
        WriteU16(frame, 7, (address32 >> 16) & 0xFFFF);
        WriteU16(frame, 9, count);
        return frame;
    }

    public static byte[] BuildPc10BlockWrite(int address32, byte[] dataBytes)
    {
        RequireLength("CMD=C3 PC10 block-write", dataBytes.Length, Pc10BlockMaxBytes);
        RequirePc10BlockRange(address32, dataBytes.Length);
        var frame = CreateCommandFrame(0xC3, 4 + dataBytes.Length);
        WriteU16(frame, 5, address32 & 0xFFFF);
        WriteU16(frame, 7, (address32 >> 16) & 0xFFFF);
        if (dataBytes.Length > 0)
        {
            Buffer.BlockCopy(dataBytes, 0, frame, 9, dataBytes.Length);
        }

        return frame;
    }

    public static byte[] BuildPc10MultiRead(byte[] payload)
    {
        RequireLength("CMD=C4 PC10 multi-read payload", payload.Length, Pc10MultiMaxPayloadBytes);
        return BuildCommand(0xC4, payload);
    }

    public static byte[] BuildPc10MultiWrite(byte[] payload)
    {
        RequireLength("CMD=C5 PC10 multi-write payload", payload.Length, Pc10MultiMaxPayloadBytes);
        return BuildCommand(0xC5, payload);
    }

    public static byte[] BuildFrRegister(int exNo)
    {
        var frame = CreateCommandFrame(0xCA, 1);
        frame[5] = RequireWireByte(nameof(exNo), exNo);
        return frame;
    }

    public static byte[] BuildRelayCommand(int linkNo, int stationNo, byte[] innerPayload)
    {
        var hop = ToyopucRelay.NormalizeRelayHops([(linkNo, stationNo)])[0];
        return BuildRelayCommand(hop.LinkNo, hop.StationNo, ParseRelayInnerRequest(innerPayload));
    }

    internal static byte[] BuildRelayCommand(int linkNo, int stationNo, RelayInnerRequest request)
    {
        var hop = ToyopucRelay.NormalizeRelayHops([(linkNo, stationNo)])[0];
        return BuildRelayCommandFromTrimmed(hop.LinkNo, hop.StationNo, request.TrimmedPayload);
    }

    private static byte[] BuildRelayCommandFromTrimmed(int linkNo, int stationNo, byte[] trimmedPayload)
    {
        var frame = CreateCommandFrame(0x60, 5 + trimmedPayload.Length);
        frame[5] = (byte)linkNo;
        frame[6] = (byte)(stationNo & 0xFF);
        frame[7] = (byte)((stationNo >> 8) & 0xFF);
        frame[8] = 0x05;
        Buffer.BlockCopy(trimmedPayload, 0, frame, 9, trimmedPayload.Length);
        frame[^1] = 0x00;
        return frame;
    }

    public static byte[] BuildRelayNested(IEnumerable<(int LinkNo, int StationNo)> hops, byte[] innerPayload)
    {
        ArgumentNullException.ThrowIfNull(hops);
        var hopList = ToyopucRelay.NormalizeRelayHops(hops);
        return BuildRelayNested(hopList, ParseRelayInnerRequest(innerPayload));
    }

    internal static byte[] BuildRelayNested(
        IReadOnlyList<(int LinkNo, int StationNo)> hopList,
        RelayInnerRequest request)
    {
        var inner = request.TrimmedPayload;
        byte[]? frame = null;
        for (var i = hopList.Count - 1; i >= 0; i--)
        {
            frame = BuildRelayCommandFromTrimmed(hopList[i].LinkNo, hopList[i].StationNo, inner);
            inner = FrameToInnerPayload(frame);
        }

        return frame ?? throw new InvalidOperationException("Relay frame construction failed");
    }

    internal static RelayInnerRequest ParseRelayInnerRequest(byte[] innerPayload)
    {
        ArgumentNullException.ThrowIfNull(innerPayload);
        if (innerPayload.Length < 3)
        {
            throw new ArgumentException("inner payload must contain at least LL, LH, and CMD bytes", nameof(innerPayload));
        }

        var isFullCommand = innerPayload.Length >= 5
            && innerPayload[0] == FtCommand
            && innerPayload[1] == 0x00
            && (innerPayload[2] | (innerPayload[3] << 8)) + 4 == innerPayload.Length;
        var isTrimmed = (innerPayload[0] | (innerPayload[1] << 8)) + 2 == innerPayload.Length;
        if (!isFullCommand && !isTrimmed)
        {
            throw new ArgumentException(
                "inner payload must be either a complete command frame with FT=0x00, RC=0x00, and an exact declared length, or an exact LL/LH/CMD payload",
                nameof(innerPayload));
        }

        var trimmed = isFullCommand ? innerPayload[2..] : innerPayload.ToArray();
        var command = trimmed[2];
        var body = trimmed[3..];
        var isReadOnly = IsRelayReadOnlyRequest(command, body);
        return new RelayInnerRequest(
            trimmed,
            command,
            body,
            isReadOnly,
            isReadOnly ? GetRelayReadResponseLength(command, body) : null,
            isReadOnly ? null : GetRelayStateResponseData(command, body));
    }

    private static bool IsRelayReadOnlyRequest(byte command, byte[] body)
    {
        if (command is 0x1C or 0x1E or 0x20 or 0x22 or 0x24 or 0x94 or 0x96 or 0x98 or 0xC2 or 0xC4)
        {
            return true;
        }

        if (command == 0xA0 && body.Length >= 3 && body[2] == 0x00)
        {
            return body[1] is 0x11 or 0x40;
        }

        return command == 0x32
            && body.Length >= 2
            && ((body[0] == 0x70 && body[1] == 0x00)
                || (body[0] == 0x11 && body[1] == 0x00));
    }

    private static int? GetRelayReadResponseLength(byte command, byte[] body)
    {
        int ReadBodyU16(int offset)
        {
            if (offset < 0 || offset + 1 >= body.Length)
            {
                throw new ArgumentException(
                    $"CMD={command:X2} relay request body is too short to derive its response length",
                    nameof(body));
            }

            return body[offset] | (body[offset + 1] << 8);
        }

        return command switch
        {
            0x1C => checked(ReadBodyU16(2) * 2),
            0x1E => ReadBodyU16(2),
            0x20 => 1,
            0x22 => body.Length,
            0x24 => body.Length / 2,
            0x94 => checked(ReadBodyU16(3) * 2),
            0x96 => ReadBodyU16(3),
            0x98 when body.Length >= 3 => checked(((body[0] + 7) / 8) + body[1] + (body[2] * 2)),
            0x98 => throw new ArgumentException(
                "CMD=98 relay request body is too short to derive its response length",
                nameof(body)),
            0xA0 when body.Length >= 3 && body[1] == 0x11 && body[2] == 0x00 => 11,
            0xA0 when body.Length >= 3 && body[1] == 0x40 && body[2] == 0x00 => 7,
            0xC2 => ReadBodyU16(4),
            0xC4 when body.Length >= 4 => checked(4 + ((body[0] + 7) / 8) + body[1] + (body[2] * 2) + (body[3] * 4)),
            0xC4 => throw new ArgumentException(
                "CMD=C4 relay request body is too short to derive its response length",
                nameof(body)),
            _ => null,
        };
    }

    private static byte[]? GetRelayStateResponseData(byte command, byte[] body)
    {
        if (command is 0x1D or 0x1F or 0x21 or 0x23 or 0x25 or 0x95 or 0x97 or 0x99 or 0xC3 or 0xC5 or 0xCA)
        {
            return [];
        }

        if (command == 0x32
            && body.Length >= 2
            && ((body[0] == 0x71 && body[1] == 0x00)
                || (body[0] == 0x01 && body[1] == 0x00)
                || (body[0] == 0x02 && body[1] == 0x00)))
        {
            return body[..2];
        }
        if (command == 0xA0
            && body.Length >= 3
            && body[1] is 0x41 or 0x42 or 0x43
            && body[2] == 0x00)
        {
            return body[..3];
        }

        return null;
    }

    internal sealed record RelayInnerRequest(
        byte[] TrimmedPayload,
        byte Command,
        byte[] Body,
        bool IsReadOnly,
        int? ExpectedReadResponseLength,
        byte[]? ExpectedStateResponseData);

    private static byte[] FrameToInnerPayload(byte[] frame)
    {
        if (frame.Length < 5 || frame[0] != FtCommand || frame[1] != 0x00)
        {
            throw new ArgumentException("relay frame must be a normal command request", nameof(frame));
        }

        var payload = new byte[frame.Length - 2];
        Buffer.BlockCopy(frame, 2, payload, 0, payload.Length);
        return payload;
    }
}
