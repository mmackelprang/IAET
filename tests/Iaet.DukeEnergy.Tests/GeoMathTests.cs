using FluentAssertions;

namespace Iaet.DukeEnergy.Tests;

public class GeoMathTests
{
    [Fact]
    public void DistanceMiles_returns_zero_for_identical_points()
    {
        GeoMath.DistanceMiles(35.7796, -78.6382, 35.7796, -78.6382).Should().Be(0);
    }

    [Fact]
    public void DistanceMiles_matches_known_distance_between_raleigh_and_charlotte()
    {
        // Raleigh NC to Charlotte NC is roughly 130 miles great-circle.
        var miles = GeoMath.DistanceMiles(35.7796, -78.6382, 35.2271, -80.8431);

        miles.Should().BeApproximately(130.0, 3.0);
    }

    [Fact]
    public void DistanceMiles_is_symmetric()
    {
        var forward = GeoMath.DistanceMiles(35.7796, -78.6382, 35.2271, -80.8431);
        var reverse = GeoMath.DistanceMiles(35.2271, -80.8431, 35.7796, -78.6382);

        forward.Should().BeApproximately(reverse, 1e-9);
    }

    [Fact]
    public void DistanceMiles_handles_a_short_neighborhood_scale_hop()
    {
        // 0.01 degrees of latitude is about 0.69 miles anywhere on Earth.
        GeoMath.DistanceMiles(35.7796, -78.6382, 35.7896, -78.6382)
            .Should().BeApproximately(0.69, 0.02);
    }
}
