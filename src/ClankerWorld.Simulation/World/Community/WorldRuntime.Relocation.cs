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

    /// <summary>One unpaused world day of notice, measured in world ticks.</summary>
    private long RelocationNoticeTicks => worldSystems.Config.TicksPerDay;

    /// <summary>When this person's current membership of the household was admitted; founders and births use their own start.</summary>
    private long AdmittedTick(string personId, string householdId) =>
        CommunitySystem.AdmittedTick(CommunityRelocationWorld, personId, householdId);

    private IEnumerable<SocietyAgent> ActiveResidents(string householdId) =>
        CommunitySystem.ActiveResidents(CommunityRelocationWorld, householdId);

    // A guardian remains responsible even when their dependent lives in another House.
    private bool HasDependentInPrimaryCare(string actor) =>
        CommunitySystem.HasDependentInPrimaryCare(CommunityRelocationWorld, actor);

    private HouseRelocationRules.Selection? RelocationSelection(string householdId, string? volunteerId = null) =>
        CommunitySystem.RelocationSelection(CommunityRelocationWorld, householdId, volunteerId);

    private IEnumerable<(string Id, RelocationNotice Notice)> RelocationNotices(string? householdId = null) =>
        CommunitySystem.RelocationNotices(CommunityRelocationWorld, householdId);

    private bool HoldsRelocationNotice(string actor) =>
        CommunitySystem.HoldsRelocationNotice(CommunityRelocationWorld, actor);

    /// <summary>
    /// Only a member with a move-out notice asks another household while still
    /// living in their House; other members leave first or start a household.
    /// </summary>
    private bool MayRelocateFromHousehold(string actor) =>
        CommunitySystem.MayRelocateFromHousehold(CommunityRelocationWorld, actor);

    private void SetRelocation(string actor, RelocationNotice? notice) =>
        CommunitySystem.SetRelocation(CommunityRelocationWorld, actor, notice);

    /// <summary>
    /// Brings saved notices up to date with the actual residents, family units,
    /// dependent care and completed footprint; then any adult whose notice has
    /// run out leaves, one at a time, rechecking before each departure.
    /// </summary>
    private void MaintainRelocation() =>
        CommunitySystem.MaintainRelocation(CommunityRelocationWorld);

    /// <summary>
    /// The next adult whose notice has run out, in the order selection planned
    /// the departures. Leaving in another order can change the family majority
    /// part-way, cancel a planned notice and move out someone who never had one.
    /// </summary>
    private (string Id, RelocationNotice Notice)? NextDueRelocation(string householdId) =>
        CommunitySystem.NextDueRelocation(CommunityRelocationWorld, householdId);

    private void ReconcileRelocation(string householdId) =>
        CommunitySystem.ReconcileRelocation(CommunityRelocationWorld, householdId);

    private void CancelRelocation(string actor, RelocationNotice notice, string reason) =>
        CommunitySystem.CancelRelocation(CommunityRelocationWorld, actor, notice, reason);

    /// <summary>
    /// Volunteering is offered only when it would actually take a needed place:
    /// the adult moves alone and the House would be chosen to send them first.
    /// </summary>
    private bool MayVolunteerToRelocate(string actor) =>
        CommunitySystem.MayVolunteerToRelocate(CommunityRelocationWorld, actor);

    private void AddRelocationCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (RelocationDefinition.Offer(this, actor, new()) is { } plan)
            candidates.Add(new CognitionCandidate(plan.CandidateId,
                "Volunteer to move out of your overcrowded House so another resident can stay; you keep the current notice time to find a home.",
                105)
            { ActionPlan = plan });
    }

    private void CompleteRelocationAction(RelocationActionPlan plan) =>
        CommunitySystem.CompleteRelocationAction(CommunityRelocationWorld, plan);

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
