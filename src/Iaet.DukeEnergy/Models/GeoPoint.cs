namespace Iaet.DukeEnergy.Models;

/// <summary>A WGS-84 coordinate pair.</summary>
/// <param name="Latitude">Latitude in degrees.</param>
/// <param name="Longitude">Longitude in degrees.</param>
public sealed record GeoPoint(double Latitude, double Longitude);
