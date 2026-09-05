namespace Iaet.DukeEnergy;

/// <summary>
/// Great-circle distance helpers used to decide which outages count as "in the neighborhood".
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusMiles = 3958.7613;

    /// <summary>
    /// Calculates the great-circle distance in statute miles between two WGS-84 coordinates.
    /// </summary>
    /// <param name="latitude1">Latitude of the first point, in degrees.</param>
    /// <param name="longitude1">Longitude of the first point, in degrees.</param>
    /// <param name="latitude2">Latitude of the second point, in degrees.</param>
    /// <param name="longitude2">Longitude of the second point, in degrees.</param>
    /// <returns>The distance between the two points in miles.</returns>
    public static double DistanceMiles(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        var lat1 = double.DegreesToRadians(latitude1);
        var lat2 = double.DegreesToRadians(latitude2);
        var dLat = lat2 - lat1;
        var dLon = double.DegreesToRadians(longitude2 - longitude1);

        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
              + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return 2 * EarthRadiusMiles * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
    }
}
