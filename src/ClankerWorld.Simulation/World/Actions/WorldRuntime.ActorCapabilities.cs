namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck ChildActorCapabilities(string actor, string candidate)
    {
        if (!agents.TryGetValue(actor, out var state))
            return ActionCheck.Refuse(ActionRefusalReason.ActorUnavailable, "the person is no longer here");
        if (!IsChild(actor) || !AgePermitsCandidate(actor, candidate))
            return ActionCheck.Refuse(ActionRefusalReason.LifeStage, "this activity is for children");
        if (PassengerBoat(actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.Boat, "they must stay aboard the boat");
        if (NeedsUrgentWarmth(state))
            return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "getting warm comes first");
        if (PendingInstructionFor(actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current order comes first");
        // Mounted movement retains the route service's existing rider policy.
        return new();
    }
}
