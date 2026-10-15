using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IInventoryConstructionWorld
{

    InventoryCheckpoint ReserveExpansionInputs(InventoryCheckpoint inventory, IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string jobId, long completion, out IReadOnlyList<string> reservationIds);
    InventoryCheckpoint ConsumeConstructionInputs(InventoryCheckpoint inventory, IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner);
}
