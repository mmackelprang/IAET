using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public static class StateMachineBuilder
{
    public static StateMachineModel Build(string name, IReadOnlyList<string> messageSequence)
    {
        ArgumentNullException.ThrowIfNull(messageSequence);

        if (messageSequence.Count == 0)
            return new StateMachineModel { Name = name, States = [], Transitions = [], InitialState = string.Empty };

        var states = BuildOrderedUniqueList(messageSequence);
        var transitions = new List<StateTransition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < messageSequence.Count - 1; i++)
        {
            var key = $"{messageSequence[i]}→{messageSequence[i + 1]}";
            if (seen.Add(key))
            {
                transitions.Add(new StateTransition
                {
                    From = messageSequence[i],
                    To = messageSequence[i + 1],
                    Trigger = messageSequence[i + 1],
                });
            }
        }

        return new StateMachineModel
        {
            Name = name,
            States = states,
            Transitions = transitions,
            InitialState = messageSequence[0],
        };
    }

    private static List<string> BuildOrderedUniqueList(IReadOnlyList<string> source)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (var item in source)
        {
            if (seen.Add(item))
                list.Add(item);
        }
        return list;
    }
}
