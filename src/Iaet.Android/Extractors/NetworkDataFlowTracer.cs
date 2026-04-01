using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

/// <summary>
/// Represents a traced network data flow from response handler through parsing to UI.
/// </summary>
public sealed record NetworkDataFlow
{
    public required string SourceType { get; init; }  // "gRPC", "Retrofit", "OkHttp", "HTTP"
    public required string SourceFile { get; init; }
    public string? ResponseHandler { get; init; }
    public string? ParsingDescription { get; init; }
    public string? TargetVariable { get; init; }
    public string? UiBinding { get; init; }
    public string? InferredPurpose { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
}

/// <summary>
/// Traces network response data through parsing code to UI components.
/// Works with gRPC callbacks, Retrofit response handlers, OkHttp interceptors.
/// Obfuscation-aware: matches by framework types, not variable names.
/// </summary>
public static partial class NetworkDataFlowTracer
{
    public static IReadOnlyList<NetworkDataFlow> Trace(string javaSource, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        if (string.IsNullOrEmpty(javaSource))
            return [];

        var results = new List<NetworkDataFlow>();

        // gRPC StreamObserver.onNext callbacks
        foreach (Match match in GrpcOnNextPattern().Matches(javaSource))
        {
            results.Add(BuildFlow("gRPC", sourceFile, match, javaSource));
        }

        // Retrofit Call.enqueue / onResponse callbacks
        foreach (Match match in RetrofitOnResponsePattern().Matches(javaSource))
        {
            results.Add(BuildFlow("Retrofit", sourceFile, match, javaSource));
        }

        // OkHttp Callback.onResponse
        foreach (Match match in OkHttpOnResponsePattern().Matches(javaSource))
        {
            results.Add(BuildFlow("OkHttp", sourceFile, match, javaSource));
        }

        // Cronet UrlRequest.Callback (used by Google apps instead of OkHttp)
        foreach (Match match in CronetOnResponseStartedPattern().Matches(javaSource))
        {
            results.Add(BuildFlow("Cronet", sourceFile, match, javaSource));
        }
        foreach (Match match in CronetOnReadCompletedPattern().Matches(javaSource))
        {
            if (!results.Exists(r => r.SourceFile == sourceFile && r.SourceType == "Cronet"))
            {
                results.Add(BuildFlow("Cronet", sourceFile, match, javaSource));
            }
        }

        // Direct Response.body() parsing
        foreach (Match match in ResponseBodyPattern().Matches(javaSource))
        {
            if (!results.Exists(r => r.SourceFile == sourceFile && r.SourceType == "OkHttp"))
            {
                results.Add(BuildFlow("HTTP", sourceFile, match, javaSource));
            }
        }

        return results;
    }

    public static IReadOnlyList<NetworkDataFlow> TraceFromDirectory(string decompiledDir)
    {
        ArgumentNullException.ThrowIfNull(decompiledDir);
        if (!Directory.Exists(decompiledDir))
            return [];

        var allFlows = new List<NetworkDataFlow>();
        foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
        {
#pragma warning disable CA1849 // File scanning loop — ReadAllTextAsync not practical here without extra complexity
            var source = File.ReadAllText(file);
#pragma warning restore CA1849
            // Quick filter: skip files without network response patterns
            if (!source.Contains("onNext", StringComparison.Ordinal) &&
                !source.Contains("onResponse", StringComparison.Ordinal) &&
                !source.Contains("onResponseStarted", StringComparison.Ordinal) &&
                !source.Contains("onReadCompleted", StringComparison.Ordinal) &&
                !source.Contains("Response.body", StringComparison.Ordinal) &&
                !source.Contains("response.body", StringComparison.Ordinal))
                continue;

            var relativePath = Path.GetRelativePath(decompiledDir, file);
            allFlows.AddRange(Trace(source, relativePath));
        }

        return allFlows;
    }

    private static NetworkDataFlow BuildFlow(string sourceType, string sourceFile, Match match, string fullSource)
    {
        // Find parsing patterns near the match
        var context = GetSurroundingLines(fullSource, match.Index, 20);

        var parsing = new List<string>();
        foreach (Match p in JsonParsePattern().Matches(context))
            parsing.Add(p.Value);
        foreach (Match p in ProtoParsePattern().Matches(context))
            parsing.Add(p.Value);
        foreach (Match p in GetterPattern().Matches(context))
            parsing.Add(p.Value);

        // Find UI updates near the match
        var uiBindings = new List<string>();
        foreach (Match u in LiveDataPattern().Matches(context))
            uiBindings.Add($"{u.Groups[1].Value}.postValue()");
        foreach (Match u in SetTextPatternNet().Matches(context))
            uiBindings.Add($"{u.Groups[1].Value}.setText()");
        foreach (Match u in NotifyPattern().Matches(context))
            uiBindings.Add("notifyDataSetChanged()");

        var handlerText = match.Value.Trim();

        return new NetworkDataFlow
        {
            SourceType = sourceType,
            SourceFile = sourceFile,
            ResponseHandler = handlerText.Length > 80
                ? handlerText[..80]
                : handlerText,
            ParsingDescription = parsing.Count > 0
                ? string.Join("; ", parsing.Distinct().Take(5))
                : null,
            TargetVariable = uiBindings.Count > 0 ? uiBindings[0].Split('.')[0] : null,
            UiBinding = uiBindings.Count > 0 ? string.Join(", ", uiBindings) : null,
            InferredPurpose = InferPurpose(sourceFile, context),
            Confidence = DetermineConfidence(parsing.Count, uiBindings.Count),
        };
    }

    private static string GetSurroundingLines(string source, int index, int lineCount)
    {
        var start = index;
        for (var i = 0; i < lineCount && start > 0; i++)
        {
            start = source.LastIndexOf('\n', start - 1);
            if (start < 0) { start = 0; break; }
        }

        var end = index;
        for (var i = 0; i < lineCount && end < source.Length; i++)
        {
            end = source.IndexOf('\n', end + 1);
            if (end < 0) { end = source.Length; break; }
        }

        return source[start..end];
    }

    private static string? InferPurpose(string sourceFile, string context)
    {
        var upper = (sourceFile + context).ToUpperInvariant();
        if (upper.Contains("MESSAGE", StringComparison.Ordinal) ||
            upper.Contains("THREAD", StringComparison.Ordinal) ||
            upper.Contains("SMS", StringComparison.Ordinal))
            return "messaging/SMS data";
        if (upper.Contains("CALL", StringComparison.Ordinal) ||
            upper.Contains("VOIP", StringComparison.Ordinal) ||
            upper.Contains("TELEPHON", StringComparison.Ordinal))
            return "call/VoIP data";
        if (upper.Contains("CONTACT", StringComparison.Ordinal) ||
            upper.Contains("PEOPLE", StringComparison.Ordinal))
            return "contacts data";
        if (upper.Contains("ACCOUNT", StringComparison.Ordinal) ||
            upper.Contains("PROFILE", StringComparison.Ordinal) ||
            upper.Contains("SETTINGS", StringComparison.Ordinal))
            return "account/settings data";
        if (upper.Contains("NOTIFICATION", StringComparison.Ordinal) ||
            upper.Contains("ALERT", StringComparison.Ordinal))
            return "notification data";
        return null;
    }

    private static ConfidenceLevel DetermineConfidence(int parseCount, int uiCount)
    {
        if (parseCount > 0 && uiCount > 0) return ConfidenceLevel.High;
        if (parseCount > 0 || uiCount > 0) return ConfidenceLevel.Medium;
        return ConfidenceLevel.Low;
    }

    [GeneratedRegex(@"onNext\s*\([^)]*\)\s*\{", RegexOptions.Multiline)]
    private static partial Regex GrpcOnNextPattern();

    [GeneratedRegex(@"onResponse\s*\([^)]*Response[^)]*\)\s*\{", RegexOptions.Multiline)]
    private static partial Regex RetrofitOnResponsePattern();

    [GeneratedRegex(@"onResponse\s*\([^)]*Call[^)]*,\s*Response[^)]*\)\s*\{", RegexOptions.Multiline)]
    private static partial Regex OkHttpOnResponsePattern();

    [GeneratedRegex(@"(?:response|Response)\.body\(\)")]
    private static partial Regex ResponseBodyPattern();

    [GeneratedRegex(@"JsonParser|JsonReader|JSONObject|JSONArray|Gson\.fromJson|parseFrom\(")]
    private static partial Regex JsonParsePattern();

    [GeneratedRegex(@"parseFrom\(|mergeFrom\(|writeTo\(|toByteArray\(")]
    private static partial Regex ProtoParsePattern();

    [GeneratedRegex(@"\.get[A-Z]\w*\(\)")]
    private static partial Regex GetterPattern();

    [GeneratedRegex(@"(\w+)\.postValue\(")]
    private static partial Regex LiveDataPattern();

    [GeneratedRegex(@"(\w+)\.setText\(")]
    private static partial Regex SetTextPatternNet();

    [GeneratedRegex(@"notifyDataSetChanged\(\)")]
    private static partial Regex NotifyPattern();

    [GeneratedRegex(@"onResponseStarted\s*\([^)]*(?:UrlRequest|urlRequest)[^)]*,\s*[^)]*(?:UrlResponseInfo|urlResponseInfo)[^)]*\)\s*\{", RegexOptions.Multiline)]
    private static partial Regex CronetOnResponseStartedPattern();

    [GeneratedRegex(@"onReadCompleted\s*\([^)]*(?:UrlRequest|urlRequest)[^)]*,\s*[^)]*(?:UrlResponseInfo|urlResponseInfo)[^)]*,\s*[^)]*(?:ByteBuffer|byteBuffer)[^)]*\)\s*\{", RegexOptions.Multiline)]
    private static partial Regex CronetOnReadCompletedPattern();
}
