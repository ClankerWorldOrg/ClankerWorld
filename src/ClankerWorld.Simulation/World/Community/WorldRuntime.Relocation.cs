using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// A move-out notice for an adult of an overcrowded House. The deadline is a
/// fixed world tick, so pausing stops it. A late forced replacement gets a
/// fresh day; a
/// volunteer who takes another adult's place keeps that adult's deadline.
/// </summary>
public sealed record RelocationNotice(string HouseholdId, long NoticeTick, long DeadlineTick, string Reason);

/// <summary>
/// Overcrowding relocation: choosing who must move out, the one-day notice and
/// revalidation before each departure. The exit itself is the ordinary
/// departure transition, so goods, the food allowance, care and paused work
/// follow the departure rules.
/// </summary>
public sealed partial class WorldRuntime
{
    public const int RelocationSchemaVersion = 64;
    private const string HouseholdVolunteerCandidate = "household_volunteer";

    /// <summary>One unpaused world day of notice, measured in world ticks.</summary>
    private long RelocationNoticeTicks => worldSystems.Config.TicksPerDay;

    /// <summary>When this person's current membership of the household was admitted; founders and births use their own start.</summary>
    private long AdmittedTick(string personId, string householdId) => society.Checkpoint.Relationships
        .Where(edge => edge.Type == SocietyRelationshipType.HouseholdMembership && edge.TargetId == personId &&
            edge.HouseholdId == householdId && edge.State == SocietyRelationshipState.Accepted)
        .Select(edge => Math.Max(edge.EffectiveTick, edge.ProposedTick)).DefaultIfEmpty(0).Max();

    private IEnumerable<SocietyAgent> ActiveResidents(string householdId) => society.Checkpoint.Agents
        .Where(person => person.Status == SocietyAgentStatus.Active && person.HouseholdId == householdId);

    // A guardian remains responsible even when their dependent lives in another House.
    private bool HasDependentInPrimaryCare(string actor) => society.Checkpoint.Agents.Any(person =>
        person.Status == SocietyAgentStatus.Active && person.PrimaryCaregiverId == actor &&
        LifeStageRules.IsDependent(person.AgeBand));

    private HouseRelocationRules.Selection? RelocationSelection(string householdId, string? volunteerId = null)
    {
        if (HouseForHousehold(householdId) is not { } house) return null;
        var definition = BuildingStorageRules.EffectiveDefinition(
            worldContent.Buildings.Single(item => item.CanonicalId == house.DefinitionId), house);
        var residents = ActiveResidents(householdId).ToArray();
        var adults = residents.Where(person => LifeStageRules.IsAdult(person.AgeBand) &&
                agents.ContainsKey(person.Id))
            .Select(person =>
            {
                var notice = agents[person.Id].Housing?.Relocation is { } relocation && relocation.HouseholdId == householdId
                    ? relocation : null;
                return person.Id == volunteerId && notice is null
                    ? new HouseRelocationRules.Adult(person.Id, AdmittedTick(person.Id, householdId),
                        !HasDependentInPrimaryCare(person.Id), HouseRelocationRules.Volunteer, WorldTick)
                    : new HouseRelocationRules.Adult(person.Id, AdmittedTick(person.Id, householdId),
                        !HasDependentInPrimaryCare(person.Id), notice?.Reason, notice?.NoticeTick);
            });
        return HouseRelocationRules.Select(residents, adults, definition.Width, definition.Height);
    }

    private IEnumerable<(string Id, RelocationNotice Notice)> RelocationNotices(string? householdId = null) =>
        agents.Values.Where(person => person.Housing?.Relocation is { } notice &&
                (householdId is null || notice.HouseholdId == householdId))
            .Select(person => (person.AgentId, person.Housing!.Relocation!))
            .OrderBy(item => item.Item2.DeadlineTick).ThenBy(item => item.Item2.NoticeTick)
            .ThenBy(item => item.AgentId, StringComparer.Ordinal);

    private bool HoldsRelocationNotice(string actor) => agents.TryGetValue(actor, out var person) &&
        person.Housing?.Relocation is not null;

    /// <summary>
    /// Only a member with a move-out notice asks another household while still
    /// living in their House; other members leave first or start a household.
    /// </summary>
    private bool MayRelocateFromHousehold(string actor) =>
        agents.TryGetValue(actor, out var person) && person.Housing?.Relocation is { } notice &&
        society.Checkpoint.GetAgent(actor).HouseholdId == notice.HouseholdId &&
        RelocationSelection(notice.HouseholdId)?.Chosen.Any(item => item.Id == actor) == true;

    private void SetRelocation(string actor, RelocationNotice? notice) =>
        SetHousing(actor, (agents[actor].Housing ?? new()) with { Relocation = notice });

    /// <summary>
    /// Brings saved notices up to date with the actual residents, family units,
    /// dependent care and completed footprint; then any adult whose notice has
    /// run out leaves, one at a time, rechecking before each departure.
    /// </summary>
    private void MaintainRelocation()
    {
        var households = society.Checkpoint.Households.Select(household => household.Id)
            .Concat(RelocationNotices().Select(item => item.Notice.HouseholdId))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var householdId in households)
        {
            ReconcileRelocation(householdId);
            // A failed exit cancels its notice, so this loop always ends.
            while (NextDueRelocation(householdId) is { } due)
            {
                if (!DepartHousehold(due.Id, "displaced"))
                    CancelRelocation(due.Id, due.Notice, "not_needed");
                ReconcileRelocation(householdId);
            }
        }
    }

    /// <summary>
    /// The next adult whose notice has run out, in the order selection planned
    /// the departures. Leaving in another order can change the family majority
    /// part-way, cancel a planned notice and move out someone who never had one.
    /// </summary>
    private (string Id, RelocationNotice Notice)? NextDueRelocation(string householdId)
    {
        var due = RelocationNotices(householdId).Where(item => item.Notice.DeadlineTick <= WorldTick).ToArray();
        if (due.Length == 0) return null;
        var planned = RelocationSelection(householdId)?.Chosen.Select(item => item.Id).ToList() ?? [];
        return due.OrderBy(item => planned.IndexOf(item.Id) is var index and >= 0 ? index : int.MaxValue).First();
    }

    private void ReconcileRelocation(string householdId)
    {
        var selection = RelocationSelection(householdId);
        var chosen = (selection?.Chosen ?? []).ToDictionary(item => item.Id, item => item.Reason, StringComparer.Ordinal);
        // A changed care/family situation replaces a selected adult, not the
        // overcrowding case. Inherit a future deadline; a late forced replacement
        // receives a fresh day without restarting the other residents' notices.
        var replaced = new Queue<RelocationNotice>(RelocationNotices(householdId)
            .Where(item => !chosen.ContainsKey(item.Id)).Select(item => item.Notice));
        foreach (var (id, notice) in RelocationNotices(householdId).ToArray())
        {
            if (chosen.ContainsKey(id) && society.Checkpoint.GetAgent(id).HouseholdId == householdId) continue;
            var reason = selection is null ? "no_house"
                : selection.Chosen.Count == 0 && !HouseResidentCapacity(householdId)!.IsOvercrowded ? "room"
                : HasDependentInPrimaryCare(id) ? "care"
                : notice.Reason != HouseRelocationRules.Volunteer &&
                  HouseRelocationRules.DominantFamily(ActiveResidents(householdId).ToArray()) is { } dominant &&
                  society.Checkpoint.GetAgent(id).DomesticFamilyUnitId == dominant ? "family"
                : selection.Chosen.Any(item => item.Reason == HouseRelocationRules.Volunteer) ? "replaced"
                : HouseResidentCapacity(householdId) is { IsOvercrowded: false } ? "room" : "not_needed";
            CancelRelocation(id, notice, reason);
        }
        foreach (var item in selection?.Chosen ?? [])
        {
            if (agents[item.Id].Housing?.Relocation is not null) continue;
            var deadline = replaced.TryDequeue(out var prior) ? prior.DeadlineTick : checked(WorldTick + RelocationNoticeTicks);
            if (item.Reason != HouseRelocationRules.Volunteer && deadline <= WorldTick)
                deadline = checked(WorldTick + RelocationNoticeTicks);
            var noticeTick = deadline - RelocationNoticeTicks;
            SetRelocation(item.Id, new(householdId, noticeTick, deadline, item.Reason));
            AppendEvent(new RelocationNoticeEventPayload(item.Id, householdId, item.Reason, deadline));
        }
    }

    private void CancelRelocation(string actor, RelocationNotice notice, string reason)
    {
        SetRelocation(actor, null);
        AppendEvent(new RelocationCancelledEventPayload(actor, notice.HouseholdId, reason));
        // A member who no longer has to move stops asking elsewhere; someone who already left keeps asking.
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not null &&
            agents[actor].Housing?.Request is { } request)
            EndHousingRequest(actor, request, "housing_request_cancelled", remember: false);
    }

    /// <summary>
    /// Volunteering is offered only when it would actually take a needed place:
    /// the adult moves alone and the House would be chosen to send them first.
    /// </summary>
    private bool MayVolunteerToRelocate(string actor) =>
        IsAdult(actor) && !HoldsRelocationNotice(actor) &&
        society.Checkpoint.GetAgent(actor).HouseholdId is { } householdId &&
        HouseResidentCapacity(householdId) is { IsOvercrowded: true } &&
        RelocationSelection(householdId, actor)?.Chosen.Any(item => item.Id == actor) == true;

    private void AddRelocationCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (PlanRelocationAction(actor) is { } plan && CheckRelocationActionPlan(plan).Allowed)
            candidates.Add(new CognitionCandidate(plan.CandidateId,
                "Volunteer to move out of your overcrowded House so another resident can stay; you keep the current notice time to find a home.",
                105)
            { ActionPlan = plan });
    }

    private void ApplyVolunteerToRelocate(string actor)
    {
        if (PlanRelocationAction(actor) is { } plan) ApplyRelocationActionPlan(plan);
    }

    private void CompleteRelocationAction(RelocationActionPlan plan)
    {
        var actor = plan.Actor;
        var householdId = plan.HouseholdId;
        // Taking someone's place keeps their deadline, so volunteering never delays the move.
        var deadline = plan.InheritedDeadline ?? checked(WorldTick + RelocationNoticeTicks);
        SetRelocation(actor, new(householdId, WorldTick, deadline, HouseRelocationRules.Volunteer));
        AppendEvent(new RelocationNoticeEventPayload(actor, householdId, HouseRelocationRules.Volunteer, deadline));
        ReconcileRelocation(householdId);
    }

    /// <summary>What the next housing step is for a House that is over its limit, in the agent's own words.</summary>
    private string RelocationNote(string actor, string householdId, HouseResidentCapacityRules.Capacity capacity)
    {
        var house = HouseForHousehold(householdId)!;
        var expansionJob = (worldSimulation.BuildingExpansions ?? []).FirstOrDefault(job => job.BuildingInstanceId == house.InstanceId &&
            job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused);
        var expansion = expansionJob?.State switch
        {
            WorldProductionJobState.Running => " Expansion under way; no places until it finishes.",
            WorldProductionJobState.Paused => " Expansion paused; no places until it finishes.",
            _ => string.Empty,
        };
        var counts = $"House overcrowded: {capacity.ResidentCount}/{capacity.Limit} places.";
        if (agents[actor].Housing?.Relocation is { } notice)
        {
            var why = notice.Reason switch
            {
                HouseRelocationRules.Volunteer => "you volunteered",
                HouseRelocationRules.LatestUnrelatedArrival => "you were the latest arrival outside the majority family",
                _ => "you were the latest arrival with no family majority",
            };
            var nextStep = agents[actor].Housing?.Request is not null
                ? "Waiting for a household's admission answers."
                : "Ask a household with room or start your own.";
            return $"{counts} You must move out in about {HoursLeft(notice.DeadlineTick)} hours ({why}). " + nextStep + expansion;
        }
        var notices = RelocationNotices(householdId).Count();
        if (notices > 0)
            return $"{counts} {notices} adult{(notices == 1 ? " has" : "s have")} notice to move out." + expansion;
        var next = expansionJob?.State == WorldProductionJobState.Paused ? "resume the expansion"
            : expansion.Length > 0 ? "finish the expansion"
            : HouseCanExpandNow(house) ? "expand the House"
            : HouseCanExpandWithLandPermission(house) ? "get the Council's land permission and expand the House"
            : "an adult may start a separate household with their dependents and build a House";
        return $"{counts} Nobody can be required to leave. Next: {next}." + expansion;
    }

    private string HoursLeft(long deadlineTick)
    {
        var ticksPerDay = worldSystems.Config.TicksPerDay;
        var hours = (Math.Max(0, deadlineTick - WorldTick) * 24 + ticksPerDay - 1) / ticksPerDay;
        return hours.ToString(CultureInfo.InvariantCulture);
    }

    private static void ValidateRelocation(AgentState person, RelocationNotice notice,
        Dictionary<string, SocietyAgent> people, SocietyCheckpoint society, int schemaVersion)
    {
        if (schemaVersion < RelocationSchemaVersion)
            throw new InvalidDataException($"Move-out notices require world schema {RelocationSchemaVersion}.");
        var day = (long)society.Config.TicksPerWorldDay;
        if (!people.TryGetValue(person.AgentId, out var adult) || adult.Status != SocietyAgentStatus.Active ||
            !LifeStageRules.IsAdult(adult.AgeBand) ||
            string.IsNullOrWhiteSpace(notice.HouseholdId) || adult.HouseholdId != notice.HouseholdId ||
            !HouseRelocationRules.Reasons.Contains(notice.Reason, StringComparer.Ordinal) ||
            notice.NoticeTick < 0 || notice.NoticeTick > society.WorldTick ||
            notice.DeadlineTick <= notice.NoticeTick || notice.DeadlineTick > notice.NoticeTick + day ||
            notice.Reason != HouseRelocationRules.Volunteer && notice.DeadlineTick != notice.NoticeTick + day)
            throw new InvalidDataException("The saved move-out notice is invalid.");
    }
}
