using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record KnowledgeStorageActionPlan(string ActorId, AgentKnowledgeArtifact Artifact, bool Collect,
    string OwnerId, string? HouseholdId, string? HouseId, string? CarrierId, string? StorageBuildingId,
    InventoryGroundPosition? GroundPosition, GridPoint Target, int Range, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, new KnowledgeStorageChoice(Artifact.Id, Collect).Encode())
{
    public override string Activity => "move that selected knowledge artifact";
}

public sealed record KnowledgeStorageChoice(string ArtifactId, bool Collect)
{
    public string Encode() => IdentityRules.PrefixedReference(Collect ? "knowledge_collect:" : "knowledge_store:", ArtifactId);
    public static KnowledgeStorageChoice? Decode(string candidate) => candidate.StartsWith("knowledge_collect:", StringComparison.Ordinal)
        ? new(candidate["knowledge_collect:".Length..], true)
        : candidate.StartsWith("knowledge_store:", StringComparison.Ordinal) ? new(candidate["knowledge_store:".Length..], false) : null;
}
