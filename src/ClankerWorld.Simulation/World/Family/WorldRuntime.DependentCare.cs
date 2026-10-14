using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private long GuardianStageTicks => Math.Max(1, worldSystems.Config.TicksPerDay);

    private bool NeedsCaregiver(string child) => agents.ContainsKey(child) &&
        society.Checkpoint.GetAgent(child).AgeBand is SocietyAgeBand.Infant or SocietyAgeBand.Child or SocietyAgeBand.Adolescent &&
        !SocietyRules.HasActivePrimaryCaregiver(society.Checkpoint, child);
    private bool CanAcceptGuardian(string adult, string child) => IsAdult(adult) &&
        ReadyForBriefInteraction(adult) &&
        NeedsCaregiver(child) && agents[child].GuardianSearch is { } search &&
            search.OfferedAdultIds.Contains(adult, StringComparer.Ordinal) &&
            GuardianAdultsForStage(child, search.Stage).Contains(adult, StringComparer.Ordinal);

    private bool EligibleCaregiver(string adult, string child) => IsAdult(adult) && NeedsCaregiver(child) &&
        society.Checkpoint.GetAgent(adult).HouseholdId is { } household &&
        society.Checkpoint.GetAgent(child).HouseholdId == household;

    private IEnumerable<SocietyRelationship> CareProposals() => society.Checkpoint.Relationships.Where(edge =>
        edge.Type == SocietyRelationshipType.Caregiver && edge.State == SocietyRelationshipState.Proposed &&
        edge.Id.StartsWith("care-relationship:", StringComparison.Ordinal));

    private IEnumerable<string> IllDependentsNeedingCare(string adult) => society.Checkpoint.Relationships
        .Where(edge => edge.Type == SocietyRelationshipType.Caregiver && edge.ProposerId == adult &&
            edge.State == SocietyRelationshipState.Accepted && edge.EffectiveTick <= WorldTick &&
            agents.TryGetValue(edge.TargetId, out var recipient) &&
            society.Checkpoint.GetAgent(edge.TargetId).AgeBand != SocietyAgeBand.Infant &&
            recipient.Survival is { IllnessBasisPoints: >= 2_500 } && !RecentlyCaredForIllness(edge.TargetId))
        .Select(edge => edge.TargetId);

    private bool CanOfferCare(string adult, string child) => EligibleCaregiver(adult, child) &&
        agents[child].GuardianSearch is null &&
        !CareProposals().Any(edge => edge.TargetId == child) &&
        !society.Checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted) &&
        !society.Checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && WorldTick - Math.Max(edge.ProposedTick, edge.EffectiveTick) < worldSystems.Config.TicksPerDay);

    private bool CanAssumePrimaryCare(string adult, string child) => IsAdult(adult) && NeedsCaregiver(child) &&
        agents[child].GuardianSearch is null &&
        society.Checkpoint.GetAgent(adult).HouseholdId is { } household &&
        society.Checkpoint.GetAgent(child).HouseholdId == household &&
        society.Checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);

    private bool HasDependentCareDecision(string actor) => ReadyForBriefInteraction(actor) &&
        (CareProposals().Any(edge => edge.TargetId == actor) ||
         agents.Keys.Any(child => CanOfferCare(actor, child) || CanAssumePrimaryCare(actor, child) ||
             CanAcceptGuardian(actor, child)));

    private void MaintainDependentCare()
    {
        foreach (var edge in CareProposals().ToArray())
        {
            if (!EligibleCaregiver(edge.ProposerId, edge.TargetId) || WorldTick - edge.ProposedTick > 120)
            {
                society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, edge.Id, edge.ProposerId));
                AppendEvent(new CaregiverProposalExpiredEventPayload(edge.TargetId, edge.TargetId));
            }
        }

        foreach (var child in agents.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            if (!NeedsCaregiver(child))
            {
                if (agents[child].GuardianSearch is not null)
                    SetGuardianSearch(child, null);
                continue;
            }

            var search = agents[child].GuardianSearch;
            if (search is null)
            {
                search = NewGuardianSearch(child, WorldTick);
                SetGuardianSearch(child, search);
                AppendEvent(new GuardianNeededEventPayload(child));
                continue;
            }

            search = RefreshedGuardianSearch(child, search, WorldTick - search.StageStartedTick >= GuardianStageTicks);
            if (search != agents[child].GuardianSearch)
                SetGuardianSearch(child, search);
        }
    }

    /// <summary>
    /// Decisions later in the tick can change who lives where, so open searches are brought up to date
    /// before the tick ends and the saved offers always match the world they were saved with.
    /// </summary>
    private void SettleGuardianSearches()
    {
        foreach (var child in agents.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            if (agents[child].GuardianSearch is not { } search) continue;
            if (!NeedsCaregiver(child))
            {
                SetGuardianSearch(child, null);
                continue;
            }
            var settled = RefreshedGuardianSearch(child, search, stageExpired: false);
            if (settled != search)
                SetGuardianSearch(child, settled);
        }
    }

    private GuardianSearchState RefreshedGuardianSearch(string child, GuardianSearchState search, bool stageExpired)
    {
        var stageAdults = GuardianAdultsForStage(child, search.Stage);
        if (search.Stage != "town" && (stageAdults.Length == 0 || stageExpired))
            return NextGuardianStage(child, search);
        return search.OfferedAdultIds.SequenceEqual(stageAdults, StringComparer.Ordinal)
            ? search
            : search with { OfferedAdultIds = stageAdults };
    }

    private GuardianSearchState NewGuardianSearch(string child, long tick)
    {
        foreach (var stage in new[] { "relatives", "household", "town" })
        {
            if (stage == "town" || NewGuardianAdultsForStage(society.Checkpoint, towns, child, stage).Length > 0)
                return new(stage, tick, tick, GuardianAdultsForStage(child, stage));
        }
        throw new InvalidOperationException("A guardian search must have a final Town stage.");
    }

    private GuardianSearchState NextGuardianStage(string child, GuardianSearchState previous)
    {
        var nextStage = previous.Stage switch
        {
            "relatives" => "household",
            "household" => "town",
            _ => "town",
        };
        // A stage that would ask nobody new is skipped rather than waited out.
        if (nextStage != "town" && NewGuardianAdultsForStage(society.Checkpoint, towns, child, nextStage).Length == 0)
            nextStage = "town";
        return new(nextStage, previous.StartedTick, WorldTick, GuardianAdultsForStage(child, nextStage));
    }

    private string[] GuardianAdultsForStage(string child, string stage)
    {
        return GuardianAdultsForStage(society.Checkpoint, towns, child, stage);
    }

    /// <summary>
    /// The search widens: each stage adds a new group and keeps asking the earlier ones, so a relative
    /// who missed their first day can still accept later.
    /// </summary>
    private static string[] GuardianAdultsForStage(
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        string child,
        string stage)
    {
        var stages = stage switch
        {
            "relatives" => new[] { "relatives" },
            "household" => ["relatives", "household"],
            "town" => ["relatives", "household", "town"],
            _ => throw new InvalidDataException("The saved guardian search stage is invalid."),
        };
        return stages.SelectMany(item => NewGuardianAdultsForStage(checkpoint, towns, child, item))
            .Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>The adults a stage adds to the search; nobody belongs to more than one stage.</summary>
    private static string[] NewGuardianAdultsForStage(
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        string child,
        string stage)
    {
        var relatives = LivingAdultRelatives(checkpoint, child).ToHashSet(StringComparer.Ordinal);
        var householdId = checkpoint.GetAgent(child).HouseholdId;
        var householdAdults = householdId is null ? [] : checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
                person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder &&
                person.HouseholdId == householdId)
            .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        return stage switch
        {
            "relatives" => relatives.Order(StringComparer.Ordinal).ToArray(),
            "household" => householdAdults.Where(id => !relatives.Contains(id)).ToArray(),
            "town" => TownAdultsFor(checkpoint, towns, child).Where(id => !relatives.Contains(id) &&
                !householdAdults.Contains(id, StringComparer.Ordinal)).ToArray(),
            _ => throw new InvalidDataException("The saved guardian search stage is invalid."),
        };
    }

    private string[] TownAdultsFor(string child) => TownAdultsFor(society.Checkpoint, towns, child);

    private static string[] TownAdultsFor(SocietyCheckpoint checkpoint, IReadOnlyList<TownRuntimeState> towns, string child)
    {
        var townId = towns.SingleOrDefault(item => item.ResidentIds.Contains(child, StringComparer.Ordinal))?.Id;
        if (townId is null)
            return [];
        return towns.Single(town => town.Id == townId).ResidentIds
            .Where(id => id != child && checkpoint.Agents.Any(person => person.Id == id &&
                person.Status == SocietyAgentStatus.Active &&
                person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private string[] LivingAdultRelatives(string child) => LivingAdultRelatives(society.Checkpoint, child);

    private static string[] LivingAdultRelatives(SocietyCheckpoint checkpoint, string child)
    {
        var parentage = checkpoint.Relationships.Where(edge =>
                edge.Type == SocietyRelationshipType.BiologicalParentage &&
                edge.State is not (SocietyRelationshipState.Proposed or SocietyRelationshipState.Rejected))
            .ToArray();
        var parents = parentage.Where(edge => edge.TargetId == child)
            .Select(edge => edge.ProposerId).ToHashSet(StringComparer.Ordinal);
        var grandparents = parentage.Where(edge => parents.Contains(edge.TargetId))
            .Select(edge => edge.ProposerId).ToHashSet(StringComparer.Ordinal);
        var adultSiblings = parentage.Where(edge => parents.Contains(edge.ProposerId))
            .Select(edge => edge.TargetId);
        var auntsAndUncles = parentage.Where(edge => grandparents.Contains(edge.ProposerId))
            .Select(edge => edge.TargetId).Where(id => !parents.Contains(id));
        return parents.Concat(grandparents).Concat(adultSiblings).Concat(auntsAndUncles)
            .Where(id => id != child && checkpoint.Agents.Any(person => person.Id == id &&
                person.Status == SocietyAgentStatus.Active &&
                person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private void SetGuardianSearch(string child, GuardianSearchState? search)
    {
        agents[child] = agents[child] with { GuardianSearch = search };
        checkpointSchemaVersion = StateSchemaVersion;
    }

    private void AddDependentCareCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!ReadyForBriefInteraction(actor)) return;
        foreach (var edge in society.Checkpoint.Relationships.Where(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                     (edge.ProposerId == actor || edge.TargetId == actor) &&
                     (edge.State == SocietyRelationshipState.Accepted || edge.State == SocietyRelationshipState.Proposed && edge.ProposerId == actor)))
        {
            candidates.Add(new("guardian_end:" + edge.Id, "Withdraw from this caregiving relationship or proposal.", 110));
        }
        foreach (var dependent in IllDependentsNeedingCare(actor).Where(dependent =>
                     !NeedsUrgentFood(agents[actor]) || IsWithinInteractionRange(agents[actor].Position, agents[dependent].Position, ResourceInteractionRange)))
        {
            candidates.Add(new("guardian_tend:" + dependent,
                "Offer warmth and practical care to your ill dependent.", 75));
        }
        foreach (var child in agents.Keys.Order(StringComparer.Ordinal).Where(child => CanAcceptGuardian(actor, child)))
        {
            if (candidates.Any(candidate => candidate.Id == "guardian_accept:" + child))
                continue;
            candidates.Add(new("guardian_accept:" + child,
                $"Accept primary care of {society.Checkpoint.GetAgent(child).Name}. If your House has room, accompany them there when a physical move is needed.", 3));
        }
        foreach (var edge in CareProposals().Where(edge => edge.TargetId == actor && EligibleCaregiver(edge.ProposerId, actor)))
        {
            candidates.Add(new("guardian_accept:" + edge.Id, $"Accept care from {society.Checkpoint.GetAgent(edge.ProposerId).Name}.", 3));
            candidates.Add(new("guardian_refuse:" + edge.Id, "Refuse this caregiver proposal.", 70));
        }
        foreach (var child in agents.Keys.Order(StringComparer.Ordinal).Where(child => CanOfferCare(actor, child)))
        {
            candidates.Add(new("guardian_offer:" + child,
                $"Offer to care for {society.Checkpoint.GetAgent(child).Name}, who has no surviving active caregiver. Older dependents may refuse.", 4));
        }
        foreach (var child in agents.Keys.Order(StringComparer.Ordinal).Where(child => CanAssumePrimaryCare(actor, child)))
        {
            candidates.Add(new("guardian_primary:" + child,
                $"Become {society.Checkpoint.GetAgent(child).Name}'s primary caregiver. The former primary caregiver is unavailable, and this child already recognizes you as a caregiver.", 4));
        }
    }

    private void ApplyDependentCareCandidate(string actor, string candidate)
    {
        var target = candidate[(candidate.IndexOf(':', StringComparison.Ordinal) + 1)..];
        if (candidate.StartsWith("guardian_tend:", StringComparison.Ordinal))
        {
            TendToIllDependent(actor, target);
            return;
        }
        if (candidate.StartsWith("guardian_accept:", StringComparison.Ordinal))
        {
            if (agents.ContainsKey(target) && NeedsCaregiver(target))
            {
                AcceptGuardian(actor, target, CanAcceptGuardian(actor, target));
                return;
            }
            var edge = CareProposals().FirstOrDefault(edge => edge.Id == target && edge.TargetId == actor);
            if (edge is not null && EligibleCaregiver(edge.ProposerId, actor))
            {
                society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, edge.Id, edge.Revision, actor));
                AppendEvent(new CaregiverAcceptedEventPayload(actor));
            }
            return;
        }
        if (candidate.StartsWith("guardian_end:", StringComparison.Ordinal))
        {
            var existing = society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Id == target &&
                edge.Type == SocietyRelationshipType.Caregiver && (edge.ProposerId == actor || edge.TargetId == actor) &&
                (edge.State == SocietyRelationshipState.Accepted || edge.State == SocietyRelationshipState.Proposed && edge.ProposerId == actor));
            if (existing is null) return;
            society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, target, actor));
            AppendEvent(new CaregiverEndedEventPayload(actor));
            ReconcileGuardianPlacements();
            return;
        }
        if (candidate.StartsWith("guardian_primary:", StringComparison.Ordinal))
        {
            if (!CanAssumePrimaryCare(actor, target)) return;
            var result = society.Apply(checkpoint => SocietyRules.AssumePrimaryCare(checkpoint, actor, target));
            if (result.NewEvents?.Any(item => item.Kind == "primary_caregiver_assumed") == true)
                AppendEvent(new PrimaryCaregiverAssignedEventPayload(target));
            ReconcileGuardianPlacements();
            return;
        }
        if (candidate.StartsWith("guardian_offer:", StringComparison.Ordinal))
        {
            if (!CanOfferCare(actor, target)) return;
            if (society.Checkpoint.GetAgent(target).AgeBand == SocietyAgeBand.Infant)
            {
                var result = society.Apply(checkpoint => SocietyRules.AssumeInfantCare(checkpoint, actor, target));
                if (result.CreatedId is not null) AppendEvent(new CaregiverAssignedEventPayload(target));
            }
            else
            {
                society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
                    new(NewCareRelationshipId(actor, target, WorldTick), 1, SocietyRelationshipType.Caregiver,
                        actor, target, WorldTick, PrivacyClass: "public", HouseholdId: HouseholdFor(actor))));
                AppendEvent(new CaregiverProposedEventPayload(target));
            }
            return;
        }
        var proposal = CareProposals().FirstOrDefault(edge => edge.Id == target && edge.TargetId == actor);
        if (proposal is null || !EligibleCaregiver(proposal.ProposerId, actor)) return;
        if (candidate.StartsWith("guardian_refuse:", StringComparison.Ordinal))
        {
            society.Apply(checkpoint => SocietyRules.RefuseRelationship(checkpoint, proposal.Id, proposal.Revision, actor));
            AppendEvent(new CaregiverRefusedEventPayload(actor));
        }
    }

    private void AcceptGuardian(string adult, string child, bool authorized)
    {
        if (!authorized || !IsAdult(adult) || !NeedsCaregiver(child)) return;
        var adultHousehold = society.Checkpoint.GetAgent(adult).HouseholdId;
        var adultTown = TownForResident(adult);
        var childTown = TownForResident(child);
        var destination = adultHousehold is not null && adultHousehold != society.Checkpoint.GetAgent(child).HouseholdId &&
            adultTown is not null && adultTown == childTown &&
            CanFitGuardianHousehold(adultHousehold, adult, child)
            ? adultHousehold : null;
        var result = society.Apply(checkpoint => SocietyRules.AcceptDependentGuardianship(
            checkpoint, adult, child, destination));
        if (result.NewEvents?.Any(item => item.Kind == "dependent_guardian_accepted") != true ||
            !SocietyRules.HasActivePrimaryCaregiver(society.Checkpoint, child))
            return;
        SetGuardianSearch(child, null);
        BeginGuardianPlacement(child, adult, result.CreatedId!);
        SetHousing(child, (agents[child].Housing ?? new()) with { Blocker = HousingBlocker(child) });
        AppendEvent(new GuardianAssignedEventPayload(child));
    }

    private static void ValidateDependentCare(
        IEnumerable<AgentState> physical,
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        int schemaVersion)
    {
        var people = checkpoint.Agents.ToDictionary(person => person.Id, StringComparer.Ordinal);
        foreach (var state in physical)
        {
            if (state.GuardianSearch is not { } search) continue;
            if (schemaVersion < DependentGuardianSearchSchemaVersion || !people.TryGetValue(state.AgentId, out var child) ||
                child.Status != SocietyAgentStatus.Active ||
                child.AgeBand is not (SocietyAgeBand.Infant or SocietyAgeBand.Child or SocietyAgeBand.Adolescent) ||
                SocietyRules.HasActivePrimaryCaregiver(checkpoint, child.Id) ||
                search.Stage is not ("relatives" or "household" or "town") ||
                search.StartedTick < 0 || search.StartedTick > checkpoint.WorldTick ||
                search.StageStartedTick < search.StartedTick || search.StageStartedTick > checkpoint.WorldTick ||
                search.OfferedAdultIds is null ||
                search.OfferedAdultIds.Any(id => string.IsNullOrWhiteSpace(id) || !people.TryGetValue(id, out var adult) ||
                    adult.Status != SocietyAgentStatus.Active || adult.AgeBand is not (SocietyAgeBand.Adult or SocietyAgeBand.Elder)) ||
                search.OfferedAdultIds.Distinct(StringComparer.Ordinal).Count() != search.OfferedAdultIds.Count ||
                !search.OfferedAdultIds.SequenceEqual(search.OfferedAdultIds.Order(StringComparer.Ordinal)) ||
                !search.OfferedAdultIds.SequenceEqual(GuardianAdultsForStage(checkpoint, towns, child.Id, search.Stage), StringComparer.Ordinal) ||
                search.Stage != "town" && search.OfferedAdultIds.Count == 0)
                throw new InvalidDataException("The saved dependent guardian search is invalid.");
        }
    }

    private bool CanFitGuardianHousehold(string householdId, string adult, string child) =>
        HouseResidentCapacity(householdId, society.Checkpoint.GetAgent(child) with
        {
            DomesticFamilyUnitId = society.Checkpoint.GetAgent(adult).DomesticFamilyUnitId,
        }) is { IsOvercrowded: false };

    private bool CanTendToIllDependent(string adult, string dependent) => IsAdult(adult) &&
        IllDependentsNeedingCare(adult).Contains(dependent, StringComparer.Ordinal);

    private void TendToIllDependent(string adult, string dependent)
    {
        if (!ReadyForBriefInteraction(adult) || !CanTendToIllDependent(adult, dependent)) return;
        var caregiver = agents[adult];
        var recipient = agents[dependent];
        if (NeedsUrgentFood(caregiver) && !IsWithinInteractionRange(caregiver.Position, recipient.Position, ResourceInteractionRange)) return;
        if (!IsWithinInteractionRange(caregiver.Position, recipient.Position, ResourceInteractionRange))
        {
            MoveToward(adult, caregiver, recipient.Position, "care", ResourceInteractionRange);
            return;
        }
        recipient = recipient with
        {
            Survival = recipient.Survival! with
            {
                WarmthBasisPoints = Math.Min(10_000, recipient.Survival.WarmthBasisPoints + 1_000),
                IllnessBasisPoints = Math.Max(0, recipient.Survival.IllnessBasisPoints - IllnessCareReliefBasisPoints),
            },
        };
        agents[dependent] = recipient;
        AppendEvent(new DependentCaredForEventPayload(dependent));
    }
}
