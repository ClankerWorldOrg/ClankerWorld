using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string OrchardReplantingReservationId(string lotId) =>
        FarmingSystem.OrchardReplantingReservationId(lotId);

    private int PlantingSeedQuantity(InventoryLot lot) =>
        TreePlantingGoodsQuantity(InventoryIndex.For(society.Checkpoint.Inventory), lot);


    public TreePlantingResult PlantTree(string planterId, string species, string seedLotId, GridPoint destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(species);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedLotId);
        gate.Wait();
        try
        {
            return FarmingSystem.PlantTreeCore(this.farmingWorld, (planterId.Trim()), (species.Trim()), (seedLotId.Trim()), destination);
        }
        finally
        {
            gate.Release();
        }
    }

    private (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) PlantingObstacles() =>
        FarmingSystem.PlantingObstacles(farmingWorld);

}
