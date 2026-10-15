using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderSelectionWorld
{
    WorldContentSimulationState Simulation { get; }
    WorldMap Map { get; }
    int ComfortableFullness { get; }
    int ResourceInteractionRange { get; }
    IReadOnlyDictionary<string, ResourceState> Resources { get; }
    WorldKnowledgeState Knowledge { get; }
    int OutingFullnessReserve { get; }
    bool IsActiveOrder(string status);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeReadOrderCandidate(OwnerQueuedInstruction instruction);
    bool IsConversationOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? TalkOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    bool IsCartOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? CartOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    bool IsAnimalOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? AnimalOrderCandidate(OwnerQueuedInstruction instruction);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? BoatOrderCandidate(OwnerQueuedInstruction instruction);
    bool IsTreePlantingOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? TreePlantingOrderCandidate(OwnerQueuedInstruction instruction, AgentState person);
    bool IsKnowledgeOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(OwnerQueuedInstruction instruction);
    bool IsShelterOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? ShelterOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? ConstructionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? DeliveryOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    bool IsFieldOrder(string action);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? FieldOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(OwnerQueuedInstruction instruction);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    bool MovementOrderNeedsHouseInvitation(string actor, GridPoint destination);
    bool CanSwim(string actor, GridPoint? origin = null, int additionalCarriedUnits = 0);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianOrderCandidate(string adult, string child);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> PreferredFood(string owner, string? actor = null);
    ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null);
    int FreeCarryCapacity(string actor);
    int FoodHarvestCarryUnits(MapResource source);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    string FoodArrivalReceipt(string resourceId, GridPoint position);
    string FoodKnowledgeKind(MapResource resource);
    bool CanReachTerrain(string actor, GridPoint target, ReachPurpose purpose, int load, SwimmingPolicy swimming);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    bool HasWarmthForOuting(AgentState person);
}
