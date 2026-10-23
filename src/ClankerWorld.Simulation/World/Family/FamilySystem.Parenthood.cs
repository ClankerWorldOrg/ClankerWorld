using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static ActorCapabilityPolicy ParenthoodCapabilities(ParenthoodChoiceKind kind) =>
        new(kind is ParenthoodChoiceKind.Propose or ParenthoodChoiceKind.Accept ? ActionLifeStages.ReproductiveAdults : ActionLifeStages.Adults,
            AllowBoat: true, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

        // Only adults can have children; elders cannot (the owner's answer of October 2).
    internal static bool CanHaveChildren(IFamilyParenthoodWorld world, string actor) => world.PhysicalAgents.Living.ContainsKey(actor) &&
        LifeStageRules.CanHaveChildren(world.Society.GetAgent(actor).AgeBand);

        // A couple who may plan a child, keep a plan going and be held by the continuity rule.
    internal static bool EligibleContinuityParents(IFamilyParenthoodWorld world, string actor, string other) => actor != other &&
        CanHaveChildren(world, actor) && CanHaveChildren(world, other) && !world.CloseKin(actor, other) &&
        world.Society.GetAgent(actor).Status == SocietyAgentStatus.Active &&
        world.Society.GetAgent(other).Status == SocietyAgentStatus.Active &&
        world.Society.GetAgent(actor).HouseholdId is not null &&
        world.Society.GetAgent(other).HouseholdId is not null;

    internal static bool Partners(IFamilyParenthoodWorld world, string actor, string other) =>
        EligibleContinuityParents(world, actor, other) &&
        world.Partnerships(actor).Any(item => item.State == SocietyRelationshipState.Accepted &&
            (item.ProposerId == other || item.TargetId == other));

    internal static bool HasParenthoodDecision(IFamilyParenthoodWorld world, string actor) => world.ReadyForLesson(actor) &&
        (world.PhysicalAgents.Living.Values.Any(person => person.Parenthood is { Stage: "requested" } plan && plan.PartnerId == actor) ||
         ChildrenNeedingCare(world, actor).Any());

    internal static IEnumerable<AgentState> ChildrenNeedingCare(IFamilyParenthoodWorld world, string actor) => world.PhysicalAgents.Living.Values.Where(person =>
        LifeStageRules.IsDependent(world.Society.GetAgent(person.AgentId).AgeBand) &&
        (person.FullnessBasisPoints < NeedThresholds.FoodPickupFullness || person.Survival is { WarmthBasisPoints: < NeedThresholds.ComfortableWarmth } ||
         person.Survival is { IllnessBasisPoints: >= NeedThresholds.MedicalCareIllness } && !world.RecentlyCaredForIllness(person.AgentId)) &&
        (world.Society.Relationships.Any(item => item.Type == SocietyRelationshipType.Caregiver &&
             item.State == SocietyRelationshipState.Accepted && item.EffectiveTick <= world.WorldTick &&
             item.ProposerId == actor && item.TargetId == person.AgentId) ||
         world.NeedsCaregiver(person.AgentId) && world.PhysicalAgents.Living.TryGetValue(actor, out var caregiver) &&
             world.IsWithinInteractionRange(caregiver.Position, person.Position, world.ResourceInteractionRange)));

    internal static bool FamilyResourcesReady(IFamilyParenthoodWorld world, string actor) =>
        FamilyFoodReady(world, actor) && world.AccessibleShelters(actor, includeStormRefuge: false).Any();

    internal static bool FamilyFoodReady(IFamilyParenthoodWorld world, string actor) =>
        world.Society.GetAgent(actor).HouseholdId is not null &&
        world.ParenthoodFoodReadiness(actor).IsReady &&
        BirthFood(world, actor) is not null;

    internal static int CaregiverFoodCarryRequirement(IFamilyParenthoodWorld world, string actor, AgentState parent)
    {
        if (!world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor) || world.NeedsUrgentFood(parent) ||
            world.PreferredFood(actor, actor).Any() ||
            !ChildrenNeedingCare(world, actor).Any(child => child.FullnessBasisPoints < NeedThresholds.FoodPickupFullness && world.CanReach(actor, child.Position, ReachPurpose.PracticalCare, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable)))
            return 0;
        return world.MinimumFoodPickupCarryUnits(actor, parent.Position, forDependent: true);
    }

    internal static void CompleteParenthoodAction(IFamilyParenthoodWorld world, ParenthoodActionPlan plan)
    {
        switch (plan.Choice.Kind)
        {
            case ParenthoodChoiceKind.Propose:
                SetParenthood(world, plan.OwnerId, new(plan.PartnerId, "requested", world.WorldTick, world.WorldTick,
                    PrimaryCaregiverId: plan.CaregiverId, IntendedHouseholdId: plan.IntendedHouseholdId));
                world.MarkCurrentSchema();
                break;
            case ParenthoodChoiceKind.Accept:
                SetParenthood(world, plan.OwnerId, plan.Original! with
                { Stage = "preparing", PrimaryCaregiverId = plan.CaregiverId, IntendedHouseholdId = plan.IntendedHouseholdId });
                break;
            case ParenthoodChoiceKind.Postpone:
                SetParenthood(world, plan.OwnerId, plan.Original! with { Stage = "postponed" });
                break;
            default:
                SetParenthood(world, plan.OwnerId, plan.Original! with { Stage = "cancelled" });
                break;
        }
    }

    internal static (string Selector, string CaregiverId, string HouseholdId)[] ParenthoodCaregiverOptions(IFamilyParenthoodWorld world,
        string initiatorId, string acceptingParentId)
    {
        var options = new List<(string Selector, string CaregiverId, string HouseholdId)>();
        foreach (var option in new[] { (Selector: "initiator", CaregiverId: initiatorId),
                     (Selector: "acceptor", CaregiverId: acceptingParentId) })
        {
            var householdId = world.HouseholdFor(option.CaregiverId);
            if (householdId is null || options.Any(existing => existing.Selector == option.Selector)) continue;
            options.Add((option.Selector, option.CaregiverId, householdId));
        }
        return options.ToArray();
    }

    internal static void MaintainParenthood(IFamilyParenthoodWorld world)
    {
        foreach (var person in world.PhysicalAgents.Living.Values.OrderBy(person => person.Parenthood?.RequestedTick ?? long.MaxValue)
                     .ThenBy(person => person.AgentId, StringComparer.Ordinal).ToArray())
        {
            if (person.Parenthood is not { } plan || !ActiveParenthood(plan))
            {
                continue;
            }
            // A plan the continuity rule has sent ahead waits for food instead of expiring.
            if (!Partners(world, person.AgentId, plan.PartnerId) ||
                world.WorldTick - plan.LastTransitionTick > (plan.Stage == "requested" ? 120 : 2_400) &&
                !world.ContinuityPlanDue(person.AgentId, plan.PartnerId))
            {
                SetParenthood(world, person.AgentId, plan with { Stage = "cancelled" });
                continue;
            }
            if (plan.Stage != "preparing")
                continue;
            if (plan.PrimaryCaregiverId is not { } caregiverId || plan.IntendedHouseholdId is null ||
                world.HouseholdFor(caregiverId) is not { } birthHouseholdId)
            {
                SetParenthood(world, person.AgentId, plan with { Stage = "cancelled" });
                continue;
            }
            if (world.WorldTick - plan.LastTransitionTick < 600 || !FamilyFoodReady(world, caregiverId) ||
                !world.ReadyForLesson(person.AgentId) || !world.ReadyForLesson(plan.PartnerId))
            {
                continue;
            }
            // Losing shelter after agreement creates a housing need, not a
            // blocked birth. Keep the child near their caregiver in that case.
            var birthPosition = world.AccessibleShelters(caregiverId, includeStormRefuge: false).FirstOrDefault()?.Position ?? world.PhysicalAgents.Living[caregiverId].Position;
            var site = world.Map.Tiles.Where(tile => world.Map.IsBuildable(tile.Position) &&
                world.IsWithinInteractionRange(tile.Position, birthPosition, world.ResourceInteractionRange) &&
                !world.PhysicalAgents.Living.Values.Any(resident => resident.Position == tile.Position)).Select(tile => (GridPoint?)tile.Position).FirstOrDefault();
            if (site is null)
            {
                continue;
            }
            var requestId = world.ParenthoodRequestId(person.AgentId, plan.RequestedTick);
            var householdCaregivers = new[] { person.AgentId, plan.PartnerId }
                .Where(parent => world.HouseholdFor(parent) == birthHouseholdId)
                .Append(caregiverId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var birthFood = BirthFood(world, caregiverId)!;
            world.CommitBirth(new(requestId, 1, person.AgentId, plan.PartnerId, birthHouseholdId,
                    householdCaregivers, [person.AgentId, plan.PartnerId], birthFood[0].LotId, 4, world.WorldTick,
                    PrimaryCaregiverId: caregiverId, FoodContributions: birthFood));
            var birth = world.Society.Births.FirstOrDefault(item => item.RequestId == requestId);
            if (birth is null)
            {
                continue;
            }
            world.PhysicalAgents.RecordBirth(birth.ChildId, site.Value);
            var housingBlocker = world.HousingBlocker(birth.ChildId);
            if (housingBlocker is not null)
            {
                world.SetHousing(birth.ChildId, new(Blocker: housingBlocker));
                world.AppendEvent(new HousingBlockedEventPayload(birth.ChildId, housingBlocker));
            }
            if (world.TownForResident(caregiverId) is { } parentTownId)
                world.AddTownResident(parentTownId, birth.ChildId, "child_joined");
            SetParenthood(world, person.AgentId, plan with
            {
                Stage = "completed",
                ChildId = birth.ChildId,
                BirthHouseholdId = birth.HouseholdId,
            });
            world.AppendEvent(new ChildBornEventPayload(birth.ChildId));
        }
    }

    internal static void CompleteChildCare(IFamilyParenthoodWorld world, PracticalCareActionPlan plan)
    {
        var actor = plan.Actor;
        var childId = plan.Choice.DependentId;
        var child = world.PhysicalAgents.Living[childId];
        if (plan.Food is ConsumeFoodActionPlan meal)
        {
            var serving = world.Society.Inventory.GetLot(meal.StockId);
            world.ConsumeCareFood(actor, serving.Id);
            child = AgentStore.PrepareMeal(child, Math.Min(10_000, child.FullnessBasisPoints + world.FoodNourishment(serving.ItemKind)), world.AfterMeal(child, serving));
        }
        child = AgentStore.PrepareSurvival(child, (child.Survival ?? new SurvivalCondition()) with
        {
            WarmthBasisPoints = Math.Min(10_000, (child.Survival?.WarmthBasisPoints ?? 10_000) + 1_000),
            IllnessBasisPoints = Math.Max(0, (child.Survival?.IllnessBasisPoints ?? 0) - world.IllnessCareReliefBasisPoints),
        });
        world.PhysicalAgents.CompleteDependentCare(childId, child);
        world.AppendEvent(new ChildCaredForEventPayload(childId));
    }

    internal static void SetParenthood(IFamilyParenthoodWorld world, string owner, ParenthoodPlan plan)
    {
        if (world.PhysicalAgents.Living[owner].Parenthood?.Stage != plan.Stage)
        {
            plan = plan with { LastTransitionTick = world.WorldTick };
            world.AppendEvent(WorldEvents.Parenthood(plan.Stage, owner));
        }
        world.PhysicalAgents.RecordParenthood(owner, world.PhysicalAgents.Living[owner], plan);
    }

    internal static bool MayProposeParenthood(IFamilyParenthoodWorld world, string actor, string partner)
    {
        var continuityCouple = world.ContinuityCoupleOf(actor);
        return Partners(world, actor, partner) && FamilyResourcesReady(world, actor) &&
            !world.PhysicalAgents.Living.Values.Any(person => ActiveParenthood(person.Parenthood) &&
                (person.AgentId == actor || person.AgentId == partner || person.Parenthood!.PartnerId == actor || person.Parenthood.PartnerId == partner)) &&
            (continuityCouple is not null || !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.ProposerId == actor && edge.State == SocietyRelationshipState.Accepted && world.PhysicalAgents.Living.ContainsKey(edge.TargetId) &&
                LifeStageRules.IsYoungChild(world.Society.GetAgent(edge.TargetId).AgeBand))) &&
            !world.PhysicalAgents.Living.Values.Any(person => person.Parenthood is { } previous &&
                (person.AgentId == actor || previous.PartnerId == actor) && world.WorldTick - previous.LastTransitionTick < world.TicksPerDay);
    }

    internal static ParenthoodActionPlan? PlanParenthoodAction(IFamilyParenthoodWorld world, string actor, ParenthoodChoice choice)
    {
        var owner = choice.Kind == ParenthoodChoiceKind.Propose ? actor : choice.OwnerId;
        if (!world.PhysicalAgents.Living.TryGetValue(owner, out var physical) || !world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        var original = physical.Parenthood;
        var partner = choice.Kind == ParenthoodChoiceKind.Propose ? choice.OwnerId : original?.PartnerId;
        if (partner is null || !world.PhysicalAgents.Living.ContainsKey(partner)) return null;
        var caregiver = choice.Kind == ParenthoodChoiceKind.Propose ? actor : original?.PrimaryCaregiverId;
        var home = choice.Kind == ParenthoodChoiceKind.Propose ? world.HouseholdFor(actor) : original?.IntendedHouseholdId;
        if (choice.Kind == ParenthoodChoiceKind.Accept)
        {
            caregiver = choice.Selector == "initiator" ? owner : actor;
            home = choice.HouseholdId;
        }
        var partnership = world.Society.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Partnership &&
            edge.State == SocietyRelationshipState.Accepted && (edge.ProposerId == owner && edge.TargetId == partner || edge.ProposerId == partner && edge.TargetId == owner));
        return new(actor, choice, owner, partner, original, partnership, world.ContinuityCoupleFor(owner, partner), caregiver, home);
    }

    internal static ActionCheck CheckParenthoodActionPlan(IFamilyParenthoodWorld world, ParenthoodActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ParenthoodCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.OwnerId, out var owner) || !world.PhysicalAgents.Living.ContainsKey(plan.PartnerId) || owner.Parenthood != plan.Original ||
            plan.Partnership is null || !RelationshipPlan.Matches(world.Society.Relationships.FirstOrDefault(edge => edge.Id == plan.Partnership.Id), plan.Partnership) ||
            world.ContinuityCoupleFor(plan.OwnerId, plan.PartnerId) != plan.Continuity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected partnership, parenthood request or continuity deadline has changed");
        if (plan.Choice.Kind == ParenthoodChoiceKind.Propose)
            return plan.CaregiverId == plan.Actor && world.HouseholdFor(plan.Actor) == plan.IntendedHouseholdId && MayProposeParenthood(world, plan.Actor, plan.PartnerId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that couple or intended home is no longer ready for a parenthood request");
        if (plan.Original is not { } original || !ActiveParenthood(original) || plan.Actor != plan.OwnerId && plan.Actor != plan.PartnerId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected parenthood plan is no longer active");
        bool allowed;
        switch (plan.Choice.Kind)
        {
            case ParenthoodChoiceKind.Accept:
                allowed = plan.Actor == plan.PartnerId && original.Stage == "requested" && Partners(world, plan.OwnerId, plan.Actor) &&
                    plan.CaregiverId is { } caregiver && world.HouseholdFor(caregiver) == plan.IntendedHouseholdId && FamilyResourcesReady(world, caregiver) &&
                    ParenthoodCaregiverOptions(world, plan.OwnerId, plan.Actor).Any(option => option.Selector == plan.Choice.Selector &&
                        option.CaregiverId == caregiver && option.HouseholdId == plan.IntendedHouseholdId);
                break;
            case ParenthoodChoiceKind.Decline:
                allowed = original.Stage == "requested" && plan.Actor == plan.PartnerId && plan.Continuity is null;
                break;
            case ParenthoodChoiceKind.Cancel:
                allowed = !(original.Stage == "requested" && plan.Actor == plan.PartnerId) && plan.Continuity is null;
                break;
            default:
                allowed = plan.Continuity is { } rule && world.ContinuityAllowsPostponement(rule);
                break;
        }
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected parenthood choice is no longer available");
    }
}
