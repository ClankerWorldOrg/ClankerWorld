namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckKnowledgeReading(KnowledgeReadingActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: plan.Share, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (!HeldKnowledgeArtifacts(plan.Actor).Any(artifact => artifact.Id == plan.Artifact.Id &&
                TownHearingProcedure.Digest(artifact) == TownHearingProcedure.Digest(plan.Artifact)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected held knowledge artifact or its contents have changed");
        if (!agents.ContainsKey(plan.RecipientId) || !IsAdult(plan.RecipientId) || !plan.Share && plan.RecipientId != plan.Actor ||
            !HasUnknownArtifactContents(plan.RecipientId, plan.Artifact))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient or their unknown contents have changed");
        return !plan.Share || IsWithinInteractionRange(agents[plan.Actor].Position, agents[plan.RecipientId].Position, 1)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected knowledge recipient is no longer nearby");
    }

    private void ApplyKnowledgeReading(KnowledgeReadingActionPlan plan)
    {
        if (CheckKnowledgeReading(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (!plan.Share) { ReadIfKnowledgeArtifact(plan.Artifact.LotId, plan.Artifact.CreatorId, plan.Actor, plan.Artifact); return; }
        var learned = LearnArtifactFacts(plan.RecipientId, plan.Actor, plan.Artifact, "shared") +
            LearnArtifactRecipes(plan.RecipientId, plan.Actor, plan.Artifact, "shared");
        if (learned > 0)
        {
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent("agent_knowledge_shared", $"{plan.Actor}|{plan.RecipientId}|{plan.Artifact.Id}|{learned}");
        }
    }
}
