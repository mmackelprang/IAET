namespace Iaet.DukeEnergy;

/// <summary>
/// Duke Energy operating-company codes accepted by the public outage-map API as the
/// <c>jurisdiction</c> query parameter.
/// </summary>
/// <remarks>
/// <para>
/// The four codes exercised by known public consumers of the outage-map API are
/// <see cref="Carolinas"/>, <see cref="Florida"/>, <see cref="Indiana"/> and
/// <see cref="Midwest"/>. <see cref="Progress"/> is included because Duke Energy Progress is a
/// distinct operating company, but it has not been verified against the live API.
/// </para>
/// <para>
/// The API accepts arbitrary strings, so callers are never restricted to these constants.
/// </para>
/// </remarks>
public static class Jurisdictions
{
    /// <summary>Duke Energy Carolinas.</summary>
    public const string Carolinas = "DEC";

    /// <summary>Duke Energy Progress (unverified against the live API).</summary>
    public const string Progress = "DEP";

    /// <summary>Duke Energy Florida.</summary>
    public const string Florida = "DEF";

    /// <summary>Duke Energy Indiana.</summary>
    public const string Indiana = "DEI";

    /// <summary>Duke Energy Midwest (Ohio and Kentucky).</summary>
    public const string Midwest = "DEM";

    private static readonly string[] AllCodes = [Carolinas, Progress, Florida, Indiana, Midwest];

    /// <summary>Gets every jurisdiction code known to this library.</summary>
    public static IReadOnlyList<string> All => AllCodes;
}
