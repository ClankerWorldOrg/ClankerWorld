using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddLearningCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        CommunitySystem.AddLearningCandidateOptions(this.CommunityLearningWorld, options, actor);
        foreach (var candidate in options)
            if (LearningChoice.Decode(candidate.Id) is { } choice && global::ClankerWorld.Simulation.World.WorldRuntime.CommunityActions.Learning.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

}
