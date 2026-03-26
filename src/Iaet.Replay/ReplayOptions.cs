namespace Iaet.Replay;

/// <summary>
/// Configuration options for <see cref="HttpReplayEngine"/>.
/// </summary>
public sealed class ReplayOptions
{
    /// <summary>Maximum HTTP replays allowed per minute.</summary>
    public int RequestsPerMinute { get; set; } = 10;

    /// <summary>Maximum HTTP replays allowed per day.</summary>
    public int RequestsPerDay { get; set; } = 100;

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// When <see langword="true"/> the engine skips the actual HTTP call and returns an
    /// empty <see cref="Iaet.Core.Models.ReplayResult"/> with status 0.
    /// </summary>
    public bool DryRun { get; set; }
}
