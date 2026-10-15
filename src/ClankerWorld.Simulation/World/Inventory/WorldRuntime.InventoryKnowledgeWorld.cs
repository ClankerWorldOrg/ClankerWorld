using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryKnowledgeWorld InventoryKnowledgeWorld => new InventoryKnowledgeAdapter(this);
    private sealed class InventoryKnowledgeAdapter(WorldRuntime runtime) : IInventoryKnowledgeWorld
    {
        public long WorldTick => runtime.WorldTick;
        public string WritingMaterialTransferId(long tick, string actor, string kind) => WorldRuntime.WritingMaterialTransferId(tick, actor, kind);
    }
}
