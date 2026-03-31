namespace Iaet.Core.Models;

public sealed record NetworkSecurityConfig
{
    public IReadOnlyList<PinnedDomain> PinnedDomains { get; init; } = [];
    public IReadOnlyList<string> CleartextPermittedDomains { get; init; } = [];
    public bool CleartextDefaultPermitted { get; init; } = true;
}
