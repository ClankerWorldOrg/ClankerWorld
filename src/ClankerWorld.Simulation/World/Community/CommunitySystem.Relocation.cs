using System.Globalization;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static long AdmittedTick(ICommunityRelocationWorld world, string personId, string householdId) => world.Society.Relationships
        .Where(edge => edge.Type == SocietyRelationshipType.HouseholdMembership && edge.TargetId == personId &&
            edge.HouseholdId == householdId && edge.State == SocietyRelationshipState.Accepted)
        .Select(edge => Math.Max(edge.EffectiveTick, edge.ProposedTick)).DefaultIfEmpty(0).Max();

    internal static IEnumerable<SocietyAgent> ActiveResidents(ICommunityRelocationWorld world, string householdId) => world.Society.Agents
        .Where(person => person.Status == SocietyAgentStatus.Active && person.HouseholdId == householdId);

    internal static bool HasDependentInPrimaryCare(ICommunityRelocationWorld world, string actor) => world.Society.Agents.Any(person =>
        person.Status == SocietyAgentStatus.Active && person.PrimaryCaregiverId == actor &&
        LifeStageRules.IsDependent(person.AgeBand));

    internal static HouseRelocationRules.Selection? RelocationSelection(ICommunityRelocationWorld world, string householdId, string? volunteerId = null)
    {
        if (world.HouseForHousehold(householdId) is not { } house) return null;
        var definition = BuildingStorageRules.EffectiveDefinition(
            world.Content.Buildings.Single(item => item.CanonicalId == house.DefinitionId), house);
        var residents = ActiveResidents(world, householdId).ToArray();
        var adults = residents.Where(person => LifeStageRules.IsAdult(person.AgeBand) &&
                world.PhysicalAgents.Living.ContainsKey(person.Id))
            .Select(person =>
            {
                var notice = world.PhysicalAgents.Living[person.Id].Housing?.Relocation is { } relocation && relocation.HouseholdId == householdId
                    ? relocation : null;
                return person.Id == volunteerId && notice is null
                    ? new HouseRelocationRules.Adult(person.Id, AdmittedTick(world, person.Id, householdId),
                        !HasDependentInPrimaryCare(world, person.Id), HouseRelocationRules.Volunteer, world.WorldTick)
                    : new HouseRelocationRules.Adult(person.Id, AdmittedTick(world, person.Id, householdId),
                        !HasDependentInPrimaryCare(world, person.Id), notice?.Reason, notice?.NoticeTick);
            });
        return HouseRelocationRules.Select(residents, adults, definition.Width, definition.Height);
    }

    internal static IEnumerable<(string Id, RelocationNotice Notice)> RelocationNotices(ICommunityRelocationWorld world, string? householdId = null) =>
        world.PhysicalAgents.Living.Values.Where(person => person.Housing?.Relocation is { } notice &&
                (householdId is null || notice.HouseholdId == householdId))
            .Select(person => (person.AgentId, person.Housing!.Relocation!))
            .OrderBy(item => item.Item2.DeadlineTick).ThenBy(item => item.Item2.NoticeTick)
            .ThenBy(item => item.AgentId, StringComparer.Ordinal);

    internal static bool HoldsRelocationNotice(ICommunityRelocationWorld world, string actor) => world.PhysicalAgents.Living.TryGetValue(actor, out var person) &&
        person.Housing?.Relocation is not null;

    internal static bool MayRelocateFromHousehold(ICommunityRelocationWorld world, string actor) =>
        world.PhysicalAgents.Living.TryGetValue(actor, out var person) && person.Housing?.Relocation is { } notice &&
        world.Society.GetAgent(actor).HouseholdId == notice.HouseholdId &&
        RelocationSelection(world, notice.HouseholdId)?.Chosen.Any(item => item.Id == actor) == true;

    internal static void SetRelocation(ICommunityRelocationWorld world, string actor, RelocationNotice? notice) =>
        SetHousing(world.HousingWorld, actor, (world.PhysicalAgents.Living[actor].Housing ?? new()) with { Relocation = notice });

    internal static void MaintainRelocation(ICommunityRelocationWorld world)
    {
        var households = world.Society.Households.Select(household => household.Id)
            .Concat(RelocationNotices(world).Select(item => item.Notice.HouseholdId))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var householdId in households)
        {
            ReconcileRelocation(world, householdId);
            // A failed exit cancels its notice, so this loop always ends.
            while (NextDueRelocation(world, householdId) is { } due)
            {
                if (!world.DepartHousehold(due.Id, "displaced"))
                    CancelRelocation(world, due.Id, due.Notice, "not_needed");
                ReconcileRelocation(world, householdId);
            }
        }
    }

    internal static (string Id, RelocationNotice Notice)? NextDueRelocation(ICommunityRelocationWorld world, string householdId)
    {
        var due = RelocationNotices(world, householdId).Where(item => item.Notice.DeadlineTick <= world.WorldTick).ToArray();
        if (due.Length == 0) return null;
        var planned = RelocationSelection(world, householdId)?.Chosen.Select(item => item.Id).ToList() ?? [];
        return due.OrderBy(item => planned.IndexOf(item.Id) is var index and >= 0 ? index : int.MaxValue).First();
    }

    internal static void ReconcileRelocation(ICommunityRelocationWorld world, string householdId)
    {
        var selection = RelocationSelection(world, householdId);
        var chosen = (selection?.Chosen ?? []).ToDictionary(item => item.Id, item => item.Reason, StringComparer.Ordinal);
        // A changed care/family situation replaces a selected adult, not the
        // overcrowding case. Inherit a future deadline; a late forced replacement
        // receives a fresh day without restarting the other residents' notices.
        var replaced = new Queue<RelocationNotice>(RelocationNotices(world, householdId)
            .Where(item => !chosen.ContainsKey(item.Id)).Select(item => item.Notice));
        foreach (var (id, notice) in RelocationNotices(world, householdId).ToArray())
        {
            if (chosen.ContainsKey(id) && world.Society.GetAgent(id).HouseholdId == householdId) continue;
            var reason = selection is null ? "no_house"
                : selection.Chosen.Count == 0 && !world.HouseResidentCapacity(householdId)!.IsOvercrowded ? "room"
                : HasDependentInPrimaryCare(world, id) ? "care"
                : notice.Reason != HouseRelocationRules.Volunteer &&
                  HouseRelocationRules.DominantFamily(ActiveResidents(world, householdId).ToArray()) is { } dominant &&
                  world.Society.GetAgent(id).DomesticFamilyUnitId == dominant ? "family"
                : selection.Chosen.Any(item => item.Reason == HouseRelocationRules.Volunteer) ? "replaced"
                : world.HouseResidentCapacity(householdId) is { IsOvercrowded: false } ? "room" : "not_needed";
            CancelRelocation(world, id, notice, reason);
        }
        foreach (var item in selection?.Chosen ?? [])
        {
            if (world.PhysicalAgents.Living[item.Id].Housing?.Relocation is not null) continue;
            var deadline = replaced.TryDequeue(out var prior) ? prior.DeadlineTick : checked(world.WorldTick + world.RelocationNoticeTicks);
            if (item.Reason != HouseRelocationRules.Volunteer && deadline <= world.WorldTick)
                deadline = checked(world.WorldTick + world.RelocationNoticeTicks);
            var noticeTick = deadline - world.RelocationNoticeTicks;
            SetRelocation(world, item.Id, new(householdId, noticeTick, deadline, item.Reason));
            world.AppendEvent("relocation_notice", $"{item.Id}|{householdId}|{item.Reason}|" +
                deadline.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal static void CancelRelocation(ICommunityRelocationWorld world, string actor, RelocationNotice notice, string reason)
    {
        SetRelocation(world, actor, null);
        world.AppendEvent("relocation_cancelled", $"{actor}|{notice.HouseholdId}|{reason}");
        // A member who no longer has to move stops asking elsewhere; someone who already left keeps asking.
        if (world.Society.GetAgent(actor).HouseholdId is not null &&
            world.PhysicalAgents.Living[actor].Housing?.Request is { } request)
            world.EndHousingRequest(actor, request, "housing_request_cancelled", remember: false);
    }

    internal static bool MayVolunteerToRelocate(ICommunityRelocationWorld world, string actor) =>
        world.IsAdult(actor) && !HoldsRelocationNotice(world, actor) &&
        world.Society.GetAgent(actor).HouseholdId is { } householdId &&
        world.HouseResidentCapacity(householdId) is { IsOvercrowded: true } &&
        RelocationSelection(world, householdId, actor)?.Chosen.Any(item => item.Id == actor) == true;

    internal static void CompleteRelocationAction(ICommunityRelocationWorld world, RelocationActionPlan plan)
    {
        var actor = plan.Actor;
        var householdId = plan.HouseholdId;
        // Taking someone's place keeps their deadline, so volunteering never delays the move.
        var deadline = plan.InheritedDeadline ?? checked(world.WorldTick + world.RelocationNoticeTicks);
        SetRelocation(world, actor, new(householdId, world.WorldTick, deadline, HouseRelocationRules.Volunteer));
        world.AppendEvent("relocation_notice", $"{actor}|{householdId}|{HouseRelocationRules.Volunteer}|" +
            deadline.ToString(CultureInfo.InvariantCulture));
        ReconcileRelocation(world, householdId);
    }
}
