using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static string OrchardReplantingReservationId(string lotId) =>
        FarmingSystem.OrchardReplantingReservationId(lotId);

    private int PlantingSeedQuantity(InventoryLot lot) =>
        FarmingSystem.PlantingSeedQuantity(farmingWorld, lot);

    private InventoryLot? CarriedPlantingSeed(string actor, string kind) =>
        FarmingSystem.CarriedPlantingSeed(farmingWorld, actor, kind);

    private static bool IsPlantedTree(MapResource resource) =>
        FarmingSystem.IsPlantedTree(resource);

    private void AddForestryCandidates(List<CognitionCandidate> candidates, string actor,
        PlaytestInhabitantState state) =>
        FarmingSystem.AddForestryCandidates(farmingWorld, candidates, actor, state);

    private MapResource? ReplantableTree(string actor, GridPoint origin) =>
        FarmingSystem.ReplantableTree(farmingWorld, actor, origin);

    private void ReplantTree(string actor, PlaytestInhabitantState state) =>
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

    private void PlantTreeNearby(string actor, PlaytestInhabitantState state, bool orchard = false) =>
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
