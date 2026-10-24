using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddFamilyCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        this.familySystem.AddFamilyCandidateOptions(this.FamilyPartnershipWorld, options, actor);
        foreach (var candidate in options)
            if (PartnershipChoice.Decode(candidate.Id) is { } choice && global::ClankerWorld.Simulation.World.WorldRuntime.FamilyActions.Partnership.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

}
