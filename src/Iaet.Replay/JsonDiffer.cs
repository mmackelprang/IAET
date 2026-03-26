using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.Replay;

/// <summary>
/// Performs field-level JSON comparison using JSONPath-style paths (e.g. <c>$.user.name</c>).
/// </summary>
public static class JsonDiffer
{
    /// <summary>
    /// Compares <paramref name="expected"/> and <paramref name="actual"/> JSON strings and
    /// returns a list of <see cref="FieldDiff"/> entries for every changed, added, or removed field.
    /// </summary>
    /// <param name="expected">The baseline JSON string (may be <see langword="null"/>).</param>
    /// <param name="actual">The observed JSON string (may be <see langword="null"/>).</param>
    /// <returns>An empty list when the documents are identical; otherwise the set of differences.</returns>
    public static IReadOnlyList<FieldDiff> Diff(string? expected, string? actual)
    {
        if (expected is null && actual is null)
        {
            return [];
        }

        var diffs = new List<FieldDiff>();

        if (expected is null)
        {
            // Every field in actual is an addition (expected = null)
            using var actualDoc = JsonDocument.Parse(actual!);
            CollectAll(actualDoc.RootElement, "$", isExpected: false, diffs);
            return diffs;
        }

        if (actual is null)
        {
            // Every field in expected is a removal (actual = null)
            using var expectedDoc = JsonDocument.Parse(expected);
            CollectAll(expectedDoc.RootElement, "$", isExpected: true, diffs);
            return diffs;
        }

        using var expDoc = JsonDocument.Parse(expected);
        using var actDoc = JsonDocument.Parse(actual);
        CompareElements(expDoc.RootElement, actDoc.RootElement, "$", diffs);
        return diffs;
    }

    private static void CompareElements(
        JsonElement exp,
        JsonElement act,
        string path,
        List<FieldDiff> diffs)
    {
        if (exp.ValueKind == JsonValueKind.Object && act.ValueKind == JsonValueKind.Object)
        {
            // Check properties in expected
            foreach (var prop in exp.EnumerateObject())
            {
                var childPath = $"{path}.{prop.Name}";
                if (act.TryGetProperty(prop.Name, out var actProp))
                {
                    CompareElements(prop.Value, actProp, childPath, diffs);
                }
                else
                {
                    diffs.Add(new FieldDiff(childPath, Serialize(prop.Value), null));
                }
            }

            // Check for properties only in actual (additions)
            foreach (var prop in act.EnumerateObject())
            {
                if (!exp.TryGetProperty(prop.Name, out _))
                {
                    diffs.Add(new FieldDiff($"{path}.{prop.Name}", null, Serialize(prop.Value)));
                }
            }
        }
        else
        {
            var expStr = Serialize(exp);
            var actStr = Serialize(act);
            if (expStr != actStr)
            {
                diffs.Add(new FieldDiff(path, expStr, actStr));
            }
        }
    }

    /// <summary>
    /// Recursively enumerates all leaf and object fields and records them as either
    /// expected-only or actual-only differences.
    /// </summary>
    private static void CollectAll(
        JsonElement element,
        string path,
        bool isExpected,
        List<FieldDiff> diffs)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                CollectAll(prop.Value, $"{path}.{prop.Name}", isExpected, diffs);
            }
        }
        else
        {
            var value = Serialize(element);
            var diff = isExpected
                ? new FieldDiff(path, value, null)
                : new FieldDiff(path, null, value);
            diffs.Add(diff);
        }
    }

    private static string Serialize(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.Null   => "null",
            _                    => element.GetRawText(),
        };
}
