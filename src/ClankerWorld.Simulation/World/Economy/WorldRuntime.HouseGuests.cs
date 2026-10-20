using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed record HouseGuestInvitation(
    string HouseInstanceId, string GuestId, string InvitedById, long ChangedTick, bool Active);

public sealed record BuildingAccessResult(bool Applied, string? Failure = null);

public sealed partial class WorldRuntime
{
    private const string InviteHouseGuestPrefix = HouseGuestChoice.InvitePrefix;
    private const string RevokeHouseGuestPrefix = HouseGuestChoice.RevokePrefix;

    public BuildingAccessResult SetHouseGuestInvitation(string actor, string houseId, string guestId, bool invited)
    {
        gate.Wait();
        try { return SetHouseGuestInvitationCore(actor, houseId, guestId, invited); }
        finally { gate.Release(); }
    }

    private BuildingAccessResult SetHouseGuestInvitationCore(string actor, string houseId, string guestId, bool invited,
        HouseGuestActionPlan? originalPlan = null)
    {
        var plan = originalPlan ?? PlanHouseGuest(actor, new(guestId, invited), houseId, offeredChoice: false);
        if (plan is null) return new(false, "Only an adult member of the House's household can invite or revoke a guest.");
        if (CheckActionPlan(plan).Refusal is { } refusal)
            return new(false, refusal.Reason is ActionRefusalReason.ActorUnavailable or ActionRefusalReason.LifeStage
                ? "Only an adult member of the House's household can invite or revoke a guest." : refusal.Explanation);
        var existing = (worldSimulation.GuestInvitations ?? []).SingleOrDefault(item =>
            item.HouseInstanceId == houseId && item.GuestId == guestId);
        if (existing?.Active == invited || existing is null && !invited) return new(true);
        var invitation = new HouseGuestInvitation(houseId, guestId, actor, WorldTick, invited);
        constructionState = WorldConstructionState.From(worldSimulation with
        {
            GuestInvitations = (worldSimulation.GuestInvitations ?? []).Where(item =>
                    item.HouseInstanceId != houseId || item.GuestId != guestId).Append(invitation)
                .OrderBy(item => item.HouseInstanceId, StringComparer.Ordinal)
                .ThenBy(item => item.GuestId, StringComparer.Ordinal).ToArray(),
        });
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(invited ? new HouseGuestInvitedEventPayload(actor, houseId, guestId) : new HouseGuestRevokedEventPayload(actor, houseId, guestId));
        return new(true);
    }

    private bool HasHouseGuestInvitation(string actor, string houseId) =>
        (worldSimulation.GuestInvitations ?? []).Any(item =>
            item.HouseInstanceId == houseId && item.GuestId == actor && item.Active);

    private void AddHouseGuestCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (household is null || HouseForHousehold(household) is not { } house) return;
        foreach (var plan in agents.Values.Where(person => person.AgentId != actor &&
                     society.Checkpoint.GetAgent(person.AgentId).HouseholdId != household &&
                     !HasHouseGuestInvitation(person.AgentId, house.InstanceId) &&
                     IsWithinInteractionRange(agents[actor].Position, person.Position, 2))
                 .OrderBy(person => person.AgentId, StringComparer.Ordinal)
                 .Select(person => HouseGuestDefinition.Offer(this, actor, new(person.AgentId, true)))
                 .Where(plan => plan is not null).Take(4))
            candidates.Add(new(plan!.CandidateId,
                $"Invite {plan.GuestName} to shelter in the House during storms, without stock or cooking access.",
                110, plan.Choice.GuestId)
            { ActionPlan = plan });
        foreach (var invitation in (worldSimulation.GuestInvitations ?? []).Where(item =>
                     item.HouseInstanceId == house.InstanceId && item.Active && agents.ContainsKey(item.GuestId)))
        {
            var plan = HouseGuestDefinition.Offer(this, actor, new(invitation.GuestId, false));
            if (plan is not null)
                candidates.Add(new(plan.CandidateId,
                    $"End {plan.GuestName}'s invitation to shelter in the House.", 110, invitation.GuestId)
                { ActionPlan = plan });
        }
    }
}
