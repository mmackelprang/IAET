namespace Iaet.Core.Models;

public sealed record ReplayResult(
    int ResponseStatus,
    string? ResponseBody,
    IReadOnlyList<FieldDiff> Diffs,
    long DurationMs
);

public sealed record FieldDiff(string Path, string? Expected, string? Actual);
