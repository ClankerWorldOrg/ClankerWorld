namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActorCapabilityState? CapabilitiesFor(string actor) => agents.TryGetValue(actor, out var state)
        ? new(society.Checkpoint.GetAgent(actor).AgeBand, PassengerBoat(actor) is not null,
            RidingAnimal(actor) is not null, NeedsUrgentFood(state), NeedsUrgentWarmth(state), PendingInstructionFor(actor) is not null)
        : null;

    private ActionCheck ChildActorCapabilities(string actor, string candidate) =>
        ActorCapabilities.Check(CapabilitiesFor(actor), candidate, new(ActionLifeStages.Children));
}
