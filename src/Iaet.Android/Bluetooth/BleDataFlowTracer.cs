using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Android.Bluetooth;

/// <summary>
/// Traces data from BLE characteristic callbacks through parsing code to UI components.
/// Obfuscation-aware: matches by Android SDK types, not variable names.
/// Reports confidence levels since regex/heuristic analysis of decompiled source is imperfect.
/// </summary>
public static partial class BleDataFlowTracer
{
    public static IReadOnlyList<BleDataFlow> Trace(string javaSource, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        if (string.IsNullOrEmpty(javaSource))
            return [];

        var results = new List<BleDataFlow>();

        // Stage 1: Find onCharacteristicChanged callbacks
        var callbackMatches = CharacteristicChangedPattern().Matches(javaSource);
        if (callbackMatches.Count == 0)
            return [];

        // Stage 2: Find getValue() / ByteBuffer parsing near callbacks
        var parsePatterns = new List<string>();
        foreach (Match match in GetValuePattern().Matches(javaSource))
            parsePatterns.Add(match.Value);
        foreach (Match match in ByteBufferPattern().Matches(javaSource))
            parsePatterns.Add(match.Value);
        foreach (Match match in ArrayIndexPattern().Matches(javaSource))
            parsePatterns.Add(match.Value);

        // Stage 3: Find field assignments (LiveData, postValue, setValue, setText)
        var assignments = new List<(string Variable, string Pattern)>();
        foreach (Match match in LiveDataPostPattern().Matches(javaSource))
            assignments.Add((match.Groups[1].Value, "LiveData.postValue"));
        foreach (Match match in SetTextPattern().Matches(javaSource))
            assignments.Add((match.Groups[1].Value, "setText"));
        foreach (Match match in FieldAssignPattern().Matches(javaSource))
            assignments.Add((match.Groups[1].Value, "field assignment"));

        // Stage 4: Build data flow records
        // Each callback gets a flow entry with whatever parsing/UI we can find in the same file
        foreach (Match callback in callbackMatches)
        {
            var flow = new BleDataFlow
            {
                CharacteristicUuid = "unknown",  // UUID association requires cross-file analysis
                CallbackLocation = $"{sourceFile}:{callback.Index}",
                ParsingDescription = parsePatterns.Count > 0
                    ? string.Join("; ", parsePatterns.Take(3))
                    : null,
                VariableName = assignments.Count > 0
                    ? assignments[0].Variable
                    : null,
                UiBinding = assignments.Count > 0
                    ? $"{assignments[0].Variable} via {assignments[0].Pattern}"
                    : null,
                InferredMeaning = InferMeaning(sourceFile, parsePatterns, assignments),
                Confidence = DetermineConfidence(parsePatterns.Count, assignments.Count),
            };
            results.Add(flow);
        }

        return results;
    }

    public static IReadOnlyList<BleDataFlow> TraceFromDirectory(string decompiledDir)
    {
        ArgumentNullException.ThrowIfNull(decompiledDir);
        if (!Directory.Exists(decompiledDir))
            return [];

        var allFlows = new List<BleDataFlow>();
        foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
        {
#pragma warning disable CA1849 // File scanning loop — ReadAllTextAsync not practical here without extra complexity
            var source = File.ReadAllText(file);
#pragma warning restore CA1849
            if (!source.Contains("onCharacteristicChanged", StringComparison.Ordinal))
                continue;
            var relativePath = Path.GetRelativePath(decompiledDir, file);
            allFlows.AddRange(Trace(source, relativePath));
        }

        return allFlows;
    }

    private static string? InferMeaning(
        string sourceFile,
        List<string> parsePatterns,
        List<(string Variable, string Pattern)> assignments)
    {
        // Heuristic: infer from file name, variable names, and parse patterns
        var hints = new List<string>();
        var upperFile = sourceFile.ToUpperInvariant();

        if (upperFile.Contains("HEART", StringComparison.Ordinal) || upperFile.Contains("HR", StringComparison.Ordinal))
            hints.Add("heart rate data");
        if (upperFile.Contains("TEMP", StringComparison.Ordinal))
            hints.Add("temperature data");
        if (upperFile.Contains("BATTERY", StringComparison.Ordinal))
            hints.Add("battery level");
        if (upperFile.Contains("SENSOR", StringComparison.Ordinal))
            hints.Add("sensor data");

        if (parsePatterns.Exists(p => p.Contains("getFloat", StringComparison.OrdinalIgnoreCase)))
            hints.Add("floating-point value");
        if (parsePatterns.Exists(p =>
            p.Contains("getShort", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("getInt", StringComparison.OrdinalIgnoreCase)))
            hints.Add("integer value");

        return hints.Count > 0 ? string.Join(", ", hints) : null;
    }

    private static ConfidenceLevel DetermineConfidence(int parseCount, int assignmentCount)
    {
        if (parseCount > 0 && assignmentCount > 0) return ConfidenceLevel.High;
        if (parseCount > 0 || assignmentCount > 0) return ConfidenceLevel.Medium;
        return ConfidenceLevel.Low;
    }

    [GeneratedRegex(@"onCharacteristicChanged\s*\(")]
    private static partial Regex CharacteristicChangedPattern();

    [GeneratedRegex(@"\.getValue\(\)")]
    private static partial Regex GetValuePattern();

    [GeneratedRegex(@"ByteBuffer\.\w+\(")]
    private static partial Regex ByteBufferPattern();

    [GeneratedRegex(@"value\[\d+\]")]
    private static partial Regex ArrayIndexPattern();

    [GeneratedRegex(@"(\w+)\.postValue\(")]
    private static partial Regex LiveDataPostPattern();

    [GeneratedRegex(@"(\w+)\.setText\(")]
    private static partial Regex SetTextPattern();

    [GeneratedRegex(@"this\.(\w+)\s*=\s*")]
    private static partial Regex FieldAssignPattern();
}
