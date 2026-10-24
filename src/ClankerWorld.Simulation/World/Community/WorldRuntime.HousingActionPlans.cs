using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddHousingCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddHousingCandidateOptions(options, actor);
        foreach (var candidate in options)
        {
            if (HousingChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (HousingDefinition.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

}
