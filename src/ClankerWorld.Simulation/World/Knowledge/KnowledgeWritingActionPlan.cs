namespace ClankerWorld.Simulation.World;

public sealed record PlannedWritingMaterial(string LotId, string ItemKind, int Quantity);
public sealed record PlannedWritingPickup(string LotId, string OwnerId, string ItemKind, int Quantity, GridPoint Position, int Range);

public sealed record KnowledgeWritingActionPlan(string ActorId, string SelectedCandidateId, string Kind,
    AgentKnowledgeArtifact? Source, IReadOnlyList<AgentKnowledgeFact> Facts, IReadOnlyList<AgentRecipeKnowledge> Recipes,
    IReadOnlyList<PlannedWritingMaterial> Materials, PlannedWritingPickup? Pickup, AgentKnowledgeWritingProject? Project)
    : ActionPlan(ActorId, SelectedCandidateId)
{
    public override string Activity => "write that selected knowledge record";
}
