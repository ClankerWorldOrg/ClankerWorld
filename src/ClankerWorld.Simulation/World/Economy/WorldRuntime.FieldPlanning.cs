using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string FarmCandidate(FarmWorkKind kind, GridPoint point, string? crop = null) =>
        FarmingSystem.FarmCandidate(kind, point, crop);

    private void AddFieldCandidates(List<CognitionCandidate> candidates, string actor, AgentState state) =>
        farmingSystem.AddFieldCandidates(farmingWorld, candidates, actor, state);

    private IEnumerable<GridPoint> NearbyFarmTiles(GridPoint origin) =>
        FarmingSystem.NearbyFarmTiles(farmingWorld, origin);

    private bool HasOtherAgentClaimedPlanting(FarmFieldState field, string actor) =>
        farmingSystem.HasOtherAgentClaimedPlanting(farmingWorld, field, actor);

    private bool WantsFieldPlantingStock(string actor, string itemKind) =>
        farmingSystem.WantsFieldPlantingStock(farmingWorld, actor, itemKind);

    private InventoryLot? PlantingStock(string actor, FarmFieldState field, string crop, bool requirePickupRoute = false) =>
        FarmingSystem.PlantingStock(farmingWorld, actor, field, crop, requirePickupRoute);

    private void ApplyFieldCandidate(string actor, AgentState state, string candidate, string? orderInstructionId = null) =>
        farmingSystem.ApplyFieldCandidate(farmingWorld, actor, state, candidate, orderInstructionId);


}
