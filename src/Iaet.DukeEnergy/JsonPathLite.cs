using System.Globalization;
using System.Text.Json;

namespace Iaet.DukeEnergy;

/// <summary>
/// Resolves dotted paths such as <c>data.account[0].number</c> against a JSON document, and
/// coerces the result to a string.
/// </summary>
/// <remarks>
/// Deliberately far smaller than JSONPath: endpoint profiles only ever need to pull scalars out
/// of a known response shape, and a small resolver keeps profiles readable.
/// </remarks>
public static class JsonPathLite
{
    /// <summary>Resolves a dotted path against a JSON element.</summary>
    /// <param name="root">The element to resolve against.</param>
    /// <param name="path">A dotted path; segments may carry <c>[n]</c> array indexers.</param>
    /// <param name="value">The resolved element when the path matched.</param>
    /// <returns><see langword="true"/> when the whole path resolved.</returns>
    public static bool TrySelect(JsonElement root, string path, out JsonElement value)
    {
        value = root;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var current = root;

        foreach (var rawSegment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = rawSegment;

            // Split "name[0][1]" into the property name and its trailing indexers.
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            var name    = bracket < 0 ? segment : segment[..bracket];
            var indexer = bracket < 0 ? string.Empty : segment[bracket..];

            if (name.Length > 0)
            {
                if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, name, out current))
                {
                    return false;
                }
            }

            while (indexer.Length > 0)
            {
                var close = indexer.IndexOf(']', StringComparison.Ordinal);
                if (close < 1 || indexer[0] != '[')
                {
                    return false;
                }

                if (!int.TryParse(indexer[1..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    || current.ValueKind != JsonValueKind.Array
                    || index < 0
                    || index >= current.GetArrayLength())
                {
                    return false;
                }

                current = current[index];
                indexer = indexer[(close + 1)..];
            }
        }

        value = current;
        return true;
    }

    /// <summary>Resolves a dotted path and coerces the result to a string.</summary>
    /// <param name="root">The element to resolve against.</param>
    /// <param name="path">A dotted path.</param>
    /// <returns>The resolved scalar, or <see langword="null"/> when the path did not resolve.</returns>
    public static string? SelectString(JsonElement root, string path)
        => TrySelect(root, path, out var element) ? AsString(element) : null;

    /// <summary>Coerces a JSON scalar to its string form.</summary>
    /// <param name="element">The element to coerce.</param>
    /// <returns>
    /// The string form of a string, number or boolean; <see langword="null"/> for null, arrays and
    /// objects.
    /// </returns>
    public static string? AsString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String                     => element.GetString(),
        JsonValueKind.Number                     => element.GetRawText(),
        JsonValueKind.True or JsonValueKind.False => element.GetBoolean() ? "true" : "false",
        _                                        => null,
    };

    /// <summary>Looks up a property without regard to casing.</summary>
    /// <param name="element">The object to search.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The matched property value.</param>
    /// <returns><see langword="true"/> when a property matched.</returns>
    public static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        value = default;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Exact match first; Duke's payloads are camelCase, so this is the common path.
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
