using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IInvestigationAgent
{
    string AgentName { get; }
    Task<AgentFindings> ExecuteAsync(AgentDispatch task, ProjectConfig project, CancellationToken ct = default);
}
