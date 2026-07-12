namespace PlcComm.Toyopuc;

/// <summary>
/// Selects the direct or relay route used by a high-level TOYOPUC session.
/// </summary>
public sealed class ToyopucRoute
{
    private ToyopucRoute(IReadOnlyList<(int LinkNo, int StationNo)>? relayHops)
    {
        RelayHops = relayHops;
    }

    /// <summary>Gets the explicit direct route.</summary>
    public static ToyopucRoute Direct { get; } = new(null);

    /// <summary>Creates an explicit relay route with one or more validated hops.</summary>
    /// <param name="hops">Relay text or link/station tuples.</param>
    /// <returns>A validated relay route.</returns>
    public static ToyopucRoute Relay(object hops)
    {
        ArgumentNullException.ThrowIfNull(hops);
        return new ToyopucRoute(ToyopucRelay.NormalizeRelayHops(hops));
    }

    /// <summary>Gets a value indicating whether this is a relay route.</summary>
    public bool UsesRelay => RelayHops is not null;

    /// <summary>Gets the validated relay hops, or <see langword="null"/> for a direct route.</summary>
    public IReadOnlyList<(int LinkNo, int StationNo)>? RelayHops { get; }
}
