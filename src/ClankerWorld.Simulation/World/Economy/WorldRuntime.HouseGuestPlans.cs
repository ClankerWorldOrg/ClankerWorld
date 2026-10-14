namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HouseGuestActionPlan? PlanHouseGuest(string actor, HouseGuestChoice choice, string? houseId = null, bool offeredChoice = true)
    {
        if (!agents.ContainsKey(actor)) return null;
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        var house = houseId is null ? household is null ? null : HouseForHousehold(household)
            : worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == houseId);
        if (house is null) return null;
        return new(actor, choice, house, household,
            agents.ContainsKey(choice.GuestId) ? society.Checkpoint.GetAgent(choice.GuestId).Name : choice.GuestId, offeredChoice);
    }

    private ActionCheck CheckHouseGuest(HouseGuestActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseGuestCapabilities(plan.OfferedChoice));
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.House.InstanceId) is not { } house ||
            house.DefinitionId != plan.House.DefinitionId || house.Position != plan.House.Position || house.HouseholdId is null ||
            house.HouseholdId != plan.ActorHouseholdId || society.Checkpoint.GetAgent(plan.Actor).HouseholdId != plan.ActorHouseholdId ||
            !worldContent.Buildings.Any(definition => definition.CanonicalId == house.DefinitionId && definition.Tags.Contains("house", StringComparer.Ordinal)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Only an adult member of the House's household can invite or revoke a guest.");
        if (!agents.TryGetValue(plan.Choice.GuestId, out var guest))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Choose a living named guest.");
        if (society.Checkpoint.GetAgent(guest.AgentId).HouseholdId == house.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Household members already have access to their own House.");
        if (plan.OfferedChoice)
        {
            if (HasHouseGuestInvitation(guest.AgentId, house.InstanceId) == plan.Choice.Invited)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected guest invitation already matches this choice");
            if (plan.Choice.Invited && !IsWithinInteractionRange(agents[plan.Actor].Position, guest.Position, 2))
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer close enough to invite that guest");
        }
        return new();
    }

    private void ApplyHouseGuest(string actor, string candidate, HouseGuestActionPlan? originalPlan)
    {
        if (HouseGuestChoice.Decode(candidate) is not { } choice) return;
        var plan = originalPlan ?? PlanHouseGuest(actor, choice);
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        SetHouseGuestInvitationCore(actor, plan.House.InstanceId, choice.GuestId, choice.Invited, plan);
    }
}
