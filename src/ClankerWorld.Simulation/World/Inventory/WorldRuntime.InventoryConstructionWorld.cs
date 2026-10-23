using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static IInventoryConstructionWorld InventoryConstructionWorld => new InventoryConstructionAdapter();
    private sealed class InventoryConstructionAdapter : IInventoryConstructionWorld
    {
        public InventoryCheckpoint ReserveExpansionInputs(InventoryCheckpoint inventory, IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string jobId, long completion, out IReadOnlyList<string> reservationIds) => WorldRuntime.ReserveExpansionInputs(inventory, inputs, actor, jobId, completion, out reservationIds);
        public InventoryCheckpoint ConsumeConstructionInputs(InventoryCheckpoint inventory, IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner) => WorldRuntime.ConsumeConstructionInputs(inventory, inputs, purpose, owner);
    }
}
