using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private LearningActionPlan? PlanLearningAction(string actor, LearningChoice choice, bool waitingForModel = false) =>
        CommunitySystem.PlanLearningAction(CommunityLearningWorld, actor, choice, waitingForModel);
    private ActionCheck CheckLearningActionPlan(LearningActionPlan plan, ActorCapabilityPolicy? policy = null) => CommunitySystem.CheckLearningActionPlan(CommunityLearningWorld, plan, policy);

    private void AddLearningCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddLearningCandidateOptions(options, actor);
        foreach (var candidate in options)
            if (LearningChoice.Decode(candidate.Id) is { } choice && LearningDefinition.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

}
