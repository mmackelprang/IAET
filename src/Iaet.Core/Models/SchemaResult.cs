namespace Iaet.Core.Models;

public sealed record SchemaResult(
    string JsonSchema,
    string CSharpRecord,
    string OpenApiFragment,
    IReadOnlyList<string> Warnings
);
