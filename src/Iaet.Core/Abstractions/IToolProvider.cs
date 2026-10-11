using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

/// <summary>
/// An external analysis engine or runtime that IAET can delegate to. Providers declare the
/// capabilities they unlock and can be probed by <c>iaet doctor</c> before an investigation
/// relies on them. Modeled on the provider registry / capability router pattern.
/// </summary>
public interface IToolProvider
{
    string Name { get; }

    IReadOnlyList<string> Capabilities { get; }

    Task<ToolHealth> CheckAsync(CancellationToken ct = default);
}
