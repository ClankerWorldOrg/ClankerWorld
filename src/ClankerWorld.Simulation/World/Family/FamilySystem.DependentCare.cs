using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static ActorCapabilityPolicy DependentCareCapabilities(DependentCareActionPlan plan) =>
        new(plan.AcceptsGuardianSearch || plan.Choice.Kind is DependentCareChoiceKind.Offer or DependentCareChoiceKind.Primary
            ? ActionLifeStages.Adults : ActionLifeStages.NonInfants,
            AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: plan.Choice.Kind == DependentCareChoiceKind.End, AllowActiveOrder: true);

    internal static bool NeedsCaregiver(IFamilyCareWorld world, string child) => world.PhysicalAgents.Living.ContainsKey(child) &&
        LifeStageRules.IsDependent(world.Society.GetAgent(child).AgeBand) &&
        !SocietyRules.HasActivePrimaryCaregiver(world.Society, child);

    internal static bool CanAcceptGuardian(IFamilyCareWorld world, string adult, string child) => world.IsAdult(adult) &&
        world.ReadyForBriefInteraction(adult) &&
        NeedsCaregiver(world, child) && world.PhysicalAgents.Living[child].GuardianSearch is { } search &&
            search.OfferedAdultIds.Contains(adult, StringComparer.Ordinal) &&
            world.GuardianAdultsForStage(child, search.Stage).Contains(adult, StringComparer.Ordinal);

    internal static bool EligibleCaregiver(IFamilyCareWorld world, string adult, string child) => world.IsAdult(adult) && NeedsCaregiver(world, child) &&
        world.Society.GetAgent(adult).HouseholdId is { } household &&
        world.Society.GetAgent(child).HouseholdId == household;

    internal static IEnumerable<SocietyRelationship> CareProposals(IFamilyCareWorld world) => world.Society.Relationships.Where(edge =>
        edge.Type == SocietyRelationshipType.Caregiver && edge.State == SocietyRelationshipState.Proposed &&
        edge.Id.StartsWith("care-relationship:", StringComparison.Ordinal));

    internal static IEnumerable<string> IllDependentsNeedingCare(IFamilyCareWorld world, string adult) => world.Society.Relationships
        .Where(edge => edge.Type == SocietyRelationshipType.Caregiver && edge.ProposerId == adult &&
            edge.State == SocietyRelationshipState.Accepted && edge.EffectiveTick <= world.WorldTick &&
            world.PhysicalAgents.Living.TryGetValue(edge.TargetId, out var recipient) &&
            !LifeStageRules.IsInfant(world.Society.GetAgent(edge.TargetId).AgeBand) &&
            recipient.Survival is { IllnessBasisPoints: >= NeedThresholds.MedicalCareIllness } && !world.RecentlyCaredForIllness(edge.TargetId))
        .Select(edge => edge.TargetId);

    internal static bool CanOfferCare(IFamilyCareWorld world, string adult, string child) => EligibleCaregiver(world, adult, child) &&
        world.PhysicalAgents.Living[child].GuardianSearch is null &&
        !CareProposals(world).Any(edge => edge.TargetId == child) &&
        !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted) &&
        !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && world.WorldTick - Math.Max(edge.ProposedTick, edge.EffectiveTick) < world.TicksPerDay);

    internal static bool CanAssumePrimaryCare(IFamilyCareWorld world, string adult, string child) => world.IsAdult(adult) && NeedsCaregiver(world, child) &&
        world.PhysicalAgents.Living[child].GuardianSearch is null &&
        world.Society.GetAgent(adult).HouseholdId is { } household &&
        world.Society.GetAgent(child).HouseholdId == household &&
        world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);

    internal static bool HasDependentCareDecision(IFamilyCareWorld world, string actor) => world.ReadyForBriefInteraction(actor) &&
        (CareProposals(world).Any(edge => edge.TargetId == actor) ||
         world.PhysicalAgents.Living.Keys.Any(child => CanOfferCare(world, actor, child) || CanAssumePrimaryCare(world, actor, child) ||
             CanAcceptGuardian(world, actor, child)));

    internal static void MaintainDependentCare(IFamilyCareWorld world)
    {
        foreach (var edge in CareProposals(world).ToArray())
        {
            if (!EligibleCaregiver(world, edge.ProposerId, edge.TargetId) || world.WorldTick - edge.ProposedTick > 120)
            {
                world.RevokeCare(edge.Id, edge.ProposerId);
                world.AppendEvent(new CaregiverProposalExpiredEventPayload(edge.TargetId, edge.TargetId));
            }
        }

        foreach (var child in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            if (!NeedsCaregiver(world, child))
            {
                if (world.PhysicalAgents.Living[child].GuardianSearch is not null)
                    SetGuardianSearch(world, child, null);
                continue;
            }

            var search = world.PhysicalAgents.Living[child].GuardianSearch;
            if (search is null)
            {
                search = NewGuardianSearch(world, child, world.WorldTick);
                SetGuardianSearch(world, child, search);
                world.AppendEvent(new GuardianNeededEventPayload(child));
                continue;
            }

            search = RefreshedGuardianSearch(world, child, search, world.WorldTick - search.StageStartedTick >= world.GuardianStageTicks);
            if (search != world.PhysicalAgents.Living[child].GuardianSearch)
                SetGuardianSearch(world, child, search);
        }
    }

        /// <summary>
    /// Decisions later in the tick can change who lives where, so open searches are brought up to date
    /// before the tick ends and the saved offers always match the world they were saved with.
    /// </summary>
    internal static void SettleGuardianSearches(IFamilyCareWorld world)
    {
        foreach (var child in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            if (world.PhysicalAgents.Living[child].GuardianSearch is not { } search) continue;
            if (!NeedsCaregiver(world, child))
            {
                SetGuardianSearch(world, child, null);
                continue;
            }
            var settled = RefreshedGuardianSearch(world, child, search, stageExpired: false);
            if (settled != search)
                SetGuardianSearch(world, child, settled);
        }
    }

    internal static GuardianSearchState RefreshedGuardianSearch(IFamilyCareWorld world, string child, GuardianSearchState search, bool stageExpired)
    {
        var stageAdults = world.GuardianAdultsForStage(child, search.Stage);
        if (search.Stage != "town" && (stageAdults.Length == 0 || stageExpired))
            return NextGuardianStage(world, child, search);
        return search.OfferedAdultIds.SequenceEqual(stageAdults, StringComparer.Ordinal)
            ? search
            : search with { OfferedAdultIds = stageAdults };
    }

    internal static GuardianSearchState NewGuardianSearch(IFamilyCareWorld world, string child, long tick)
    {
        foreach (var stage in new[] { "relatives", "household", "town" })
        {
            if (stage == "town" || world.NewGuardianAdultsForStage(child, stage).Length != 0)
                return new(stage, tick, tick, world.GuardianAdultsForStage(child, stage));
        }
        throw new InvalidOperationException("A guardian search must have a final Town stage.");
    }

    internal static GuardianSearchState NextGuardianStage(IFamilyCareWorld world, string child, GuardianSearchState previous)
    {
        var nextStage = previous.Stage switch
        {
            "relatives" => "household",
            "household" => "town",
            _ => "town",
        };
        // A stage that would ask nobody new is skipped rather than waited out.
        if (nextStage != "town" && world.NewGuardianAdultsForStage(child, nextStage).Length == 0)
            nextStage = "town";
        return new(nextStage, previous.StartedTick, world.WorldTick, world.GuardianAdultsForStage(child, nextStage));
    }

    internal static void SetGuardianSearch(IFamilyCareWorld world, string child, GuardianSearchState? search)
    {
        world.PhysicalAgents.RecordGuardianSearch(child, world.PhysicalAgents.Living[child], search);
        world.MarkCurrentSchema();
    }

    internal static void AddDependentCareOptions(IFamilyCareWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (!world.ReadyForBriefInteraction(actor)) return;
        foreach (var edge in world.Society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                     (edge.ProposerId == actor || edge.TargetId == actor) &&
                     (edge.State == SocietyRelationshipState.Accepted || edge.State == SocietyRelationshipState.Proposed && edge.ProposerId == actor)))
        {
            candidates.Add(new(new DependentCareChoice(DependentCareChoiceKind.End, edge.Id).Encode(), "Withdraw from this caregiving relationship or proposal.", 110));
        }
        foreach (var dependent in IllDependentsNeedingCare(world, actor).Where(dependent =>
                     !world.NeedsUrgentFood(world.PhysicalAgents.Living[actor]) || world.IsWithinInteractionRange(world.PhysicalAgents.Living[actor].Position, world.PhysicalAgents.Living[dependent].Position, world.ResourceInteractionRange)))
        {
            if (world.OfferPracticalCare(actor, new(PracticalCareKind.IllDependent, dependent)) is { } care)
                candidates.Add(new CognitionCandidate(care.CandidateId, "Offer warmth and practical care to your ill dependent.", 75) { ActionPlan = care });
        }
        foreach (var child in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).Where(child => CanAcceptGuardian(world, actor, child)))
        {
            if (candidates.Any(candidate => candidate.Id == new DependentCareChoice(DependentCareChoiceKind.Accept, child).Encode()))
                continue;
            candidates.Add(new(new DependentCareChoice(DependentCareChoiceKind.Accept, child).Encode(),
                $"Accept primary care of {world.Society.GetAgent(child).Name}. If your House has room, accompany them there when a physical move is needed.", 3));
        }
        foreach (var edge in CareProposals(world).Where(edge => edge.TargetId == actor && EligibleCaregiver(world, edge.ProposerId, actor)))
        {
            candidates.Add(new(new DependentCareChoice(DependentCareChoiceKind.Accept, edge.Id).Encode(), $"Accept care from {world.Society.GetAgent(edge.ProposerId).Name}.", 3));
            candidates.Add(new(new DependentCareChoice(DependentCareChoiceKind.Refuse, edge.Id).Encode(), "Refuse this caregiver proposal.", 70));
        }
        foreach (var child in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).Where(child => CanOfferCare(world, actor, child)))
        {
            candidates.Add(new(new DependentCareChoice(DependentCareChoiceKind.Offer, child).Encode(),
                $"Offer to care for {world.Society.GetAgent(child).Name}, who has no surviving active caregiver. Older dependents may refuse.", 4));
        }
        foreach (var child in world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).Where(child => CanAssumePrimaryCare(world, actor, child)))
        {
            candidates.Add(new(new DependentCareChoice(DependentCareChoiceKind.Primary, child).Encode(),
                $"Become {world.Society.GetAgent(child).Name}'s primary caregiver. The former primary caregiver is unavailable, and this child already recognizes you as a caregiver.", 4));
        }
    }

    internal static void CompleteDependentCareChoice(IFamilyCareWorld world, DependentCareActionPlan plan)
    {
        var actor = plan.Actor;
        var target = plan.Choice.TargetId;
        if (plan.AcceptsGuardianSearch) { AcceptGuardian(world, actor, target, true, plan); return; }
        switch (plan.Choice.Kind)
        {
            case DependentCareChoiceKind.Accept:
                var edge = plan.Relationship!;
                world.AcceptCare(edge.Id, edge.Revision, actor);
                world.AppendEvent(new CaregiverAcceptedEventPayload(actor));
                break;
            case DependentCareChoiceKind.End:
                world.RevokeCare(target, actor);
                world.AppendEvent(new CaregiverEndedEventPayload(actor));
                world.ReconcileGuardianPlacements();
                break;
            case DependentCareChoiceKind.Primary:
                var result = world.AssumePrimaryCare(actor, target);
                if (result.NewEvents?.Any(item => item.Kind == "primary_caregiver_assumed") == true)
                    world.AppendEvent(new PrimaryCaregiverAssignedEventPayload(target));
                world.ReconcileGuardianPlacements();
                break;
            case DependentCareChoiceKind.Offer:
                if (LifeStageRules.IsInfant(world.Society.GetAgent(target).AgeBand))
                {
                    var assigned = world.AssumeInfantCare(actor, target);
                    if (assigned.CreatedId is not null) world.AppendEvent(new CaregiverAssignedEventPayload(target));
                }
                else
                {
                    world.ProposeCare(new(world.NewCareRelationshipId(actor, target, world.WorldTick), 1, SocietyRelationshipType.Caregiver,
                            actor, target, world.WorldTick, PrivacyClass: "public", HouseholdId: world.HouseholdFor(actor)));
                    world.AppendEvent(new CaregiverProposedEventPayload(target));
                }
                break;
            case DependentCareChoiceKind.Refuse:
                var proposal = plan.Relationship!;
                world.RefuseCare(proposal.Id, proposal.Revision, actor);
                world.AppendEvent(new CaregiverRefusedEventPayload(actor));
                break;
        }
    }

    internal static void AcceptGuardian(IFamilyCareWorld world, string adult, string child, bool authorized, DependentCareActionPlan? originalPlan = null)
    {
        if (!authorized || !world.IsAdult(adult) || !NeedsCaregiver(world, child)) return;
        var destination = originalPlan is not null ? originalPlan.DestinationHousehold : ImmediateGuardianHousehold(world, adult, child);
        var result = world.AcceptDependentGuardianship(adult, child, destination);
        if (result.NewEvents?.Any(item => item.Kind == "dependent_guardian_accepted") != true ||
            !SocietyRules.HasActivePrimaryCaregiver(world.Society, child))
            return;
        SetGuardianSearch(world, child, null);
        world.BeginGuardianPlacement(child, adult, result.CreatedId!);
        world.SetHousing(child, (world.PhysicalAgents.Living[child].Housing ?? new()) with { Blocker = world.HousingBlocker(child) });
        world.AppendEvent(new GuardianAssignedEventPayload(child));
    }

    internal static bool CanFitGuardianHousehold(IFamilyCareWorld world, string householdId, string adult, string child) =>
        world.HouseResidentCapacity(householdId, world.Society.GetAgent(child) with
        {
            DomesticFamilyUnitId = world.Society.GetAgent(adult).DomesticFamilyUnitId,
        }) is { IsOvercrowded: false };

    internal static bool CanTendToIllDependent(IFamilyCareWorld world, string adult, string dependent) => world.IsAdult(adult) &&
        IllDependentsNeedingCare(world, adult).Contains(dependent, StringComparer.Ordinal);

    internal static void CompleteIllDependentCare(IFamilyCareWorld world, string adult, string dependent)
    {
        var recipient = world.PhysicalAgents.Living[dependent];
        recipient = AgentStore.PrepareSurvival(recipient, recipient.Survival! with
        {
            WarmthBasisPoints = Math.Min(10_000, recipient.Survival.WarmthBasisPoints + 1_000),
            IllnessBasisPoints = Math.Max(0, recipient.Survival.IllnessBasisPoints - world.IllnessCareReliefBasisPoints),
        });
        world.PhysicalAgents.CompleteDependentCare(dependent, recipient);
        world.AppendEvent(new DependentCaredForEventPayload(dependent));
    }

    internal static string? ImmediateGuardianHousehold(IFamilyCareWorld world, string adult, string child)
    {
        var home = world.HouseholdFor(adult);
        var town = world.TownForResident(adult);
        return home is not null && home != world.HouseholdFor(child) && town is not null && town == world.TownForResident(child) &&
            CanFitGuardianHousehold(world, home, adult, child) ? home : null;
    }

    internal static DependentCareActionPlan? PlanDependentCare(IFamilyCareWorld world, string actor, DependentCareChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        SocietyRelationship? relationship = null;
        GuardianSearchState? search = null;
        var child = choice.TargetId;
        if (choice.Kind == DependentCareChoiceKind.Accept && world.PhysicalAgents.Living.TryGetValue(child, out var dependent) && NeedsCaregiver(world, child))
        {
            search = dependent.GuardianSearch;
            if (search is null) return null;
        }
        else if (choice.Kind is DependentCareChoiceKind.Accept or DependentCareChoiceKind.Refuse or DependentCareChoiceKind.End)
        {
            relationship = world.Society.Relationships.FirstOrDefault(edge => edge.Id == choice.TargetId && edge.Type == SocietyRelationshipType.Caregiver);
            if (relationship is null) return null;
            child = relationship.TargetId;
        }
        else if (choice.Kind == DependentCareChoiceKind.Primary)
            relationship = world.Society.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.State == SocietyRelationshipState.Accepted && edge.ProposerId == actor && edge.TargetId == child);
        if (choice.Kind != DependentCareChoiceKind.End && !world.PhysicalAgents.Living.ContainsKey(child)) return null;
        var home = world.HouseholdFor(actor);
        var house = search is not null && home is not null ? world.HouseholdBuildingWithTag(home, "house") : null;
        return new(actor, choice, relationship, home, world.PhysicalAgents.Living.ContainsKey(child) ? world.HouseholdFor(child) : null,
            search?.StartedTick, search is not null ? ImmediateGuardianHousehold(world, actor, child) : null,
            world.TownForResident(actor), world.PhysicalAgents.Living.ContainsKey(child) ? world.TownForResident(child) : null,
            house?.InstanceId, house?.DefinitionId, house?.PlacedTick,
            world.Society.GetAgent(actor).DomesticFamilyUnitId, world.PhysicalAgents.Living.ContainsKey(child) ? world.Society.GetAgent(child).DomesticFamilyUnitId : null);
    }

    internal static ActionCheck CheckDependentCarePlan(IFamilyCareWorld world, DependentCareActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? DependentCareCapabilities(plan));
        if (!capability.Allowed) return capability;
        if (plan.Relationship is { } original && !RelationshipPlan.Matches(world.Society.Relationships.FirstOrDefault(edge => edge.Id == original.Id), original))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiving relationship has changed");
        var child = plan.Relationship?.TargetId ?? plan.Choice.TargetId;
        if (plan.Choice.Kind != DependentCareChoiceKind.End && (!world.PhysicalAgents.Living.ContainsKey(child) ||
                world.HouseholdFor(plan.Actor) != plan.ActorHousehold || world.HouseholdFor(child) != plan.ChildHousehold ||
                world.Society.GetAgent(plan.Actor).DomesticFamilyUnitId != plan.ActorFamilyUnit ||
                world.Society.GetAgent(child).DomesticFamilyUnitId != plan.ChildFamilyUnit))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiver or child's household has changed");
        var house = plan.AcceptsGuardianSearch && plan.ActorHousehold is not null
            ? world.HouseholdBuildingWithTag(plan.ActorHousehold, "house") : null;
        bool allowed;
        if (plan.AcceptsGuardianSearch)
            allowed = CanAcceptGuardian(world, plan.Actor, child) && world.PhysicalAgents.Living[child].GuardianSearch?.StartedTick == plan.GuardianSearchStartedTick &&
                world.TownForResident(plan.Actor) == plan.ActorTown && world.TownForResident(child) == plan.ChildTown &&
                ImmediateGuardianHousehold(world, plan.Actor, child) == plan.DestinationHousehold &&
                house?.InstanceId == plan.HouseId && house?.DefinitionId == plan.HouseDefinitionId && house?.PlacedTick == plan.HousePlacedTick;
        else allowed = plan.Choice.Kind switch
        {
            DependentCareChoiceKind.Accept or DependentCareChoiceKind.Refuse => plan.Relationship is { State: SocietyRelationshipState.Proposed } proposal &&
                proposal.TargetId == plan.Actor && CareProposals(world).Any(edge => edge.Id == proposal.Id) && EligibleCaregiver(world, proposal.ProposerId, plan.Actor),
            DependentCareChoiceKind.End => plan.Relationship is { } edge && (edge.ProposerId == plan.Actor || edge.TargetId == plan.Actor) &&
                (edge.State == SocietyRelationshipState.Accepted || edge.State == SocietyRelationshipState.Proposed && edge.ProposerId == plan.Actor),
            DependentCareChoiceKind.Offer => CanOfferCare(world, plan.Actor, child),
            DependentCareChoiceKind.Primary => plan.Relationship is not null && CanAssumePrimaryCare(world, plan.Actor, child),
            _ => false,
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiving responsibility is no longer available");
    }
}
