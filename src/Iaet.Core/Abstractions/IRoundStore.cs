using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IRoundStore
{
    Task<int> CreateRoundAsync(string projectName, RoundPlan plan, CancellationToken ct = default);
    Task SaveFindingsAsync(string projectName, int roundNumber, AgentFindings findings, CancellationToken ct = default);
    Task<RoundPlan?> GetPlanAsync(string projectName, int roundNumber, CancellationToken ct = default);
    Task<IReadOnlyList<AgentFindings>> GetFindingsAsync(string projectName, int roundNumber, CancellationToken ct = default);
}
