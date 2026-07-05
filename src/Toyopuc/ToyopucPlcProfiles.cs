namespace PlcComm.Toyopuc;

public static class ToyopucPlcProfiles
{
    public static ToyopucPlcProfile Generic { get; } = new(
        "toyopuc:generic",
        ToyopucAddressingOptions.Generic,
        CreateGenericAreas());

    public static ToyopucPlcProfile ToyopucPlusStandard { get; } = new(
        "toyopuc:plus:standard",
        ToyopucAddressingOptions.ToyopucPlusStandard,
        CreateToyopucPlusStandardAreas());

    public static ToyopucPlcProfile ToyopucPlusExtended { get; } = new(
        "toyopuc:plus:extended",
        ToyopucAddressingOptions.ToyopucPlusExtended,
        CreateToyopucPlusAreas());

    public static ToyopucPlcProfile Nano10GxMode { get; } = new(
        "toyopuc:nano-10gx:native",
        ToyopucAddressingOptions.Nano10GxMode,
        CreateNano10GxModeAreas());

    public static ToyopucPlcProfile Nano10GxCompatible { get; } = new(
        "toyopuc:nano-10gx:compatible",
        ToyopucAddressingOptions.Nano10GxCompatible,
        CreateNano10GxAreas());

    public static ToyopucPlcProfile Pc10GStandardPc3Jg { get; } = new(
        "toyopuc:pc10g:standard-pc3jg",
        ToyopucAddressingOptions.Pc10GStandardPc3Jg,
        CreatePc10StandardPc3JgAreas());

    public static ToyopucPlcProfile Pc10GMode { get; } = new(
        "toyopuc:pc10g:pc10",
        ToyopucAddressingOptions.Pc10GMode,
        CreatePc10ModeAreas());

    public static ToyopucPlcProfile Pc3JxPc3Separate { get; } = new(
        "toyopuc:pc3jx:pc3-separate",
        ToyopucAddressingOptions.Pc3JxPc3Separate,
        CreatePc3JxPc3Areas());

    public static ToyopucPlcProfile Pc3JxPlusExpansion { get; } = new(
        "toyopuc:pc3jx:plus-expansion",
        ToyopucAddressingOptions.Pc3JxPlusExpansion,
        CreatePc3JxPlusAreas());

    public static ToyopucPlcProfile Pc3JgMode { get; } = new(
        "toyopuc:pc3jg:pc3jg",
        ToyopucAddressingOptions.Pc3JgMode,
        CreatePc3JgModeAreas());

    public static ToyopucPlcProfile Pc3JgPc3Separate { get; } = new(
        "toyopuc:pc3jg:pc3-separate",
        ToyopucAddressingOptions.Pc3JgPc3Separate,
        CreatePc3JgPc3Areas());

    private static readonly string[] ProfileNames =
    [
        Generic.Name,
        ToyopucPlusStandard.Name,
        ToyopucPlusExtended.Name,
        Nano10GxMode.Name,
        Nano10GxCompatible.Name,
        Pc10GStandardPc3Jg.Name,
        Pc10GMode.Name,
        Pc3JxPc3Separate.Name,
        Pc3JxPlusExpansion.Name,
        Pc3JgMode.Name,
        Pc3JgPc3Separate.Name,
    ];

    public static IReadOnlyList<string> GetNames()
    {
        return ProfileNames;
    }

    public static string NormalizeName(string? profile)
    {
        return FromName(profile).Name;
    }

    public static string GetDisplayName(string? profile)
    {
        var normalized = FromName(profile).Name;
        return normalized switch
        {
            "toyopuc:generic" => "TOYOPUC Generic",
            "toyopuc:plus:standard" => "TOYOPUC Plus (standard)",
            "toyopuc:plus:extended" => "TOYOPUC Plus (extended)",
            "toyopuc:nano-10gx:native" => "TOYOPUC Nano 10GX (native)",
            "toyopuc:nano-10gx:compatible" => "TOYOPUC Nano 10GX (compatible)",
            "toyopuc:pc10g:standard-pc3jg" => "TOYOPUC PC10G (standard PC3JG)",
            "toyopuc:pc10g:pc10" => "TOYOPUC PC10G (PC10)",
            "toyopuc:pc3jx:pc3-separate" => "TOYOPUC PC3JX (PC3 separate)",
            "toyopuc:pc3jx:plus-expansion" => "TOYOPUC PC3JX (Plus expansion)",
            "toyopuc:pc3jg:pc3jg" => "TOYOPUC PC3JG (PC3JG)",
            "toyopuc:pc3jg:pc3-separate" => "TOYOPUC PC3JG (PC3 separate)",
            _ => throw new ArgumentException($"Unknown PLC profile: {profile}", nameof(profile)),
        };
    }

    public static ToyopucPlcProfile FromName(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new ArgumentException(
                $"PLC profile is required. Use an explicit canonical name such as '{Generic.Name}'.",
                nameof(profile));
        }

        var normalized = profile.Trim();
        if (normalized.Equals(Generic.Name, StringComparison.Ordinal))
        {
            return Generic;
        }

        if (normalized.Equals(ToyopucPlusStandard.Name, StringComparison.Ordinal))
        {
            return ToyopucPlusStandard;
        }

        if (normalized.Equals(ToyopucPlusExtended.Name, StringComparison.Ordinal))
        {
            return ToyopucPlusExtended;
        }

        if (normalized.Equals(Nano10GxMode.Name, StringComparison.Ordinal))
        {
            return Nano10GxMode;
        }

        if (normalized.Equals(Nano10GxCompatible.Name, StringComparison.Ordinal))
        {
            return Nano10GxCompatible;
        }

        if (normalized.Equals(Pc10GStandardPc3Jg.Name, StringComparison.Ordinal))
        {
            return Pc10GStandardPc3Jg;
        }

        if (normalized.Equals(Pc10GMode.Name, StringComparison.Ordinal))
        {
            return Pc10GMode;
        }

        if (normalized.Equals(Pc3JxPc3Separate.Name, StringComparison.Ordinal))
        {
            return Pc3JxPc3Separate;
        }

        if (normalized.Equals(Pc3JxPlusExpansion.Name, StringComparison.Ordinal))
        {
            return Pc3JxPlusExpansion;
        }

        if (normalized.Equals(Pc3JgMode.Name, StringComparison.Ordinal))
        {
            return Pc3JgMode;
        }

        if (normalized.Equals(Pc3JgPc3Separate.Name, StringComparison.Ordinal))
        {
            return Pc3JgPc3Separate;
        }

        throw new ArgumentException($"Unknown PLC profile: {profile}", nameof(profile));
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateGenericAreas()
    {
        return
        [
            PrefixedSplitBitArea("P", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedSplitBitArea("V", lowEnd: 0x00FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("T", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("C", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("L", lowEnd: 0x07FF, highEnd: 0x2FFF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedSplitBitArea("M", lowEnd: 0x07FF, highEnd: 0x17FF),
            PrefixedSplitWordArea("S", lowEnd: 0x03FF, highEnd: 0x13FF),
            PrefixedSplitWordArea("N", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x1FFFF),
            ExtWordArea("EB", 0x3FFFF),
            FrArea(0x1FFFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateToyopucPlusStandardAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateToyopucPlusAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateNano10GxModeAreas()
    {
        return
        [
            PrefixedSplitBitArea("P", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedSplitBitArea("V", lowEnd: 0x00FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("T", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("C", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("L", lowEnd: 0x07FF, highEnd: 0x2FFF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedSplitBitArea("M", lowEnd: 0x07FF, highEnd: 0x17FF),
            PrefixedSplitWordArea("S", lowEnd: 0x03FF, highEnd: 0x13FF),
            PrefixedSplitWordArea("N", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x1FFFF),
            ExtWordArea("EB", 0x3FFFF),
            FrArea(0x1FFFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateNano10GxAreas()
    {
        return CreateNano10GxModeAreas();
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc10StandardPc3JgAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
            ExtWordArea("EB", 0x1FFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc10ModeAreas()
    {
        return
        [
            PrefixedSplitBitArea("P", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedSplitBitArea("V", lowEnd: 0x00FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("T", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("C", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("L", lowEnd: 0x07FF, highEnd: 0x2FFF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedSplitBitArea("M", lowEnd: 0x07FF, highEnd: 0x17FF),
            PrefixedSplitWordArea("S", lowEnd: 0x03FF, highEnd: 0x13FF),
            PrefixedSplitWordArea("N", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF, packedDirectEnd: 0x0FFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x1FFFF),
            ExtWordArea("EB", 0x3FFFF),
            FrArea(0x1FFFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JxPc3Areas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JxPlusAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JgModeAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
            ExtWordArea("EB", 0x1FFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JgPc3Areas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
            ExtWordArea("EB", 0x1FFFF),
        ];
    }

    private static ToyopucAreaDescriptor SplitBitArea(string area, int lowEnd, int highEnd)
    {
        return BitArea(
            area,
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor PrefixedSplitBitArea(string area, int lowEnd, int highEnd)
    {
        return BitArea(
            area,
            [],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor SplitWordArea(string area, int lowEnd, int highEnd)
    {
        return WordArea(
            area,
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor PrefixedSplitWordArea(string area, int lowEnd, int highEnd)
    {
        return WordArea(
            area,
            [],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor BitArea(string area, int directEnd, int? prefixedEnd)
    {
        return BitArea(
            area,
            [Range(0x0000, directEnd)],
            prefixedEnd is null ? [] : [Range(0x0000, prefixedEnd.Value)]);
    }

    private static ToyopucAreaDescriptor PrefixedBitArea(string area, int prefixedEnd)
    {
        return BitArea(area, [], [Range(0x0000, prefixedEnd)]);
    }

    private static ToyopucAreaDescriptor BitArea(
        string area,
        IReadOnlyList<ToyopucAddressRange> directRanges,
        IReadOnlyList<ToyopucAddressRange> prefixedRanges)
    {
        return Area(
            area,
            directRanges,
            prefixedRanges,
            supportsPackedWord: true,
            addressWidth: 4,
            suggestedStartStep: 0x10);
    }

    private static ToyopucAreaDescriptor WordArea(string area, int directEnd, int? prefixedEnd = null)
    {
        return WordArea(
            area,
            [Range(0x0000, directEnd)],
            prefixedEnd is null ? [] : [Range(0x0000, prefixedEnd.Value)]);
    }

    private static ToyopucAreaDescriptor PrefixedWordArea(string area, int prefixedEnd)
    {
        return WordArea(area, [], [Range(0x0000, prefixedEnd)]);
    }

    private static ToyopucAreaDescriptor WordArea(
        string area,
        IReadOnlyList<ToyopucAddressRange> directRanges,
        IReadOnlyList<ToyopucAddressRange> prefixedRanges)
    {
        return Area(
            area,
            directRanges,
            prefixedRanges,
            supportsPackedWord: false,
            addressWidth: 4,
            suggestedStartStep: 0x10);
    }

    private static ToyopucAreaDescriptor ExtBitArea(string area, int directEnd, int? packedDirectEnd = null)
    {
        return Area(
            area,
            [Range(0x0000, directEnd)],
            [],
            supportsPackedWord: true,
            addressWidth: 4,
            suggestedStartStep: 0x10,
            packedDirectRangesOverride: packedDirectEnd is null ? null : [Range(0x0000, packedDirectEnd.Value)]);
    }

    private static ToyopucAreaDescriptor ExtWordArea(string area, int directEnd)
    {
        return Area(
            area,
            [Range(0x0000, directEnd)],
            [],
            supportsPackedWord: false,
            addressWidth: 5,
            suggestedStartStep: 0x100);
    }

    private static ToyopucAreaDescriptor FrArea(int directEnd)
    {
        return Area(
            "FR",
            [Range(0x000000, directEnd)],
            [],
            supportsPackedWord: false,
            addressWidth: 6,
            suggestedStartStep: 0x1000);
    }

    private static ToyopucAreaDescriptor Area(
        string area,
        IReadOnlyList<ToyopucAddressRange> directRanges,
        IReadOnlyList<ToyopucAddressRange> prefixedRanges,
        bool supportsPackedWord,
        int addressWidth,
        int suggestedStartStep,
        IReadOnlyList<ToyopucAddressRange>? packedDirectRangesOverride = null,
        IReadOnlyList<ToyopucAddressRange>? packedPrefixedRangesOverride = null)
    {
        return new ToyopucAreaDescriptor(
            area,
            directRanges,
            prefixedRanges,
            supportsPackedWord,
            addressWidth,
            suggestedStartStep,
            packedDirectRangesOverride,
            packedPrefixedRangesOverride);
    }

    private static ToyopucAddressRange Range(int start, int end)
    {
        return new ToyopucAddressRange(start, end);
    }
}
