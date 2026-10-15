using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMaterialOrdersWorld
{
    int ResourceInteractionRange { get; }
    WorldMap Map { get; }
    WorldKnowledgeState Knowledge { get; }
    IReadOnlyDictionary<string, ResourceState> Resources { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    int FreeCarryCapacity(string actor);
    bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    bool CanGatherFromSource(string actor, string itemKind, MapResource source, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache);
    ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, MapResource source, bool useHarvestBonus = true);
    bool CanExploreForOrder(AgentState person);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool RecordKnowledgeFact(string actor, GridPoint position);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0);
    string MaterialGatherReceipt(string lot);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
}
