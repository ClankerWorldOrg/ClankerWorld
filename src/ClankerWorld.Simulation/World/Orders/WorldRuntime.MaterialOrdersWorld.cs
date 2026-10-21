namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMaterialOrdersWorld MaterialOrdersWorld => new MaterialOrdersAdapter(this);

    private sealed class MaterialOrdersAdapter(WorldRuntime runtime) : IMaterialOrdersWorld
    {
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.MapResource> KnownMaterialOrderSources(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.KnownMaterialOrderSources(runtime.MaterialOrdersWorld, instruction, person);
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache) => runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public bool CanExploreForOrder(global::ClankerWorld.Simulation.World.AgentState person) => runtime.CanExploreForOrder(person);
        public global::ClankerWorld.Simulation.World.GridPoint? MaterialOrderTarget(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MaterialOrderTarget(runtime.MaterialOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources => runtime.resources;
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderCandidateFor(runtime.MaterialOrdersWorld, instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderBlockedReason(runtime.MaterialOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public string MaterialGatherReceipt(string lot) => WorldRuntime.MaterialGatherReceipt(lot);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
    }
}
