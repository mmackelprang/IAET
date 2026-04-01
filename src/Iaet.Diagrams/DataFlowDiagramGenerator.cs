namespace Iaet.Diagrams;

using System.Globalization;
using System.Text;
using Iaet.Android.Extractors;
using Iaet.Core.Models;

/// <summary>
/// Generates Mermaid flowchart diagrams from traced network and BLE data flows.
/// Shows the path from data source (API/BLE) through parsing to UI binding.
/// </summary>
public static class DataFlowDiagramGenerator
{
    public static string GenerateFromNetworkFlows(string title, IReadOnlyList<NetworkDataFlow> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% {title}");

        if (flows.Count == 0)
        {
            sb.AppendLine("    NoData[No data flows traced]");
            return sb.ToString();
        }

        var nodeId = 0;
        foreach (var flow in flows)
        {
            var prefix = $"F{nodeId++}";
            var sourceLabel = $"{flow.SourceType} Response";
            var fileLabel = flow.SourceFile.Length > 30
                ? "..." + flow.SourceFile[^30..]
                : flow.SourceFile;

            sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_src[/{sourceLabel}\\]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_file[{fileLabel}]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_src --> {prefix}_file");

            if (flow.ParsingDescription is not null)
            {
                var parseLabel = flow.ParsingDescription.Length > 40
                    ? flow.ParsingDescription[..40] + "..."
                    : flow.ParsingDescription;
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_parse[{parseLabel}]");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_file --> {prefix}_parse");

                if (flow.UiBinding is not null)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_ui([{flow.UiBinding}])");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_parse --> {prefix}_ui");
                }
            }
            else if (flow.UiBinding is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_ui([{flow.UiBinding}])");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_file --> {prefix}_ui");
            }

            if (flow.InferredPurpose is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    %% Purpose: {flow.InferredPurpose} (confidence: {flow.Confidence})");
            }
        }

        return sb.ToString();
    }

    public static string GenerateFromBleFlows(string title, IReadOnlyList<BleDataFlow> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% {title}");

        if (flows.Count == 0)
        {
            sb.AppendLine("    NoData[No BLE data flows traced]");
            return sb.ToString();
        }

        var nodeId = 0;
        foreach (var flow in flows)
        {
            var prefix = $"B{nodeId++}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_char[/BLE Characteristic\\]");

            if (flow.CallbackLocation is not null)
            {
                var cbLabel = flow.CallbackLocation.Length > 40
                    ? "..." + flow.CallbackLocation[^40..]
                    : flow.CallbackLocation;
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_cb[{cbLabel}]");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_char --> {prefix}_cb");
            }

            if (flow.ParsingDescription is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_parse[{flow.ParsingDescription}]");
                var prevNode = flow.CallbackLocation is not null ? $"{prefix}_cb" : $"{prefix}_char";
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prevNode} --> {prefix}_parse");
            }

            if (flow.UiBinding is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prefix}_ui([{flow.UiBinding}])");
                var prevNode = flow.ParsingDescription is not null ? $"{prefix}_parse" :
                    flow.CallbackLocation is not null ? $"{prefix}_cb" : $"{prefix}_char";
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {prevNode} --> {prefix}_ui");
            }

            if (flow.InferredMeaning is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    %% Inferred: {flow.InferredMeaning} (confidence: {flow.Confidence})");
            }
        }

        return sb.ToString();
    }
}
