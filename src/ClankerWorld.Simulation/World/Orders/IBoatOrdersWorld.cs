using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

internal interface IBoatOrdersWorld
{
    global::ClankerWorld.Simulation.World.BoatTransportState BoatTransport { get; }
    global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id);
    bool PortIsLegal(global::ClankerWorld.Simulation.World.PlacedBuilding port);
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    global::ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool CanWalkForTownProject(string actor, global::ClankerWorld.Simulation.World.GridPoint from, global::ClankerWorld.Simulation.World.GridPoint target, int range = 0);
    bool ReadyToBoard(string actor);
    bool ApplyBoatCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string BoatOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    OrderStore Orders { get; }
    global::ClankerWorld.Simulation.World.BoatTripRequest? BoundBoatRequest(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool CanUseTownBoat(string townId, string actor);
    global::ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(global::ClankerWorld.Simulation.World.PlacedBuilding port, string passenger);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> FreePortDocks(global::ClankerWorld.Simulation.World.PlacedBuilding port, string? exceptBoatId = null);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    bool IsActiveOrder(string status);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ActiveBoatOrder(global::ClankerWorld.Simulation.World.BoatTripRequest request);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string BoatOrderArrivalEffectId(string requestId);
    void CancelBoatRequest(BoatTripRequest request, long tick);

    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor);
    global::ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice choice, string? orderInstructionId = null);
}
