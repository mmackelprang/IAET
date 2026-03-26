namespace Iaet.Core.Models;

public sealed record EndpointDescriptor(
    string HumanName,
    string Category,
    bool IsDestructive,
    string? Notes
);
