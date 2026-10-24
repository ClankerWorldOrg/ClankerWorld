using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IBoatOrdersWorld BoatOrdersWorld => new BoatOrdersAdapter(this);

    private sealed class BoatOrdersAdapter(WorldRuntime runtime) : IBoatOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.BoatTransportState BoatTransport => runtime.boatTransport;
        public global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => runtime.Port(id);
        public bool PortIsLegal(global::ClankerWorld.Simulation.World.PlacedBuilding port) => runtime.PortIsLegal(port);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor) => TransportSystem.WaitingBoatRequest(runtime.BoatWorld, actor);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool CanWalkForTownProject(string actor, global::ClankerWorld.Simulation.World.GridPoint from, global::ClankerWorld.Simulation.World.GridPoint target, int range = 0) => runtime.CanWalkForTownProject(actor, from, target, range);
        public bool ReadyToBoard(string actor) => TransportSystem.ReadyToBoard(runtime.BoatWorld, actor);
        public bool ApplyBoatCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyBoatCandidate(actor, candidateId, originalPlan);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string BoatOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.BoatOrderBlockedReason(instruction);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public OrderStore Orders => runtime.orderStore;
        public global::ClankerWorld.Simulation.World.BoatTripRequest? BoundBoatRequest(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.BoundBoatRequest(instruction);
        public bool CanUseTownBoat(string townId, string actor) => TransportSystem.CanUseTownBoat(runtime.BoatWorld, townId, actor);
        public global::ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(global::ClankerWorld.Simulation.World.PlacedBuilding port, string passenger) => TransportSystem.AvailablePortLanding(runtime.BoatWorld, port, passenger);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> FreePortDocks(global::ClankerWorld.Simulation.World.PlacedBuilding port, string? exceptBoatId = null) => TransportSystem.FreePortDocks(runtime.BoatWorld, port, exceptBoatId);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public long WorldTick => runtime.WorldTick;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ActiveBoatOrder(global::ClankerWorld.Simulation.World.BoatTripRequest request) => runtime.ActiveBoatOrder(request);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string BoatOrderArrivalEffectId(string requestId) => WorldRuntime.BoatOrderArrivalEffectId(requestId);
        public void CancelBoatRequest(BoatTripRequest request, long tick) => runtime.transportSystem.CancelBoatRequest(request, tick);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor) => TransportSystem.BoatTripChoices(runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice choice, string? orderInstructionId = null) => TransportSystem.QueueBoatTrip(runtime.BoatWorld, actor, choice, orderInstructionId);
    }
}
