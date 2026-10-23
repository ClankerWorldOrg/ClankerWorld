namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMaterialOrdersWorld MaterialOrdersWorld => new MaterialOrdersAdapter(this);

    private sealed class MaterialOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IMaterialOrdersWorld
    {
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.MapResource> KnownMaterialOrderSources(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.KnownMaterialOrderSources(Runtime.MaterialOrdersWorld, instruction, person);
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => Runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache) => Runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => Runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public global::ClankerWorld.Simulation.World.GridPoint? MaterialOrderTarget(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MaterialOrderTarget(Runtime.MaterialOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => Runtime.knowledge;
        public global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources => Runtime.resources;
        public bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => Runtime.RecordKnowledgeFact(actor, position);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderCandidateFor(Runtime.MaterialOrdersWorld, instruction, person);
        public string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderBlockedReason(Runtime.MaterialOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => Runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public string MaterialGatherReceipt(string lot) => WorldRuntime.MaterialGatherReceipt(lot);
    }
}
