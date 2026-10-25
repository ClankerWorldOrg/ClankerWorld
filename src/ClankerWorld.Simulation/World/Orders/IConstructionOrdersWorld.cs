namespace ClankerWorld.Simulation.World;

internal interface IConstructionOrdersWorld
{
    string ConstructionInstanceId(string instructionId);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool IsAdult(string actor);
    bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    bool OrdinaryProjectCanYieldToOrder(global::ClankerWorld.Simulation.World.AgentProject project);
    string? HouseholdBuildingKind(global::ClankerWorld.Simulation.Content.BuildingDefinition definition);
    bool TownHasRoomForSharedShop(string actor, string? kind);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    bool WorkBuildingHasInputs(string actor, string household, global::ClankerWorld.Simulation.Content.BuildingDefinition building);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null);
    bool ReadyForBriefInteraction(string actor);
    bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project);
    void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state);
    bool IsActiveOrder(string status);
    void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string ConstructionOrderReceipt(string instanceId);
    void MarkCheckpointCurrent();
    void RecordConstructionReceipt(WorldConstructionReceipt receipt);
}
