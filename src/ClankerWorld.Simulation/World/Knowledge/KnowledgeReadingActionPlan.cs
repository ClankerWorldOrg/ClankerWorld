namespace ClankerWorld.Simulation.World;

public sealed record KnowledgeReadingActionPlan(string ActorId, AgentKnowledgeArtifact Artifact, string RecipientId, bool Share)
    : ActionPlan(ActorId, IdentityRules.PrefixedReference(Share ? "knowledge_share:" : "knowledge_read:",
        Share ? Artifact.Id + "|" + RecipientId : Artifact.Id))
{
    public override string Activity => Share ? "share that selected held record" : "read that selected held record";
}
