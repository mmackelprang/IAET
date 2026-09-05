namespace Iaet.DukeEnergy.Api;

/// <summary>
/// Host-level settings for the Duke Energy REST service.
/// </summary>
public sealed class DukeEnergyApiOptions
{
    /// <summary>Port to listen on.</summary>
    public int Port { get; set; } = 9300;

    /// <summary>
    /// Whether to bind all interfaces rather than loopback only. The service holds account
    /// configuration and can file outage reports, so it stays on loopback unless asked otherwise.
    /// </summary>
    public bool ListenOnAllInterfaces { get; set; }

    /// <summary>
    /// Optional path to a JSON settings file supplying the <c>DukeEnergy</c> configuration section.
    /// </summary>
    public string? SettingsPath { get; set; }
}
