using BoatTripChoice = ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IBoatOrdersWorld
{
    BoatTransportState Boats { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    OrderStore Orders { get; }
    WorldContentSimulationState Simulation { get; }
    long WorldTick { get; }
    ClankerWorld.Simulation.World.PlacedBuilding? Port(string id);
    bool PortIsLegal(PlacedBuilding port);
    ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor);
    bool CanWalkForTownProject(string actor, GridPoint from, GridPoint target, int range = 0);
    bool ReadyToBoard(string actor);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor);
    bool ApplyBoatCandidate(string actor, string candidateId, ActionPlan? originalPlan = null);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, BoatTripChoice choice, string? orderInstructionId = null);
    bool CanUseTownBoat(string townId, string actor);
    ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(PlacedBuilding port, string passenger);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.GridPoint> FreePortDocks(PlacedBuilding port, string? exceptBoatId = null);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    bool IsActiveOrder(string status);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string BoatOrderArrivalEffectId(string requestId);
    void CancelBoatRequest(BoatTripRequest request, long tick);
}
