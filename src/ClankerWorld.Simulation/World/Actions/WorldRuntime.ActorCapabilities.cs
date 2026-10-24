namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActorCapabilityState? CapabilitiesFor(string actor) => agents.TryGetValue(actor, out var state)
        ? new(society.Checkpoint.GetAgent(actor).AgeBand, TransportSystem.PassengerBoat(this.BoatWorld, actor) is not null,
            RidingAnimal(actor) is not null, NeedsUrgentFood(state), NeedsUrgentWarmth(state), PendingInstructionFor(actor) is not null)
        : null;

}
