using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

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
