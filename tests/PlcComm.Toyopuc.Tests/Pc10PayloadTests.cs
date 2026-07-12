namespace PlcComm.Toyopuc.Tests;

public class Pc10PayloadTests
{
    [Fact]
    public void PackWordValues_PreservesCurrentLittleEndianWordPacking()
    {
        var payload = Pc10Payloads.PackWordValues(new[] { 0x1234, 0x5678, 0xFFFF });

        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56, 0xFF, 0xFF }, payload);
        Assert.Throws<ArgumentOutOfRangeException>(() => Pc10Payloads.PackWordValues([-1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pc10Payloads.PackWordValues([65536]));
    }

    [Fact]
    public void BuildMultiWordReadPayload_PreservesCurrentCountAndAddressLayout()
    {
        var payload = Pc10Payloads.BuildMultiWordReadPayload(new[] { 0x04000000, 0x10000000, 0x00007FFF });

        Assert.Equal(
            new byte[]
            {
                0x00, 0x00, 0x03, 0x00,
                0x00, 0x00, 0x00, 0x04,
                0x00, 0x00, 0x00, 0x10,
                0xFF, 0x7F, 0x00, 0x00,
            },
            payload);
    }

    [Fact]
    public void PayloadBuilders_RejectCountWrapAndOversizedWrites()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Pc10Payloads.BuildMultiWordReadPayload(Enumerable.Repeat(0x00100000, 0x80)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Pc10Payloads.PackMultiWordPayload(Enumerable.Repeat((Address32: 0x00100000, Value: 0), 85)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Pc10Payloads.PackMultiBitPayload(Enumerable.Range(0, 124).Select(i => (Address32: 0x00100000 + i, Value: 1))));
    }

    [Fact]
    public void PackMultiWordPayload_PreservesCurrentAddressThenValueLayout()
    {
        var payload = Pc10Payloads.PackMultiWordPayload(new[]
        {
            (Address32: 0x04000000, Value: 0x1234),
            (Address32: 0x10000000, Value: 0x5678),
        });

        Assert.Equal(
            new byte[]
            {
                0x00, 0x00, 0x02, 0x00,
                0x00, 0x00, 0x00, 0x04,
                0x00, 0x00, 0x00, 0x10,
                0x34, 0x12,
                0x78, 0x56,
            },
            payload);
    }

    [Fact]
    public void PackMultiBitPayload_PreservesCurrentBitPackingAcrossByteBoundary()
    {
        var payload = Pc10Payloads.PackMultiBitPayload(new[]
        {
            (Address32: 0x04000000, Value: 1),
            (Address32: 0x04000001, Value: 0),
            (Address32: 0x04000002, Value: 1),
            (Address32: 0x04000003, Value: 1),
            (Address32: 0x04000004, Value: 0),
            (Address32: 0x04000005, Value: 1),
            (Address32: 0x04000006, Value: 0),
            (Address32: 0x04000007, Value: 1),
            (Address32: 0x04000008, Value: 1),
        });

        Assert.Equal(
            new byte[]
            {
                0x09, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x04,
                0x01, 0x00, 0x00, 0x04,
                0x02, 0x00, 0x00, 0x04,
                0x03, 0x00, 0x00, 0x04,
                0x04, 0x00, 0x00, 0x04,
                0x05, 0x00, 0x00, 0x04,
                0x06, 0x00, 0x00, 0x04,
                0x07, 0x00, 0x00, 0x04,
                0x08, 0x00, 0x00, 0x04,
                0xAD, 0x01,
            },
            payload);
    }

    [Fact]
    public void BuildMultiBitReadPayload_PreservesCurrentCountAndAddressLayout()
    {
        var payload = Pc10Payloads.BuildMultiBitReadPayload(new[] { 0x04000000, 0x04000008 });

        Assert.Equal(
            new byte[]
            {
                0x02, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x04,
                0x08, 0x00, 0x00, 0x04,
            },
            payload);
    }
}
