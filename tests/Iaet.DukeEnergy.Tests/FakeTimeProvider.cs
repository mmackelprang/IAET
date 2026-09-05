namespace Iaet.DukeEnergy.Tests;

/// <summary>A manually advanced clock, so cache-expiry tests do not have to wait.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    internal FakeTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    internal void Advance(TimeSpan delta) => _now += delta;
}
