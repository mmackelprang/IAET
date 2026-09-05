namespace Iaet.DukeEnergy.Models;

/// <summary>
/// A street address resolved to a coordinate.
/// </summary>
/// <param name="InputAddress">The address as the caller supplied it.</param>
/// <param name="MatchedAddress">The normalized address the geocoder matched, when it reports one.</param>
/// <param name="Point">The resolved coordinate.</param>
/// <param name="Source">Which geocoder produced the match, for example <c>"census"</c>.</param>
public sealed record GeocodedAddress(
    string InputAddress,
    string? MatchedAddress,
    GeoPoint Point,
    string Source);
