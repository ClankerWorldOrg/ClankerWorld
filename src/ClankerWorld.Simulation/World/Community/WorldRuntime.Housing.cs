using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// An adult's request to live in another household's House. Every adult
/// current member of that household must agree; one refusal,
/// or no answer within the window, ends it.
/// </summary>
public sealed record HousingRequest(string HouseholdId, long RequestedTick, long ExpiryTick,
    IReadOnlyList<string> Members, IReadOnlyList<string> Approvals, IReadOnlyList<string> Rejections);

/// <summary>A household that refused or did not answer, so it is not asked again for a while.</summary>
public sealed record HousingRefusal(string HouseholdId, long Tick);

/// <summary>
/// Housing status for an adult applicant or a resident with an unmet housing
/// need, including a move-out notice from an overcrowded House.
/// </summary>
public sealed record HousingState(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HousingRequest? Request = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HousingRefusal>? Refusals = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Blocker = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RelocationNotice? Relocation = null);

/// <summary>
/// Why a resident has no authorized home or their household is overcrowded.
/// These codes are saved and shown to the owner and to the agent's own model;
/// see <see cref="WorldRuntime.HousingBlocker"/>.
/// </summary>
public static class HousingBlockers
{
    public const string NoHousehold = "no_household";
    public const string NoAuthorizedHome = "no_authorized_home";
    public const string NoLegalSite = "no_legal_site";
    public const string LandRecoveryGuidance =
        "Ask for household land use or a Council land claim where those choices are available. Approval is required.";
    public const string MissingMaterials = "missing_materials";
    public const string AwaitingAnswer = "awaiting_answer";
    public const string Overcrowded = "overcrowded";

    public static readonly IReadOnlyList<string> All =
        [NoHousehold, NoAuthorizedHome, NoLegalSite, MissingMaterials, AwaitingAnswer, Overcrowded];
}

/// <summary>
/// Existing home first: an adult with no household asks a household that
/// holds a House before any construction is considered. Moving in needs the
/// agreement of every adult member; standing nearby grants nothing, and a
/// pending request grants no stock or shelter access. Add Agent placement on
/// household property keeps joining without consent (see AddAgent).
/// </summary>
public sealed partial class WorldRuntime
{
    private const int HousingSchemaVersion = 32;
    public const int HousingRequestTicks = 120;
    private const string HousingAskPrefix = "household_ask:";
    private const string HousingAdmitPrefix = "household_admit:";
    private const string HousingRefusePrefix = "household_refuse:";

    /// <summary>A refused or unanswered household is asked again only after two world days.</summary>
    private long HousingRefusalCooldownTicks => 2L * worldSystems.Config.TicksPerDay;

    private string[] HouseholdAdults(string householdId) => society.Checkpoint.Agents.Where(person =>
            person.Status == SocietyAgentStatus.Active && person.HouseholdId == householdId &&
            LifeStageRules.IsAdult(person.AgeBand))
        .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();

    private bool HasHome(string actor) =>
        society.Checkpoint.GetAgent(actor).HouseholdId is { } householdId && HouseForHousehold(householdId) is not null;

    private HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string householdId,
        SocietyAgent? proposedResident = null)
    {
        if (HouseForHousehold(householdId) is not { } house)
            return null;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
        var effective = BuildingStorageRules.EffectiveDefinition(definition, house);
        var residents = society.Checkpoint.Agents.Where(person =>
            person.HouseholdId == householdId && person.Status == SocietyAgentStatus.Active);
        if (proposedResident is not null && !residents.Any(person => person.Id == proposedResident.Id))
            residents = residents.Append(proposedResident with { HouseholdId = householdId });
        return HouseResidentCapacityRules.Calculate(residents, effective.Width, effective.Height);
    }

    private bool CanFitHouseResident(string householdId, SocietyAgent proposedResident) =>
        HouseResidentCapacity(householdId, proposedResident) is { IsOvercrowded: false };

    /// <summary>
    /// Households this adult may ask now: they hold a House in the adult's
    /// Town, or in a Town whose admission the adult has asked for or been
    /// approved for, have room for the adult's whole care group, have an
    /// adult who can answer, and did not refuse recently. Household and Town
    /// admission stay separate decisions. An adult with no household asks,
    /// and so does a member with a move-out notice from an overcrowded House;
    /// how an adult leaves or changes a household follows the agreed departure
    /// and care-group rules.
    /// </summary>
    private IEnumerable<SocietyHousehold> AskableHouseholds(string actor)
    {
        if (!IsAdult(actor))
            yield break;
        var current = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (current is not null && !MayRelocateFromHousehold(actor))
            yield break;
        var housing = agents[actor].Housing;
        if (housing?.Request is not null)
            yield break;
        var town = TownForResident(actor);
        foreach (var household in society.Checkpoint.Households.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (household.Id == current || HouseForHousehold(household.Id) is not { } house ||
                house.TownId != town && !HasOpenAdmission(actor, house.TownId) ||
                !CanFitCareGroup(household.Id, actor) ||
                HouseholdAdults(household.Id).Length == 0 ||
                housing?.Refusals?.Any(refusal => refusal.HouseholdId == household.Id &&
                    WorldTick - refusal.Tick < HousingRefusalCooldownTicks) == true)
                continue;
            yield return household;
        }
    }

    /// <summary>Applicants whose request this member has not answered yet, in a stable order.</summary>
    private IEnumerable<string> PendingHousingRequestsFor(string member)
    {
        var householdId = society.Checkpoint.GetAgent(member).HouseholdId;
        if (householdId is null)
            yield break;
        foreach (var applicant in agents.Values.OrderBy(person => person.AgentId, StringComparer.Ordinal))
        {
            if (applicant.Housing?.Request is { } request && request.HouseholdId == householdId &&
                request.Members.Contains(member, StringComparer.Ordinal) &&
                !request.Approvals.Contains(member, StringComparer.Ordinal) &&
                !request.Rejections.Contains(member, StringComparer.Ordinal))
                yield return applicant.AgentId;
        }
    }

    private bool HasHousingDecision(string actor) => ReadyForBriefInteraction(actor) && PendingHousingRequestsFor(actor).Any();

    private void AddHousingCandidateOptions(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor))
            return;
        AddDepartureCandidates(candidates, actor);
        AddRelocationCandidates(candidates, actor);
        // Only a member with a move-out notice asks while still living in their House.
        var member = society.Checkpoint.GetAgent(actor).HouseholdId is not null;
        foreach (var household in AskableHouseholds(actor))
        {
            candidates.Add(new(HousingAskPrefix + household.Id,
                member
                    ? $"Ask the {household.Name} household to let you move into their House, which has room; every adult member must agree."
                    : $"Ask the {household.Name} household to let you live in their House; every adult member must agree.",
                18, household.Id));
        }
        foreach (var applicant in PendingHousingRequestsFor(actor))
        {
            var name = society.Checkpoint.GetAgent(applicant).Name;
            candidates.Add(new(HousingAdmitPrefix + applicant,
                $"Agree to let {name} live in your household's House as a member; every adult member must agree.", 16));
            candidates.Add(new(HousingRefusePrefix + applicant,
                $"Refuse {name}'s request to live in your household's House.", 70));
        }
    }

    private void ApplyHousingCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        if (originalPlan is RelocationActionPlan relocation) { ApplyRelocationActionPlan(relocation); return; }
        if (originalPlan is ReplacementCareActionPlan replacement) { ApplyReplacementCarePlan(replacement); return; }
        if (originalPlan is HouseholdResumeActionPlan resume) { ApplyHouseholdResumePlan(resume); return; }
        if (originalPlan is DepartureActionPlan departure) { ApplyDepartureActionPlan(departure); return; }
        if (originalPlan is HousingActionPlan housing) { ApplyHousingActionPlan(housing); return; }
        if (HousingChoice.Decode(candidate) is { } choice)
        {
            if (PlanHousingAction(actor, choice) is { } plan) ApplyHousingActionPlan(plan);
            return;
        }
        if (!IsAdult(actor)) return;
        if (candidate == HouseholdVolunteerCandidate) { ApplyVolunteerToRelocate(actor); return; }
        ApplyDepartureCandidate(actor, candidate, originalPlan);
    }

    private void CompleteHousingAction(HousingActionPlan plan)
    {
        if (plan.Choice.Kind == HousingChoiceKind.Ask)
        {
            SetHousing(plan.Actor, (agents[plan.Actor].Housing ?? new()) with
            {
                Request = new(plan.HouseholdId, WorldTick, WorldTick + HousingRequestTicks, plan.AdultMembers, [], []),
            });
            AppendEvent(new HousingRequestMadeEventPayload(plan.Actor, plan.HouseholdId));
            return;
        }
        var housing = agents[plan.ApplicantId].Housing!;
        var request = housing.Request!;
        SetHousing(plan.ApplicantId, housing with
        {
            Request = plan.Choice.Kind == HousingChoiceKind.Admit
                ? request with { Approvals = request.Approvals.Append(plan.Actor).Order(StringComparer.Ordinal).ToArray() }
                : request with { Rejections = request.Rejections.Append(plan.Actor).Order(StringComparer.Ordinal).ToArray() },
        });
        AppendEvent(new HousingAnswerRecordedEventPayload(plan.Actor, plan.ApplicantId));
        ResolveHousingRequest(plan.ApplicantId);
    }

    private void MaintainHousing()
    {
        foreach (var actor in agents.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            ResolveHousingRequest(actor);
            var housing = agents[actor].Housing;
            var refusals = housing?.Refusals?.Where(refusal => WorldTick - refusal.Tick < HousingRefusalCooldownTicks).ToArray();
            var blocker = HousingBlocker(actor);
            if (housing?.Blocker == blocker && (refusals?.Length ?? 0) == (housing?.Refusals?.Count ?? 0))
                continue;
            SetHousing(actor, (housing ?? new()) with { Blocker = blocker, Refusals = refusals });
            if (blocker is not null && blocker != HousingBlockers.AwaitingAnswer && housing?.Blocker != blocker)
                AppendEvent(new HousingBlockedEventPayload(actor, blocker));
        }
    }

    private void ResolveHousingRequest(string actor)
    {
        // Care, family or a completed expansion can change between answers,
        // including earlier in this decision batch. Cancel stale notices first.
        if (agents.TryGetValue(actor, out var applicant) && applicant.Housing is { Request: not null, Relocation: { } notice })
            ReconcileRelocation(notice.HouseholdId);
        if (!agents.TryGetValue(actor, out var state) || state.Housing is not { Request: { } request } housing)
            return;
        var living = HouseholdAdults(request.HouseholdId);
        var members = request.Members.Union(living, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!request.Members.SequenceEqual(members, StringComparer.Ordinal))
        {
            // Existing answers stay recorded, while adults added or grown into
            // the household must answer before this request can be accepted.
            request = request with { Members = members };
            housing = housing with { Request = request };
            SetHousing(actor, housing);
        }
        var current = IsAdult(actor) ? society.Checkpoint.GetAgent(actor).HouseholdId : null;
        // A member's request ends with their move-out notice: the move is no longer needed.
        if (!IsAdult(actor) || current is not null && (current == request.HouseholdId || !MayRelocateFromHousehold(actor)) ||
            HouseForHousehold(request.HouseholdId) is null || living.Length == 0)
        {
            EndHousingRequest(actor, request, "housing_request_cancelled", remember: false);
        }
        else if (WorldTick > request.ExpiryTick)
        {
            // Restored answers, or the loss of the last unanswered adult,
            // cannot grant membership after the request's world-tick deadline.
            EndHousingRequest(actor, request, "housing_request_expired", remember: true);
        }
        else if (request.Rejections.Any(id => living.Contains(id, StringComparer.Ordinal)))
        {
            EndHousingRequest(actor, request, "housing_request_refused", remember: true);
        }
        else if (living.All(id => request.Approvals.Contains(id, StringComparer.Ordinal)) &&
            !CanFitCareGroup(request.HouseholdId, actor))
        {
            EndHousingRequest(actor, request, "housing_request_blocked_capacity", remember: true);
        }
        else if (living.All(id => request.Approvals.Contains(id, StringComparer.Ordinal)))
        {
            // A member moves out through the ordinary departure first, so the
            // allowance, collection right and care group follow the same rules.
            if (current is not null &&
                !DepartHousehold(actor, HoldsRelocationNotice(actor) ? "displaced" : "voluntary"))
            {
                EndHousingRequest(actor, request, "housing_request_cancelled", remember: false);
                return;
            }
            society.Apply(checkpoint => SocietyRules.JoinHouseholdCareGroup(checkpoint, actor, request.HouseholdId));
            if (society.Checkpoint.GetAgent(actor).HouseholdId != request.HouseholdId)
            {
                EndHousingRequest(actor, request, "housing_request_cancelled", remember: false);
                return;
            }
            foreach (var id in MovingCareGroup(actor))
                SetHousing(id, (agents[id].Housing ?? new()) with { Request = null, Refusals = null, Blocker = null });
            AppendEvent(new HouseholdJoinedEventPayload(actor, request.HouseholdId));
        }
    }

    private void EndHousingRequest(string actor, HousingRequest request, string eventKind, bool remember)
    {
        var housing = agents[actor].Housing!;
        var refusals = (housing.Refusals ?? []).Where(refusal => refusal.HouseholdId != request.HouseholdId);
        if (remember)
            refusals = refusals.Append(new HousingRefusal(request.HouseholdId, WorldTick));
        SetHousing(actor, housing with
        {
            Request = null,
            Refusals = refusals.OrderBy(refusal => refusal.HouseholdId, StringComparer.Ordinal).ToArray(),
            Blocker = null,
        });
        // The blocker is recomputed now, so the owner never sees a stale "waiting" line.
        SetHousing(actor, (agents[actor].Housing ?? new()) with { Blocker = HousingBlocker(actor) });
        AppendEvent(WorldEvents.Housing(eventKind, actor, request.HouseholdId));
    }

    private void SetHousing(string actor, HousingState housing)
    {
        var empty = housing.Request is null && housing.Refusals is not { Count: > 0 } && housing.Blocker is null &&
            housing.Relocation is null;
        agents[actor] = agents[actor] with
        {
            Housing = empty ? null : housing with { Refusals = housing.Refusals is { Count: > 0 } ? housing.Refusals : null },
        };
        if (!empty)
            checkpointSchemaVersion = StateSchemaVersion;
    }

    /// <summary>
    /// The resident's current housing need, or null when their household has
    /// an authorized home within its completed capacity. Only adults can make
    /// admission requests or receive construction choices.
    /// </summary>
    private string? HousingBlocker(string actor)
    {
        if (!agents.TryGetValue(actor, out var physical))
            return null;
        var person = society.Checkpoint.GetAgent(actor);
        if (person.Status != SocietyAgentStatus.Active)
            return null;
        if (person.HouseholdId is { } currentHousehold && HouseForHousehold(currentHousehold) is not null)
            return HouseResidentCapacity(currentHousehold) is { IsOvercrowded: true }
                ? HousingBlockers.Overcrowded
                : null;
        var adult = IsAdult(actor);
        if (adult && physical.Housing?.Request is not null)
            return HousingBlockers.AwaitingAnswer;
        if (person.HouseholdId is not { } householdId)
            return adult ? HousingBlockers.NoHousehold : HousingBlockers.NoAuthorizedHome;
        if (HouseholdBuildingProjectInProgress(householdId, "house", actor))
            return HousingBlockers.NoAuthorizedHome;
        var house = PlannableHouseholdBuildings(householdId, actor)
            .FirstOrDefault(definition => HouseholdBuildingKind(definition) == "house");
        if (house is null)
            return HousingBlockers.NoAuthorizedHome;
        if (!HouseholdHasMaterialsInHand(householdId, house.BuildCosts))
            return HousingBlockers.MissingMaterials;
        if (TownLayoutService.RankConstructionSites(CreateTownLayoutContext(actor), house).Count == 0)
            return HousingBlockers.NoLegalSite;
        return HousingBlockers.NoAuthorizedHome;
    }

    /// <summary>What the agent's own model is told about its housing; null when it has a home.</summary>
    private string? HousingNote(string actor)
    {
        var person = society.Checkpoint.GetAgent(actor);
        if (person.HouseholdId is { } householdId && HouseForHousehold(householdId) is { } house &&
            HouseResidentCapacity(householdId) is { } capacity)
        {
            if (capacity.IsOvercrowded)
                return RelocationNote(actor, householdId, capacity);
            var definition = worldContent.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
            var footprint = WorldContentSimulationRules.Footprint(definition, house).ToHashSet();
            var inside = agents.Values.Count(physical =>
                society.Checkpoint.GetAgent(physical.AgentId).Status == SocietyAgentStatus.Active &&
                footprint.Contains(physical.Position));
            var state = capacity.HasFreePlace ? "has room" : "is full";
            var family = capacity.HasDominantFamily
                ? "; majority-family limit applies"
                : string.Empty;
            var expansion = (worldSimulation.BuildingExpansions ?? []).Any(job =>
                job.BuildingInstanceId == house.InstanceId && job.State == WorldProductionJobState.Running)
                ? "; expansion adds places when complete" : string.Empty;
            return $"House: {capacity.ResidentCount}/{capacity.Limit} permanent places ({state}); " +
                $"{inside} inside; absences still count{family}{expansion}";
        }
        if (agents[actor].Housing is not { Blocker: { } blocker } housing)
            return null;
        return blocker switch
        {
            HousingBlockers.AwaitingAnswer when housing.Request is { } request =>
                $"You have asked the {HouseholdNameFor(request.HouseholdId)} household to let you live in their House. Every adult member must agree.",
            HousingBlockers.NoHousehold =>
                "You belong to no household. Seek an accepting household with room for your complete care group first; otherwise start your own household and build a House.",
            HousingBlockers.NoAuthorizedHome =>
                "You have no home. Your household holds no House yet, so plan one or help build it.",
            HousingBlockers.MissingMaterials =>
                "You have no home. Your household holds no House and lacks the materials to build one.",
            HousingBlockers.NoLegalSite =>
                "You have no home. Your household has the materials for a House but no legal site to build it. " +
                HousingBlockers.LandRecoveryGuidance,
            HousingBlockers.Overcrowded =>
                "Your household's House is over its completed resident capacity and takes no new residents until it has room.",
            _ => null,
        };
    }

    private string HouseholdNameFor(string householdId) =>
        society.Checkpoint.Households.FirstOrDefault(item => item.Id == householdId)?.Name ?? "other";

    private static void ValidateHousing(IEnumerable<AgentState> physical, SocietyCheckpoint society, int schemaVersion)
    {
        var people = society.Agents.ToDictionary(person => person.Id, StringComparer.Ordinal);
        var households = society.Households.Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var person in physical)
        {
            if (person.Housing is not { } housing)
                continue;
            if (schemaVersion < HousingSchemaVersion)
                throw new InvalidDataException($"Housing requests require world schema {HousingSchemaVersion}.");
            if (housing.Request is null && housing.Refusals is not { Count: > 0 } && housing.Blocker is null &&
                housing.Relocation is null ||
                housing.Blocker is not null && !HousingBlockers.All.Contains(housing.Blocker, StringComparer.Ordinal) ||
                housing.Refusals is { } refusals && (refusals.Any(refusal => refusal is null ||
                    !households.Contains(refusal.HouseholdId) || refusal.Tick < 0 || refusal.Tick > society.WorldTick) ||
                    refusals.Select(refusal => refusal.HouseholdId).Distinct(StringComparer.Ordinal).Count() != refusals.Count))
                throw new InvalidDataException("The saved housing state is invalid.");
            if (housing.Relocation is { } notice)
                ValidateRelocation(person, notice, people, society, schemaVersion);
            var hasAdultAdmissionState = housing.Request is not null || housing.Refusals is { Count: > 0 };
            var isAdultResident = people.TryGetValue(person.AgentId, out var societyPerson) &&
                societyPerson.Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(societyPerson.AgeBand);
            if (hasAdultAdmissionState && !isAdultResident ||
                housing.Blocker == HousingBlockers.AwaitingAnswer && housing.Request is null)
                throw new InvalidDataException("Saved household admission state is only valid for an adult applicant.");
            if (housing.Request is not { } request)
                continue;
            var answers = request.Approvals is null || request.Rejections is null ? null : request.Approvals.Concat(request.Rejections).ToArray();
            // A member asks only while holding a move-out notice from their current House.
            if (!people.TryGetValue(person.AgentId, out var applicant) || applicant.HouseholdId == request.HouseholdId ||
                applicant.HouseholdId is not null && housing.Relocation?.HouseholdId != applicant.HouseholdId ||
                !households.Contains(request.HouseholdId) || request.RequestedTick < 0 || request.RequestedTick > society.WorldTick ||
                request.ExpiryTick - request.RequestedTick != HousingRequestTicks ||
                request.Members is null || answers is null || request.Members.Count == 0 ||
                request.Members.Distinct(StringComparer.Ordinal).Count() != request.Members.Count ||
                request.Members.Any(id => string.IsNullOrWhiteSpace(id) || !people.ContainsKey(id) || id == person.AgentId) ||
                answers.Distinct(StringComparer.Ordinal).Count() != answers.Length ||
                answers.Any(id => !request.Members.Contains(id, StringComparer.Ordinal)))
                throw new InvalidDataException("The saved housing request is invalid.");
        }
    }
}
