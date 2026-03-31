using Iaet.Core.Models;

namespace Iaet.Agents;

public static class FindingsMerger
{
    public static IReadOnlyList<DiscoveredEndpoint> Merge(IReadOnlyList<AgentFindings> allFindings)
    {
        ArgumentNullException.ThrowIfNull(allFindings);
        var bySignature = new Dictionary<string, MergeState>(StringComparer.Ordinal);

        foreach (var findings in allFindings)
        {
            foreach (var ep in findings.Endpoints)
            {
                if (!bySignature.TryGetValue(ep.Signature, out var state))
                {
                    state = new MergeState(ep.Signature);
                    bySignature[ep.Signature] = state;
                }

                state.ObservationCount += ep.ObservationCount;
                state.Sources.AddRange(ep.Sources);
                state.Limitations.AddRange(ep.Limitations);

                // ConfidenceLevel: High=0, Medium=1, Low=2 — lower numeric value is better
                if (ep.Confidence < state.BestConfidence)
                    state.BestConfidence = ep.Confidence;
            }
        }

        return bySignature.Values
            .Select(s => new DiscoveredEndpoint
            {
                Signature = s.Signature,
                Confidence = s.BestConfidence,
                ObservationCount = s.ObservationCount,
                Sources = s.Sources.Distinct(StringComparer.Ordinal).ToList(),
                Limitations = s.Limitations.Distinct(StringComparer.Ordinal).ToList(),
            })
            .ToList();
    }

    private sealed class MergeState(string signature)
    {
        public string Signature { get; } = signature;
        public ConfidenceLevel BestConfidence { get; set; } = ConfidenceLevel.Low;
        public int ObservationCount { get; set; }
        public List<string> Sources { get; } = [];
        public List<string> Limitations { get; } = [];
    }
}
