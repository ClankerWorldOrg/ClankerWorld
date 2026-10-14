using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed record HouseGuestInvitation(
    string HouseInstanceId, string GuestId, string InvitedById, long ChangedTick, bool Active);

public sealed record BuildingAccessResult(bool Applied, string? Failure = null);

public sealed partial class WorldRuntime
{
    private const string InviteHouseGuestPrefix = "invite_house_guest:";
    private const string RevokeHouseGuestPrefix = "revoke_house_guest:";

    public BuildingAccessResult SetHouseGuestInvitation(string actor, string houseId, string guestId, bool invited)
    {
        gate.Wait();
        try { return SetHouseGuestInvitationCore(actor, houseId, guestId, invited); }
        finally { gate.Release(); }
    }

    private BuildingAccessResult SetHouseGuestInvitationCore(string actor, string houseId, string guestId, bool invited)
    {
        var house = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == houseId);
        if (!IsAdult(actor) || house?.HouseholdId is null ||
            house.HouseholdId != society.Checkpoint.GetAgent(actor).HouseholdId ||
            !worldContent.Buildings.Any(item => item.CanonicalId == house.DefinitionId &&
                item.Tags.Contains("house", StringComparer.Ordinal)))
            return new(false, "Only an adult member of the House's household can invite or revoke a guest.");
        if (!agents.ContainsKey(guestId)) return new(false, "Choose a living named guest.");
        if (society.Checkpoint.GetAgent(guestId).HouseholdId == house.HouseholdId)
            return new(false, "Household members already have access to their own House.");
        var existing = (worldSimulation.GuestInvitations ?? []).SingleOrDefault(item =>
            item.HouseInstanceId == houseId && item.GuestId == guestId);
        if (existing?.Active == invited || existing is null && !invited) return new(true);
        var invitation = new HouseGuestInvitation(houseId, guestId, actor, WorldTick, invited);
        worldSimulation = worldSimulation with
        {
            GuestInvitations = (worldSimulation.GuestInvitations ?? []).Where(item =>
                    item.HouseInstanceId != houseId || item.GuestId != guestId).Append(invitation)
                .OrderBy(item => item.HouseInstanceId, StringComparer.Ordinal)
                .ThenBy(item => item.GuestId, StringComparer.Ordinal).ToArray(),
        };
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
        foreach (var guest in agents.Values.Where(person => person.AgentId != actor &&
                     society.Checkpoint.GetAgent(person.AgentId).HouseholdId != household &&
                     !HasHouseGuestInvitation(person.AgentId, house.InstanceId) &&
                     IsWithinInteractionRange(agents[actor].Position, person.Position, 2))
                 .OrderBy(person => person.AgentId, StringComparer.Ordinal).Take(4))
            candidates.Add(new(InviteHouseGuestPrefix + guest.AgentId,
                $"Invite {society.Checkpoint.GetAgent(guest.AgentId).Name} to shelter in the House during storms, without stock or cooking access.",
                110, guest.AgentId));
        foreach (var invitation in (worldSimulation.GuestInvitations ?? []).Where(item =>
                     item.HouseInstanceId == house.InstanceId && item.Active && agents.ContainsKey(item.GuestId)))
            candidates.Add(new(RevokeHouseGuestPrefix + invitation.GuestId,
                $"End {society.Checkpoint.GetAgent(invitation.GuestId).Name}'s invitation to shelter in the House.", 110, invitation.GuestId));
    }
}
