using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Agents;

public sealed class RoundExecutor(IReadOnlyList<IInvestigationAgent> agents)
{
    public async Task<IReadOnlyList<AgentFindings>> ExecuteRoundAsync(
        RoundPlan plan, ProjectConfig project, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(project);

        var agentMap = agents.ToDictionary(a => a.AgentName, StringComparer.OrdinalIgnoreCase);
        var tasks = new List<Task<AgentFindings>>();
        foreach (var dispatch in plan.Dispatches)
        {
            if (agentMap.TryGetValue(dispatch.Agent, out var agent))
                tasks.Add(agent.ExecuteAsync(dispatch, project, ct));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }
}
