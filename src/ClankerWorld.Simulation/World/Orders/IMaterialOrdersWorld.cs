namespace ClankerWorld.Simulation.World;

internal interface IMaterialOrdersWorld
{
    int FreeCarryCapacity(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.MapResource> KnownMaterialOrderSources(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    int ResourceInteractionRange { get; }
    bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache);
    global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true);
    bool CanExploreForOrder(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.GridPoint? MaterialOrderTarget(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    string MaterialGatherReceipt(string lot);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
}
