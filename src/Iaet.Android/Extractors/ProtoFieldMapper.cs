namespace Iaet.Android.Extractors;

using System.Text.RegularExpressions;
using Iaet.Core.Models;

/// <summary>
/// Scans decompiled Java source for protobuf-generated code patterns
/// that reveal field names at specific array positions.
/// Works with obfuscated code by matching on SDK types and positional access patterns.
/// </summary>
public static partial class ProtoFieldMapper
{
    /// <summary>
    /// Scan a single Java file for proto field mappings.
    /// </summary>
    public static IReadOnlyList<ProtoFieldMapping> Extract(string javaSource, string sourceFile)
    {
        if (string.IsNullOrEmpty(javaSource))
            return [];

        var results = new List<ProtoFieldMapping>();
        var lines = javaSource.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            // Pattern 1: Field number constants
            // e.g., public static final int PHONE_NUMBER_FIELD_NUMBER = 1;
            foreach (Match match in FieldNumberConstantPattern().Matches(line))
            {
                var fieldName = match.Groups[1].Value;
                if (int.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var fieldNum))
                {
                    results.Add(new ProtoFieldMapping
                    {
                        Position = fieldNum - 1, // Proto field numbers are 1-based, array positions are 0-based
                        SuggestedName = ConvertFieldConstantToName(fieldName),
                        Source = "field_constant",
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        Confidence = ConfidenceLevel.High,
                    });
                }
            }

            // Pattern 2: Proto getter methods
            // e.g., public String getPhoneNumber() or public List getDevicesList()
            foreach (Match match in GetterPattern().Matches(line))
            {
                var getterName = match.Groups[1].Value;
                // Try to find the position from nearby .get(N) calls
                var nearbyPos = FindNearbyPosition(lines, lineIdx, 5);
                if (nearbyPos >= 0)
                {
                    results.Add(new ProtoFieldMapping
                    {
                        Position = nearbyPos,
                        SuggestedName = DecapitalizeName(getterName),
                        Source = "getter",
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        Confidence = ConfidenceLevel.High,
                    });
                }
            }

            // Pattern 3: Positional array access with variable assignment
            // e.g., String wsUrl = list.get(2);  or  this.phoneNumber = arr.get(0);
            foreach (Match match in PositionalAccessPattern().Matches(line))
            {
                var varName = match.Groups[1].Value;
                if (int.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var pos))
                {
                    // Skip single-letter obfuscated variable names unless we can get context
                    if (varName.Length > 1 || IsThisFieldAccess(line))
                    {
                        results.Add(new ProtoFieldMapping
                        {
                            Position = pos,
                            SuggestedName = varName.Length > 1 ? ToCamelCase(varName) : $"field{pos}",
                            Source = "position_access",
                            SourceFile = sourceFile,
                            LineNumber = lineIdx + 1,
                            Confidence = varName.Length > 2 ? ConfidenceLevel.Medium : ConfidenceLevel.Low,
                        });
                    }
                }
            }

            // Pattern 4: Proto descriptor/message name strings
            foreach (Match match in ProtoDescriptorPattern().Matches(line))
            {
                var descriptor = match.Groups[1].Value;
                results.Add(new ProtoFieldMapping
                {
                    Position = -1, // Descriptor applies to the whole message, not a specific field
                    SuggestedName = descriptor,
                    Source = "descriptor",
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Scan all Java files in a directory for proto field mappings.
    /// </summary>
    public static IReadOnlyList<ProtoFieldMapping> ExtractFromDirectory(string decompiledDir)
    {
        ArgumentNullException.ThrowIfNull(decompiledDir);
        if (!Directory.Exists(decompiledDir))
            return [];

        var allMappings = new List<ProtoFieldMapping>();
        foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
        {
#pragma warning disable CA1849 // File scanning loop — ReadAllTextAsync not practical here without extra complexity
            var source = File.ReadAllText(file);
#pragma warning restore CA1849

            // Quick filter: skip files without relevant patterns
            if (!source.Contains("FIELD_NUMBER", StringComparison.Ordinal) &&
                !source.Contains(".get(", StringComparison.Ordinal) &&
                !source.Contains(".proto", StringComparison.Ordinal))
                continue;

            var relativePath = Path.GetRelativePath(decompiledDir, file);
            allMappings.AddRange(Extract(source, relativePath));
        }

        // Deduplicate: if same position has multiple names, keep highest confidence
        return allMappings
            .GroupBy(m => m.Position)
            .SelectMany(g => g.OrderBy(m => m.Confidence).Take(1)) // Keep best per position
            .OrderBy(m => m.Position)
            .ToList();
    }

    private static int FindNearbyPosition(string[] lines, int currentLine, int range)
    {
        // Search forward first (method body is after the getter signature),
        // then backward. Return the closest match.
        var forwardEnd = Math.Min(lines.Length, currentLine + range + 1);
        for (var i = currentLine + 1; i < forwardEnd; i++)
        {
            var match = DotGetPositionPattern().Match(lines[i]);
            if (match.Success && int.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var pos))
            {
                return pos;
            }
        }

        var backwardStart = Math.Max(0, currentLine - range);
        for (var i = currentLine - 1; i >= backwardStart; i--)
        {
            var match = DotGetPositionPattern().Match(lines[i]);
            if (match.Success && int.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var pos))
            {
                return pos;
            }
        }

        return -1;
    }

    private static bool IsThisFieldAccess(string line)
    {
        return line.Contains("this.", StringComparison.Ordinal);
    }

#pragma warning disable CA1308 // ToLowerInvariant used for field name normalization (display), not locale-sensitive comparison
    private static string ConvertFieldConstantToName(string constant)
    {
        // PHONE_NUMBER_FIELD_NUMBER -> phoneNumber
        // Remove _FIELD_NUMBER suffix
        var name = constant;
        if (name.EndsWith("_FIELD_NUMBER", StringComparison.Ordinal))
            name = name[..^"_FIELD_NUMBER".Length];

        // Convert SCREAMING_SNAKE to camelCase
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return constant;

        return parts[0].ToLowerInvariant() + string.Concat(
            parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    private static string DecapitalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
#pragma warning restore CA1308

    // Proto field number constants: static final int NAME_FIELD_NUMBER = N;
    [GeneratedRegex(@"static\s+final\s+int\s+(\w+_FIELD_NUMBER)\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex FieldNumberConstantPattern();

    // Proto getter methods: public Type getFieldName()
    [GeneratedRegex(@"public\s+\S+\s+get(\w{3,})\(\)")]
    private static partial Regex GetterPattern();

    // Positional access with assignment: Type varName = expr.get(N)
    [GeneratedRegex(@"(\w+)\s*=\s*\w+\.get\((\d+)\)")]
    private static partial Regex PositionalAccessPattern();

    // .get(N) for position detection
    [GeneratedRegex(@"\.get\((\d+)\)")]
    private static partial Regex DotGetPositionPattern();

    // Proto descriptor strings
    [GeneratedRegex(@"""([\w.]+\.proto|google\.\w+\.v\d+\.\w+)""")]
    private static partial Regex ProtoDescriptorPattern();
}
