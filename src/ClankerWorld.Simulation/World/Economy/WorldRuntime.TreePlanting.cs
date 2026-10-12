using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string OrchardReplantingReservationId(string lotId) =>
        FarmingSystem.OrchardReplantingReservationId(lotId);

    private int PlantingSeedQuantity(InventoryLot lot) =>
        FarmingSystem.PlantingSeedQuantity(farmingWorld, lot);

    private InventoryLot? CarriedPlantingSeed(string actor, string kind) =>
        FarmingSystem.CarriedPlantingSeed(farmingWorld, actor, kind);

    private static bool IsPlantedTree(MapResource resource) =>
        FarmingSystem.IsPlantedTree(resource);

    private void AddTreePlantingCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState state) =>
        FarmingSystem.AddTreePlantingCandidates(farmingWorld, candidates, actor, state);

    private MapResource? ReplantableTree(string actor, GridPoint origin) =>
        FarmingSystem.ReplantableTree(farmingWorld, actor, origin);

    private void ReplantTree(string actor, AgentState state) =>
        FarmingSystem.ReplantTree(farmingWorld, actor, state);

    public TreePlantingResult PlantTree(string planterId, string species, string seedLotId, GridPoint destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(species);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedLotId);
        gate.Wait();
        try
        {
            return PlantTreeCore(planterId.Trim(), species.Trim(), seedLotId.Trim(), destination);
        }
        finally
        {
            gate.Release();
        }
    }

    private TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, GridPoint destination) =>
        FarmingSystem.PlantTreeCore(farmingWorld, planterId, species, seedLotId, destination);

    private void PlantTreeNearby(string actor, AgentState state, bool orchard = false) =>
        FarmingSystem.PlantTreeNearby(farmingWorld, actor, state, orchard);

    private GridPoint? PlantingSite(string actor, GridPoint origin) =>
        FarmingSystem.PlantingSite(farmingWorld, actor, origin);

    private string PlantingSpecies(GridPoint site) =>
        FarmingSystem.PlantingSpecies(farmingWorld, site);

    private (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) PlantingObstacles() =>
        FarmingSystem.PlantingObstacles(farmingWorld);

    private TreePlantingRefusal? PlantingSiteRefusal(GridPoint point,
        (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) obstacles) =>
        FarmingSystem.PlantingSiteRefusal(farmingWorld, point, obstacles);

    private bool TreeAreaFull(GridPoint point) =>
        FarmingSystem.TreeAreaFull(farmingWorld, point);

    private void ValidatePlantedTrees() =>
        FarmingSystem.ValidatePlantedTrees(farmingWorld);

}
