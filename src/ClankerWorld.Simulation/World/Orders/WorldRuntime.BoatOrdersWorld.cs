using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IBoatOrdersWorld BoatOrdersWorld => new BoatOrdersAdapter(this);

    private sealed class BoatOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IBoatOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.BoatTransportState BoatTransport => Runtime.boatTransport;
        public global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => Runtime.Port(id);
        public bool PortIsLegal(global::ClankerWorld.Simulation.World.PlacedBuilding port) => Runtime.PortIsLegal(port);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => Runtime.PassengerBoat(actor);
        public global::ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor) => Runtime.WaitingBoatRequest(actor);
        public bool CanWalkForTownProject(string actor, global::ClankerWorld.Simulation.World.GridPoint from, global::ClankerWorld.Simulation.World.GridPoint target, int range = 0) => Runtime.CanWalkForTownProject(actor, from, target, range);
        public bool ReadyToBoard(string actor) => Runtime.ReadyToBoard(actor);
        public bool ApplyBoatCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyBoatCandidate(actor, candidateId, originalPlan);
        public global::ClankerWorld.Simulation.World.BoatTripRequest? BoundBoatRequest(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.BoundBoatRequest(instruction);
        public bool CanUseTownBoat(string townId, string actor) => Runtime.CanUseTownBoat(townId, actor);
        public global::ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(global::ClankerWorld.Simulation.World.PlacedBuilding port, string passenger) => Runtime.AvailablePortLanding(port, passenger);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> FreePortDocks(global::ClankerWorld.Simulation.World.PlacedBuilding port, string? exceptBoatId = null) => Runtime.FreePortDocks(port, exceptBoatId);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ActiveBoatOrder(global::ClankerWorld.Simulation.World.BoatTripRequest request) => Runtime.ActiveBoatOrder(request);
        public string BoatOrderArrivalEffectId(string requestId) => WorldRuntime.BoatOrderArrivalEffectId(requestId);
        public void CancelBoatRequest(BoatTripRequest request, long tick) => Runtime.transportSystem.CancelBoatRequest(request, tick);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor) => Runtime.BoatTripChoices(actor);
        public global::ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice choice, string? orderInstructionId = null) => TransportSystem.QueueBoatTrip(Runtime.BoatWorld, actor, choice, orderInstructionId);
    }
}
