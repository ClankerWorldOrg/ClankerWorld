using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMaterialOrdersWorld MaterialOrdersWorld => new MaterialOrdersAdapter(this);

    private sealed class MaterialOrdersAdapter(WorldRuntime runtime) : IMaterialOrdersWorld
    {
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public WorldMap Map => runtime.map;
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public IReadOnlyDictionary<string, ResourceState> Resources => runtime.resources;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public bool CanGatherFromSource(string actor, string itemKind, MapResource source, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache) => runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public bool CanExploreForOrder(AgentState person) => runtime.CanExploreForOrder(person);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool RecordKnowledgeFact(string actor, GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public string MaterialGatherReceipt(string lot) => WorldRuntime.MaterialGatherReceipt(lot);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
    }
}
