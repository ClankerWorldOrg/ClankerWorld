using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record KnowledgeStorageActionPlan(string ActorId, AgentKnowledgeArtifact Artifact, bool Collect,
    string OwnerId, string? HouseholdId, string? HouseId, string? CarrierId, string? StorageBuildingId,
    InventoryGroundPosition? GroundPosition, GridPoint Target, int Range, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, (Collect ? "knowledge_collect:" : "knowledge_store:") + Artifact.Id)
{
    public override string Activity => "move that selected knowledge artifact";
}
