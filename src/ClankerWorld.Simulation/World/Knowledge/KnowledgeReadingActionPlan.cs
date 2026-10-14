namespace ClankerWorld.Simulation.World;

public sealed record KnowledgeReadingActionPlan(string ActorId, AgentKnowledgeArtifact Artifact, string RecipientId, bool Share)
    : ActionPlan(ActorId, Share ? "knowledge_share:" + Artifact.Id + "|" + RecipientId : "knowledge_read:" + Artifact.Id)
{
    public override string Activity => Share ? "share that selected held record" : "read that selected held record";
}
