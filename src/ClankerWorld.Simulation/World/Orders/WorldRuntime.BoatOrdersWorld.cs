using BoatTripChoice = ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IBoatOrdersWorld BoatOrdersWorld => new BoatOrdersAdapter(this);

    private sealed class BoatOrdersAdapter(WorldRuntime runtime) : IBoatOrdersWorld
    {
        public BoatTransportState Boats => runtime.boatTransport;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public OrderStore Orders => runtime.orderStore;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public long WorldTick => runtime.WorldTick;
        public ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => runtime.Port(id);
        public bool PortIsLegal(PlacedBuilding port) => runtime.PortIsLegal(port);
        public ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor) => runtime.WaitingBoatRequest(actor);
        public bool CanWalkForTownProject(string actor, GridPoint from, GridPoint target, int range = 0) => runtime.CanWalkForTownProject(actor, from, target, range);
        public bool ReadyToBoard(string actor) => runtime.ReadyToBoard(actor);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor) => runtime.BoatTripChoices(actor);
        public bool ApplyBoatCandidate(string actor, string candidateId, ActionPlan? originalPlan = null) => runtime.ApplyBoatCandidate(actor, candidateId, originalPlan);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, BoatTripChoice choice, string? orderInstructionId = null) => runtime.QueueBoatTrip(actor, choice, orderInstructionId);
        public bool CanUseTownBoat(string townId, string actor) => runtime.CanUseTownBoat(townId, actor);
        public ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(PlacedBuilding port, string passenger) => runtime.AvailablePortLanding(port, passenger);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.GridPoint> FreePortDocks(PlacedBuilding port, string? exceptBoatId = null) => runtime.FreePortDocks(port, exceptBoatId);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string BoatOrderArrivalEffectId(string requestId) => WorldRuntime.BoatOrderArrivalEffectId(requestId);
        public void CancelBoatRequest(BoatTripRequest request, long tick) => runtime.transportSystem.CancelBoatRequest(request, tick);
    }
}
