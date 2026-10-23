namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMaterialOrdersWorld MaterialOrdersWorld => new MaterialOrdersAdapter(this);

    private sealed class MaterialOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IMaterialOrdersWorld
    {
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.MapResource> KnownMaterialOrderSources(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.KnownMaterialOrderSources(Runtime.MaterialOrdersWorld, instruction, person);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache) => Runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => Runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public global::ClankerWorld.Simulation.World.GridPoint? MaterialOrderTarget(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MaterialOrderTarget(Runtime.MaterialOrdersWorld, instruction);
        public string MaterialGatherReceipt(string lot) => WorldRuntime.MaterialGatherReceipt(lot);
    }
}
