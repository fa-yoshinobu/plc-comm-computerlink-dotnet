using System.Text.Json;

namespace PlcComm.Toyopuc.Tests;

public sealed class CanonicalProfilesTests
{
    [Fact]
    public void EmbeddedToyopucProfiles_MatchCanonicalFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "toyopuc_profiles.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var profiles = document.RootElement.GetProperty("profiles");
        var expectedProfileIds = profiles.EnumerateObject().Select(static property => property.Name).ToArray();

        Assert.Equal(expectedProfileIds, ToyopucPlcProfiles.GetNames());
        foreach (var profileProperty in profiles.EnumerateObject())
        {
            var expectedProfile = profileProperty.Value;
            Assert.Equal(
                expectedProfile.GetProperty("display_name").GetString(),
                ToyopucPlcProfiles.GetDisplayName(profileProperty.Name));

            var actual = ToyopucPlcProfiles.FromName(profileProperty.Name);
            AssertOptions(expectedProfile.GetProperty("addressing_options"), actual.AddressingOptions);
            AssertAreas(expectedProfile.GetProperty("areas"), actual.Areas);
        }
    }

    [Fact]
    public void ProfileDescriptors_MatchCanonicalProfileMetadata()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "toyopuc_profiles.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var profiles = document.RootElement.GetProperty("profiles");
        var expectedProfileIds = profiles.EnumerateObject().Select(static property => property.Name).ToArray();
        var descriptors = ToyopucPlcProfiles.GetProfileDescriptors();

        Assert.Equal(expectedProfileIds, descriptors.Select(static descriptor => descriptor.CanonicalName));
        foreach (var descriptor in descriptors)
        {
            var expected = profiles.GetProperty(descriptor.CanonicalName);
            Assert.Equal(expected.GetProperty("display_name").GetString(), descriptor.DisplayName);
            Assert.True(descriptor.Connectable);
            Assert.False(expected.TryGetProperty("base_profile", out _));
            Assert.Null(descriptor.BaseProfile);
        }
    }

    private static void AssertOptions(JsonElement expected, ToyopucAddressingOptions actual)
    {
        Assert.Equal(expected.GetProperty("use_upper_u_pc10").GetBoolean(), actual.UseUpperUPc10);
        Assert.Equal(expected.GetProperty("use_eb_pc10").GetBoolean(), actual.UseEbPc10);
        Assert.Equal(expected.GetProperty("use_fr_pc10").GetBoolean(), actual.UseFrPc10);
    }

    private static void AssertAreas(JsonElement expected, IReadOnlyList<ToyopucAreaDescriptor> actual)
    {
        var expectedAreas = expected.EnumerateArray().ToArray();
        Assert.Equal(expectedAreas.Length, actual.Count);
        for (var index = 0; index < expectedAreas.Length; index++)
        {
            var expectedArea = expectedAreas[index];
            var actualArea = actual[index];
            Assert.Equal(expectedArea.GetProperty("area").GetString(), actualArea.Area);
            AssertRanges(expectedArea.GetProperty("direct_ranges"), actualArea.DirectRanges);
            AssertRanges(expectedArea.GetProperty("prefixed_ranges"), actualArea.PrefixedRanges);
            Assert.Equal(expectedArea.GetProperty("supports_packed_word").GetBoolean(), actualArea.SupportsPackedWord);
            Assert.Equal(expectedArea.GetProperty("address_width").GetInt32(), actualArea.AddressWidth);
            Assert.Equal(expectedArea.GetProperty("suggested_start_step").GetInt32(), actualArea.SuggestedStartStep);
            AssertOptionalRanges(
                expectedArea,
                "packed_direct_ranges_override",
                actualArea.PackedDirectRangesOverride);
            AssertOptionalRanges(
                expectedArea,
                "packed_prefixed_ranges_override",
                actualArea.PackedPrefixedRangesOverride);
        }
    }

    private static void AssertOptionalRanges(
        JsonElement expectedArea,
        string propertyName,
        IReadOnlyList<ToyopucAddressRange>? actual)
    {
        if (expectedArea.TryGetProperty(propertyName, out var expectedRanges))
        {
            Assert.NotNull(actual);
            AssertRanges(expectedRanges, actual!);
        }
        else
        {
            Assert.Null(actual);
        }
    }

    private static void AssertRanges(JsonElement expected, IReadOnlyList<ToyopucAddressRange> actual)
    {
        var expectedRanges = expected.EnumerateArray().ToArray();
        Assert.Equal(expectedRanges.Length, actual.Count);
        for (var index = 0; index < expectedRanges.Length; index++)
        {
            Assert.Equal(expectedRanges[index].GetProperty("start").GetInt32(), actual[index].Start);
            Assert.Equal(expectedRanges[index].GetProperty("end").GetInt32(), actual[index].End);
        }
    }
}
