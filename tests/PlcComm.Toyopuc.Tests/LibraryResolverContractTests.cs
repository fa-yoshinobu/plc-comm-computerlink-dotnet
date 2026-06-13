namespace PlcComm.Toyopuc.Tests;

public sealed class LibraryResolverContractTests
{
    private const string GenericProfile = "toyopuc:generic";

    public static TheoryData<string> ReviewedProfiles => new()
    {
        "toyopuc:generic",
        "toyopuc:plus:standard",
        "toyopuc:plus:extended",
        "toyopuc:nano-10gx:native",
        "toyopuc:nano-10gx:compatible",
        "toyopuc:pc10g:standard-pc3jg",
        "toyopuc:pc10g:pc10",
        "toyopuc:pc3jx:pc3-separate",
        "toyopuc:pc3jx:plus-expansion",
        "toyopuc:pc3jg:pc3jg",
        "toyopuc:pc3jg:pc3-separate",
    };

    public static TheoryData<string> PrefixedAcceptCases => new()
    {
        "P1-D0000",
        "P2-D0000",
        "P3-D0000",
        "P1-M0000",
        "P1-D0000L",
        "P1-M000W",
    };

    public static TheoryData<string> PrefixedRejectCases => new()
    {
        "D0000",
        "M0000",
        "D0000L",
        "M000W",
    };

    public static TheoryData<string, string?, string, bool, bool> DerivedAcceptCases => new()
    {
        { "EP0FFW", null, "EP", true, false },
        { "EP0FFL", null, "EP", false, true },
        { "EP0FFH", null, "EP", false, true },
        { "GMFFFW", null, "GM", true, false },
        { "GMFFFL", null, "GM", false, true },
        { "GMFFFH", null, "GM", false, true },
        { "P1-M17FW", "toyopuc:pc10g:pc10", "M", true, true },
        { "P1-M17FL", "toyopuc:pc10g:pc10", "M", false, true },
    };

    public static TheoryData<string, string?> DerivedRejectCases => new()
    {
        { "M0000W", null },
        { "M0000L", null },
        { "M0000H", null },
        { "M17FW", null },
        { "M17FL", null },
        { "M17FH", null },
        { "EP0000W", null },
        { "EP0000L", null },
        { "EP0000H", null },
        { "GM1000W", null },
        { "GM1000L", null },
        { "GM1000H", null },
        { "P1-M0000W", "toyopuc:pc10g:pc10" },
    };

    public static TheoryData<string, string, string?> CanonicalRoundTripCases => new()
    {
        { "P1-D0000", "P1-D0000", "toyopuc:pc10g:pc10" },
        { "P1-D0000L", "P1-D0000L", "toyopuc:pc10g:pc10" },
        { "P1-M000W", "P1-M000W", "toyopuc:pc10g:pc10" },
        { "GX000W", "GX000W", "toyopuc:pc10g:pc10" },
        { "GY000W", "GY000W", "toyopuc:pc10g:pc10" },
    };

    [Theory]
    [MemberData(nameof(ReviewedProfiles))]
    public void ProfileBackedResolver_AcceptsPrefixedBasicFamilies(string profile)
    {
        var options = ToyopucAddressingOptions.FromProfile(profile);

        foreach (var device in PrefixedAcceptCases)
        {
            var resolved = ToyopucDeviceResolver.ResolveDevice(device, options, profile);

            Assert.NotNull(resolved.Prefix);
            Assert.Matches("^P[123]$", resolved.Prefix);
            Assert.Equal(device, resolved.Text);
        }
    }

    [Theory]
    [MemberData(nameof(ReviewedProfiles))]
    public void ProfileBackedResolver_RejectsUnprefixedBasicFamilies(string profile)
    {
        var options = ToyopucAddressingOptions.FromProfile(profile);

        foreach (var device in PrefixedRejectCases)
        {
            Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice(device, options, profile));
        }
    }

    [Theory]
    [MemberData(nameof(DerivedAcceptCases))]
    public void Resolver_AcceptsManualDerivedWidth(
        string device,
        string? profile,
        string expectedArea,
        bool packed,
        bool derivedUnit)
    {
        var resolved = Resolve(device, profile);

        Assert.Equal(expectedArea, resolved.Area);
        Assert.Equal(packed, resolved.Packed);
        Assert.Equal(device, resolved.Text);
        if (packed)
        {
            Assert.Equal("word", resolved.Unit);
        }
        else if (derivedUnit)
        {
            Assert.Equal("byte", resolved.Unit);
        }
    }

    [Theory]
    [MemberData(nameof(DerivedRejectCases))]
    public void Resolver_RejectsBitWidthNotationForDerivedDevices(string device, string? profile)
    {
        Assert.Throws<ArgumentException>(() => Resolve(device, profile));
    }

    [Theory]
    [MemberData(nameof(CanonicalRoundTripCases))]
    public void CanonicalFormatter_PreservesPrefixAndExplicitAreaNames(
        string input,
        string expectedCanonical,
        string? profile)
    {
        var first = Resolve(input, profile);
        var canonical = BuildCanonicalText(first, profile);
        var second = Resolve(canonical, profile);

        Assert.Equal(expectedCanonical, canonical);
        Assert.DoesNotContain("GXY", canonical, StringComparison.Ordinal);
        Assert.Equal(first.Prefix, second.Prefix);
        Assert.Equal(first.Area, second.Area);
        Assert.Equal(first.Index, second.Index);
        Assert.Equal(first.Unit, second.Unit);
        Assert.Equal(first.High, second.High);
        Assert.Equal(first.Packed, second.Packed);
    }

    [Fact]
    public void Resolver_RejectsSyntheticGxyArea()
    {
        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("GXY000W", profile: GenericProfile));
    }

    [Theory]
    [MemberData(nameof(ReviewedProfiles))]
    public void Catalog_DoesNotGenerateDirectBasicStartAddressesForProfile(string profile)
    {
        Assert.Throws<ArgumentException>(() => ToyopucDeviceCatalog.GetSuggestedStartAddresses("D", prefix: null, profile: profile));
        Assert.Throws<ArgumentException>(() => ToyopucDeviceCatalog.GetSuggestedStartAddresses("M", prefix: null, unit: "word", packed: true, profile: profile));

        var prefixedD = ToyopucDeviceCatalog.GetSuggestedStartAddresses("D", prefix: "P1", profile: profile);
        var prefixedM = ToyopucDeviceCatalog.GetSuggestedStartAddresses("M", prefix: "P1", unit: "word", packed: true, profile: profile);

        Assert.NotEmpty(prefixedD);
        Assert.NotEmpty(prefixedM);
    }

    private static ResolvedDevice Resolve(string device, string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = GenericProfile;
        }

        var options = ToyopucAddressingOptions.FromProfile(profile);
        return ToyopucDeviceResolver.ResolveDevice(device, options, profile);
    }

    private static string BuildCanonicalText(ResolvedDevice resolved, string? profile)
    {
        profile = string.IsNullOrWhiteSpace(profile) ? GenericProfile : profile;
        return ToyopucAddress.Format(resolved, resolved.Index, profile);
    }
}
