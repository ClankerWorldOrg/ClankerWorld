using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static ActorCapabilityPolicy GuardianTravelCapabilities(GuardianTravelKind kind) =>
        new(kind == GuardianTravelKind.Follow ? ActionLifeStages.Dependents : ActionLifeStages.Adults,
            AllowBoat: true, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: false);

    internal static void BeginGuardianPlacement(IFamilyGuardianWorld world, string child, string caregiver, string careRelationshipId)
    {
        var person = world.Society.GetAgent(child);
        var adult = world.Society.GetAgent(caregiver);
        if (person.HouseholdId == adult.HouseholdId && world.TownForResident(child) == world.TownForResident(caregiver))
            return;
        var care = world.Society.GetRelationship(careRelationshipId);
        SetGuardianPlacement(world, child, new(caregiver, care.Id, care.Revision, world.WorldTick, "collecting"));
        RefreshGuardianPlacement(world, child);
        world.AppendEvent(new GuardianPlacementPendingEventPayload(child));
    }

    internal static void SetGuardianPlacement(IFamilyGuardianWorld world, string child, GuardianPlacementState? placement)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var physical) || physical.GuardianPlacement == placement) return;
        world.PhysicalAgents.RecordGuardianPlacement(child, physical, placement);
        world.MarkCurrentSchema();
    }

    internal static bool IsCurrentGuardianPlacement(IFamilyGuardianWorld world, string child, GuardianPlacementState placement) =>
        world.PhysicalAgents.Living.ContainsKey(child) && world.PhysicalAgents.Living.ContainsKey(placement.CaregiverId) &&
        world.Society.Agents.Any(person => person.Id == child &&
            person.Status == SocietyAgentStatus.Active &&
            LifeStageRules.IsDependent(person.AgeBand) &&
            person.PrimaryCaregiverId == placement.CaregiverId) &&
        world.Society.Agents.Any(person => person.Id == placement.CaregiverId &&
            person.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(person.AgeBand)) &&
        world.Society.Relationships.Any(edge => edge.Id == placement.CareRelationshipId &&
            edge.Revision == placement.CareRevision && edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.ProposerId == placement.CaregiverId && edge.TargetId == child);

    internal static void ReconcileGuardianPlacements(IFamilyGuardianWorld world)
    {
        foreach (var child in world.PhysicalAgents.Living.Values.Where(person => person.GuardianPlacement is not null)
                     .Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray())
            RefreshGuardianPlacement(world, child);
    }

    internal static void RefreshGuardianPlacement(IFamilyGuardianWorld world, string child)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var physical) || physical.GuardianPlacement is not { } placement) return;
        if (!IsCurrentGuardianPlacement(world, child, placement))
        {
            SetGuardianPlacement(world, child, null);
            world.AppendEvent(new GuardianPlacementCancelledEventPayload(child));
            return;
        }
        var household = world.Society.GetAgent(placement.CaregiverId).HouseholdId;
        var town = world.TownForResident(placement.CaregiverId);
        var house = household is null || town is null ? null : world.HouseForHousehold(household);
        var changedHome = placement.DestinationHouseholdId != household || placement.DestinationTownId != town ||
            placement.HouseId != house?.InstanceId || placement.HouseDefinitionId != house?.DefinitionId ||
            placement.HousePlacedTick != house?.PlacedTick || placement.HousePosition != house?.Position;
        var refreshed = placement with
        {
            DestinationHouseholdId = household,
            DestinationTownId = town,
            HouseId = house?.InstanceId,
            HouseDefinitionId = house?.DefinitionId,
            HousePlacedTick = house?.PlacedTick,
            HousePosition = house?.Position,
            Stage = changedHome || house is null ? "collecting" : placement.Stage,
        };
        var homeBlocker = GuardianPlacementHomeBlocker(world, child, refreshed);
        var activityBlocker = GuardianPlacementActivityBlocker(world, child, refreshed);
        refreshed = refreshed with
        {
            Blocker = homeBlocker ?? activityBlocker ??
                (changedHome || IsGuardianHomeBlocker(placement.Blocker) ? null : placement.Blocker),
        };
        SetGuardianPlacement(world, child, refreshed);
    }

    internal static bool IsGuardianHomeBlocker(string? blocker) => blocker is not null &&
        (blocker.StartsWith("The guardian needs", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian's House", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian's home", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian is meeting", StringComparison.Ordinal) ||
         blocker.StartsWith("The guardian is finishing", StringComparison.Ordinal) ||
         blocker.StartsWith("The dependent is meeting", StringComparison.Ordinal));

    internal static string? GuardianPlacementActivityBlocker(IFamilyGuardianWorld world, string child, GuardianPlacementState placement)
    {
        var adult = world.PhysicalAgents.Living[placement.CaregiverId];
        if (world.NeedsUrgentFood(adult) || world.NeedsUrgentWarmth(adult))
            return "The guardian is meeting urgent food or warmth needs.";
        if (world.PendingInstructionFor(placement.CaregiverId) is not null || world.IsConversationBusy(placement.CaregiverId))
            return "The guardian is finishing another activity.";
        var dependent = world.PhysicalAgents.Living[child];
        if (placement.Stage == "escorting" && (world.NeedsUrgentFood(dependent) || world.NeedsUrgentWarmth(dependent) ||
            world.PendingInstructionFor(child) is not null || world.IsConversationBusy(child)))
            return "The dependent is meeting needs or finishing another activity.";
        return null;
    }

    internal static string? GuardianPlacementHomeBlocker(IFamilyGuardianWorld world, string child, GuardianPlacementState placement)
    {
        if (placement.DestinationHouseholdId is null) return "The guardian needs a household before the child can move in.";
        if (placement.DestinationTownId is null) return "The guardian needs a recorded Town membership before the child can move in.";
        if (placement.HouseId is null || placement.HousePosition is null)
            return "The guardian needs a completed House before the child can move in.";
        if (world.Society.GetAgent(placement.CaregiverId).HouseholdId != placement.DestinationHouseholdId ||
            world.TownForResident(placement.CaregiverId) != placement.DestinationTownId ||
            !world.Buildings.Any(house => house.InstanceId == placement.HouseId &&
                house.DefinitionId == placement.HouseDefinitionId && house.HouseholdId == placement.DestinationHouseholdId &&
                house.PlacedTick == placement.HousePlacedTick && house.Position == placement.HousePosition))
            return "The guardian's home changed; the journey needs a current destination.";
        return world.CanFitGuardianHousehold(placement.DestinationHouseholdId, placement.CaregiverId, child)
            ? null : "The guardian's House needs a free resident place.";
    }

    internal static bool HasGuardianPlacementTask(IFamilyGuardianWorld world, string actor) =>
        world.PhysicalAgents.Living.TryGetValue(actor, out var person) && person.GuardianPlacement is not null ||
        world.PhysicalAgents.Living.Values.Any(person => person.GuardianPlacement?.CaregiverId == actor);

    internal static string? GuardianPlacementChildFor(IFamilyGuardianWorld world, string caregiver)
    {
        var placements = world.PhysicalAgents.Living.Values
            .Where(person => person.GuardianPlacement is { } placement && placement.CaregiverId == caregiver &&
                IsCurrentGuardianPlacement(world, person.AgentId, placement) && GuardianPlacementHomeBlocker(world, person.AgentId, placement) is null)
            .OrderBy(person => person.GuardianPlacement!.StartedTick).ThenBy(person => person.AgentId, StringComparer.Ordinal).ToArray();
        var origin = world.PhysicalAgents.Living[caregiver].Position;
        // A blocked collection must not starve another accepted child of a move.
        // Keep an escort together, and retry the oldest blocked collection when
        // no reachable collection remains so its normal blocker stays visible.
        return (placements.FirstOrDefault(person => person.GuardianPlacement!.Stage == "escorting") ??
            placements.FirstOrDefault(person => world.IsWithinInteractionRange(origin, person.Position, world.ResourceInteractionRange) ||
            world.Reach(caregiver, person.Position, ReachPurpose.GuardianTravel, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) ??
            placements.FirstOrDefault())?.AgentId;
    }

    internal static CognitionCandidate? GuardianPlacementCandidate(IFamilyGuardianWorld world, string actor)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var person)) return null;
        GuardianTravelChoice? choice = person.GuardianPlacement is { Stage: "escorting" } follow
            ? new(GuardianTravelKind.Follow, follow.CaregiverId)
            : world.IsAdult(actor) && GuardianPlacementChildFor(world, actor) is { } child ? new(GuardianTravelKind.Relocate, child) : null;
        if (choice is null || world.OfferGuardianTravel(actor, choice) is not { } plan) return null;
        return new CognitionCandidate(plan.CandidateId, choice.Kind == GuardianTravelKind.Follow
            ? "Follow your accepted guardian to their House."
            : $"Collect {world.Society.GetAgent(plan.ChildId).Name} and accompany them to your House.", 8, plan.Placement.HouseId)
        { ActionPlan = plan };
    }

    internal static bool ContinueGuardianPlacement(IFamilyGuardianWorld world, string actor)
    {
        if (world.Turn.Contains(actor) || world.PendingInstructionFor(actor) is not null ||
            !HasGuardianPlacementTask(world, actor) || world.IsConversationBusy(actor)) return false;
        var person = world.PhysicalAgents.Living[actor];
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
        {
            // Infants still depend on actual nearby care; never grant them an adult food action.
            if (!LifeStageRules.IsInfant(world.Society.GetAgent(actor).AgeBand) &&
                world.UrgentSurvivalCandidateFor(actor, person) is { } urgent)
                world.ApplyCandidate(actor, person, urgent.Id, reportIdle: false);
            return true;
        }
        if (GuardianPlacementCandidate(world, actor) is not { } candidate) return false;
        world.Turn.RecordAction(actor);
        world.TryApplyGuardianPlacementCandidate(actor, candidate.Id, candidate.ActionPlan);
        return true;
    }

    internal static void EscortGuardianChild(IFamilyGuardianWorld world, string caregiver, string child)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { } placement ||
            placement.CaregiverId != caregiver || !IsCurrentGuardianPlacement(world, child, placement) ||
            GuardianPlacementHomeBlocker(world, child, placement) is not null) return;
        var adult = world.PhysicalAgents.Living[caregiver];
        if (placement.Stage == "collecting")
        {
            if (!world.IsWithinInteractionRange(adult.Position, dependent.Position, world.ResourceInteractionRange))
            {
                MoveForGuardianPlacement(world, caregiver, child, dependent.Position, "collect_guardian_child", world.ResourceInteractionRange);
                return;
            }
            placement = placement with { Stage = "escorting", Blocker = null };
            SetGuardianPlacement(world, child, placement);
        }
        if (world.PendingInstructionFor(child) is not null || world.IsConversationBusy(child) ||
            world.NeedsUrgentFood(dependent) || world.NeedsUrgentWarmth(dependent))
        {
            SetGuardianPlacement(world, child, placement with { Blocker = "The dependent is meeting needs or finishing another activity." });
            if (world.NeedsUrgentFood(dependent) || world.NeedsUrgentWarmth(dependent))
            {
                PauseGuardianPlacementWork(world, caregiver);
                if (dependent.FullnessBasisPoints < NeedThresholds.FoodPickupFullness &&
                    world.CaregiverFoodCarryRequirement(caregiver, world.PhysicalAgents.Living[caregiver]) > world.FreeCarryCapacity(caregiver))
                    world.MakeRoomForFood(caregiver, world.PhysicalAgents.Living[caregiver]);
                else
                    world.CareForChild(caregiver, child);
            }
            return;
        }
        if (!world.IsWithinInteractionRange(adult.Position, dependent.Position, 2))
        {
            SetGuardianPlacement(world, child, placement with { Blocker = "The guardian is waiting for the child to catch up." });
            return;
        }
        MoveForGuardianPlacement(world, caregiver, child, placement.HousePosition!.Value, "escort_guardian_child");
    }

    internal static void MoveForGuardianPlacement(IFamilyGuardianWorld world, string actor, string child, GridPoint destination, string reason, int range = 0)
    {
        PauseGuardianPlacementWork(world, actor);
        var before = world.PhysicalAgents.Living[actor];
        world.MoveToward(actor, before, destination, reason, range);
        if (world.PhysicalAgents.Living[child].GuardianPlacement is { } placement)
            SetGuardianPlacement(world, child, placement with
            {
                Blocker = world.PhysicalAgents.Living[actor].MoveWaitTicks > before.MoveWaitTicks
                    ? "The journey is waiting for a clear walking route." : null,
            });
    }

    internal static void PauseGuardianPlacementWork(IFamilyGuardianWorld world, string actor)
    {
        // Release native field claims before leaving for the accepted care task.
        if (world.FarmWorkFor(actor) is { } field) world.CancelFarmWork(field);
        if (world.PhysicalAgents.Living[actor].Equipment?.Repair is not null) world.CancelEquipmentRepair(actor);
        if (world.PhysicalAgents.Living[actor].Project is { Stage: not ("completed" or "cancelled") } project)
            world.SetProject(actor, project with { Stage = "paused", Blocker = "Accompanying an accepted dependent." });
    }

    internal static void FollowPlacementGuardian(IFamilyGuardianWorld world, string child, GuardianTravelActionPlan? originalPlan = null)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { Stage: "escorting" } placement) return;
        var plan = originalPlan ?? world.PlanGuardianTravel(child, new(GuardianTravelKind.Follow, placement.CaregiverId));
        if (plan is null || !world.CheckActionPlan(plan).Allowed) return;
        var adult = world.PhysicalAgents.Living[placement.CaregiverId];
        if (world.NeedsUrgentFood(adult) || world.NeedsUrgentWarmth(adult)) return;
        var home = placement.HousePosition!.Value;
        // Normal following stops beside the guardian; the final leg must enter
        // the actual House before household or Town membership changes.
        var destination = adult.Position == home ? home : adult.Position;
        MoveForGuardianPlacement(world, child, child, destination, "follow_guardian", adult.Position == home ? 0 : world.ResourceInteractionRange);
        CompleteGuardianPlacement(world, child);
    }

    internal static void AdvanceGuardianPlacementFollowers(IFamilyGuardianWorld world, IEnumerable<string> orderActors)
    {
        world.Turn.RecordActions(orderActors);
        foreach (var child in world.PhysicalAgents.Living.Values.Where(person => person.GuardianPlacement is { Stage: "escorting" })
                     .Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray())
        {
            if (!world.IsActionWithheld(child) && !world.Turn.Contains(child)) FollowPlacementGuardian(world, child);
            CompleteGuardianPlacement(world, child);
        }
    }

    internal static void CompleteGuardianPlacement(IFamilyGuardianWorld world, string child)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { Stage: "escorting" } placement ||
            !IsCurrentGuardianPlacement(world, child, placement) || GuardianPlacementHomeBlocker(world, child, placement) is not null ||
            dependent.Position != placement.HousePosition ||
            !world.IsWithinInteractionRange(dependent.Position, world.PhysicalAgents.Living[placement.CaregiverId].Position, world.ResourceInteractionRange)) return;
        var formerHousehold = world.Society.GetAgent(child).HouseholdId;
        var result = world.PlaceDependentWithGuardian(placement.CaregiverId, child, placement.CareRelationshipId, placement.CareRevision, placement.DestinationHouseholdId!);
        if (result.CreatedId != child) return;
        if (formerHousehold is not null && formerHousehold != placement.DestinationHouseholdId)
            world.RestoreHouseholdDeliveries(child, formerHousehold, "guardian-placement");
        if (!world.MoveDependentToGuardianTown(child, placement.CaregiverId))
            throw new InvalidOperationException("A validated guardian placement could not settle the child's Town.");
        SetGuardianPlacement(world, child, null);
        world.SetHousing(child, (world.PhysicalAgents.Living[child].Housing ?? new()) with { Blocker = world.HousingBlocker(child) });
        world.AppendEvent(new GuardianPlacementCompletedEventPayload(child, placement.CaregiverId));
    }

    internal static bool CanEnterGuardianPlacementHouse(IFamilyGuardianWorld world, string child, PlacedBuilding house) =>
        world.PhysicalAgents.Living.TryGetValue(child, out var person) && person.GuardianPlacement is { Stage: "escorting" } placement &&
        IsCurrentGuardianPlacement(world, child, placement) && GuardianPlacementHomeBlocker(world, child, placement) is null &&
        house.InstanceId == placement.HouseId && house.DefinitionId == placement.HouseDefinitionId &&
        house.HouseholdId == placement.DestinationHouseholdId && house.PlacedTick == placement.HousePlacedTick &&
        house.Position == placement.HousePosition &&
        world.IsWithinInteractionRange(world.PhysicalAgents.Living[placement.CaregiverId].Position, house.Position, world.ResourceInteractionRange);

    internal static GuardianTravelActionPlan? PlanGuardianTravel(IFamilyGuardianWorld world, string actor, GuardianTravelChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        var child = choice.Kind == GuardianTravelKind.Relocate ? choice.TargetId : actor;
        if (!world.PhysicalAgents.Living.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { } placement ||
            !world.PhysicalAgents.Living.ContainsKey(placement.CaregiverId)) return null;
        var target = choice.Kind == GuardianTravelKind.Relocate
            ? placement.Stage == "collecting" ? dependent.Position : placement.HousePosition ?? dependent.Position
            : world.PhysicalAgents.Living[placement.CaregiverId].Position;
        var range = choice.Kind == GuardianTravelKind.Relocate
            ? placement.Stage == "collecting" ? world.ResourceInteractionRange : 0
            : target == placement.HousePosition ? 0 : world.ResourceInteractionRange;
        return new(actor, choice, child, placement, world.Reach(actor, target, ReachPurpose.GuardianTravel, range, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckGuardianTravelPlan(IFamilyGuardianWorld world, GuardianTravelActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var following = plan.Choice.Kind == GuardianTravelKind.Follow;
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GuardianTravelCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.ChildId, out var child) || child.GuardianPlacement is not { } current ||
            current with { Stage = plan.Placement.Stage, Blocker = plan.Placement.Blocker } != plan.Placement ||
            !IsCurrentGuardianPlacement(world, plan.ChildId, current))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected child's guardian, care receipt or home has changed");
        if (GuardianPlacementHomeBlocker(world, plan.ChildId, current) is { } homeBlocker)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, homeBlocker);
        if (world.PendingInstructionFor(plan.Actor) is not null || world.IsConversationBusy(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "they need to finish the other activity first");
        if (!following)
            return current.CaregiverId == plan.Actor && GuardianPlacementChildFor(world, plan.Actor) == plan.ChildId
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "another accepted child has the earlier movement turn");
        if (plan.Actor != plan.ChildId || plan.Choice.TargetId != current.CaregiverId || current.Stage != "escorting" ||
            world.PendingInstructionFor(current.CaregiverId) is not null || world.IsConversationBusy(current.CaregiverId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected guardian is not ready for this escort");
        var adult = world.PhysicalAgents.Living[current.CaregiverId];
        return !world.NeedsUrgentFood(adult) && !world.NeedsUrgentWarmth(adult) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "the guardian is meeting urgent food or warmth needs");
    }

    internal static void ApplyCheckedGuardianTravel(IFamilyGuardianWorld world, GuardianTravelActionPlan plan)
    {
        // The selected people and home stay bound while collection/escort phases
        // advance. Following reaches the same guardian's current physical position.
        if (plan.Choice.Kind == GuardianTravelKind.Relocate) EscortGuardianChild(world, plan.Actor, plan.ChildId);
        else FollowPlacementGuardian(world, plan.ChildId, plan);
    }
}
