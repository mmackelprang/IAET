namespace Iaet.Schema;

/// <summary>
/// Result of merging multiple <see cref="JsonTypeMap"/> instances.
/// </summary>
public sealed record MergeResult(JsonTypeMap MergedMap, IReadOnlyList<string> Warnings);

/// <summary>
/// Merges multiple <see cref="JsonTypeMap"/> instances, detecting nullable fields, optional fields, and type conflicts.
/// </summary>
public static class TypeMerger
{
    /// <summary>
    /// Merges a list of <see cref="JsonTypeMap"/> instances into a single map with conflict warnings.
    /// </summary>
    public static MergeResult Merge(IReadOnlyList<JsonTypeMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        if (maps.Count == 0)
        {
            return new MergeResult(JsonTypeMap.FromFields(new Dictionary<string, FieldInfo>(StringComparer.Ordinal)), []);
        }

        var warnings = new List<string>();
        var allKeys = new HashSet<string>(StringComparer.Ordinal);

        // Collect all field names across all maps
        foreach (var map in maps)
        {
            foreach (var key in map.Fields.Keys)
            {
                allKeys.Add(key);
            }
        }

        var merged = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);

        foreach (var key in allKeys)
        {
            FieldInfo? current = null;
            bool isOptional = false;

            foreach (var map in maps)
            {
                if (!map.Fields.TryGetValue(key, out var field))
                {
                    // Field missing in this sample — it's optional
                    isOptional = true;
                    continue;
                }

                if (current is null)
                {
                    current = field;
                }
                else
                {
                    // Detect type conflicts before merging
                    if (current.JsonType != JsonFieldType.Null
                        && field.JsonType != JsonFieldType.Null
                        && current.JsonType != field.JsonType)
                    {
                        warnings.Add($"Type conflict on field '{key}': {current.JsonType} vs {field.JsonType}");
                    }

                    current = JsonTypeMap.MergeFieldInfo(current, field);
                }
            }

            if (current is not null)
            {
                if (isOptional)
                {
                    current = current with { IsNullable = true };
                }

                merged[key] = current;
            }
        }

        return new MergeResult(JsonTypeMap.FromFields(merged), warnings);
    }
}
