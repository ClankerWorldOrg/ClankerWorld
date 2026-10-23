using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddParenthoodCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var start = candidates.Count;
        AddParenthoodCandidateOptions(candidates, actor);
        for (var index = start; index < candidates.Count;)
        {
            var candidate = candidates[index];
            if (ParenthoodChoice.Decode(candidate.Id) is not { } choice) { index++; continue; }
            if (ParenthoodDefinition.Offer(this, actor, choice) is { } plan)
            {
                candidates[index] = candidate with { ActionPlan = plan };
                index++;
            }
            else candidates.RemoveAt(index);
        }
    }

}
