using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record ParenthoodPlan(string PartnerId, string Stage, long RequestedTick,
    long LastTransitionTick, string? ChildId = null, string? PrimaryCaregiverId = null,
    string? IntendedHouseholdId = null, string? BirthHouseholdId = null);

public sealed record ParenthoodFood(int AvailablePortions, int RequiredPortions)
{
    public bool IsReady => AvailablePortions >= RequiredPortions;
}

public sealed partial class WorldRuntime
{
    private const int IllnessCareCooldownTicks = 8;
    private static bool ActiveParenthood(ParenthoodPlan? plan) => plan?.Stage is "requested" or "preparing";

    private bool RecentlyCaredForIllness(string dependent) => events.Any(item =>
        (item.Kind is "child_cared_for" or "dependent_cared_for") && item.Detail == dependent &&
        item.WorldTick > WorldTick - IllnessCareCooldownTicks);

    // Only adults can have children; elders cannot (the owner's answer of October 2).
    private bool CanHaveChildren(string actor) => agents.ContainsKey(actor) &&
        society.Checkpoint.GetAgent(actor).AgeBand == SocietyAgeBand.Adult;

    // A couple who may plan a child, keep a plan going and be held by the continuity rule.
    private bool Partners(string actor, string other) => CanHaveChildren(actor) && CanHaveChildren(other) && !CloseKin(actor, other) &&
        society.Checkpoint.GetAgent(actor).HouseholdId is not null &&
        society.Checkpoint.GetAgent(other).HouseholdId is not null &&
        Partnerships(actor).Any(item => item.State == SocietyRelationshipState.Accepted &&
            (item.ProposerId == other || item.TargetId == other));

    private bool HasParenthoodDecision(string actor) => ReadyForLesson(actor) &&
        (agents.Values.Any(person => person.Parenthood is { Stage: "requested" } plan && plan.PartnerId == actor) ||
         ChildrenNeedingCare(actor).Any());

    private IEnumerable<AgentState> ChildrenNeedingCare(string actor) => agents.Values.Where(person =>
        society.Checkpoint.GetAgent(person.AgentId).AgeBand is SocietyAgeBand.Infant or SocietyAgeBand.Child or SocietyAgeBand.Adolescent &&
        (person.FullnessBasisPoints < 7_000 || person.Survival is { WarmthBasisPoints: < 6_000 } ||
         person.Survival is { IllnessBasisPoints: >= 2_500 } && !RecentlyCaredForIllness(person.AgentId)) &&
        (society.Checkpoint.Relationships.Any(item => item.Type == SocietyRelationshipType.Caregiver &&
             item.State == SocietyRelationshipState.Accepted && item.EffectiveTick <= WorldTick &&
             item.ProposerId == actor && item.TargetId == person.AgentId) ||
         NeedsCaregiver(person.AgentId) && agents.TryGetValue(actor, out var caregiver) &&
             IsWithinInteractionRange(caregiver.Position, person.Position, ResourceInteractionRange)));

    private bool FamilyResourcesReady(string actor) =>
        FamilyFoodReady(actor) && AccessibleShelters(actor, includeStormRefuge: false).Any();

    private bool FamilyFoodReady(string actor) =>
        society.Checkpoint.GetAgent(actor).HouseholdId is not null &&
        ParenthoodFoodReadiness(society.Checkpoint, actor, towns).IsReady &&
        BirthFood(actor) is not null;

    /// <summary>Derives the birth food gate from one captured checkpoint, also used by owner and parent guidance.</summary>
    public static ParenthoodFood ParenthoodFoodReadiness(SocietyCheckpoint checkpoint, string caregiverId,
        IReadOnlyList<TownRuntimeState> townStates)
    {
        if (checkpoint.GetAgent(caregiverId).HouseholdId is not { } householdId) return new(0, 4);
        var required = checkpoint.Agents.Count(person => person.HouseholdId == householdId &&
            person.Status == SocietyAgentStatus.Active) * 2 + 4;
        return new(BirthFoodSources(checkpoint, caregiverId, townStates).Sum(match => match.Quantity), required);
    }

    public static string ParenthoodFoodNote(SocietyCheckpoint checkpoint, string caregiverId,
        IReadOnlyList<TownRuntimeState> townStates, bool showAmounts = true)
    {
        var food = ParenthoodFoodReadiness(checkpoint, caregiverId, townStates);
        if (!showAmounts)
            return food.IsReady ? "The caregiver's household has enough ready-to-eat food."
                : "The caregiver's household needs more ready-to-eat food; grain and flour need cooking.";
        var amount = FormattableString.Invariant($"Household food: {food.AvailablePortions}/{food.RequiredPortions} ready-to-eat portions");
        return food.IsReady ? amount + "."
            : amount + FormattableString.Invariant($"; need {food.RequiredPortions - food.AvailablePortions} more. Grain and flour need cooking.");
    }

    // Birth reserves and consumes its food in place, which the inventory
    // allows for food in a usable storage pot as well as loose food.
    private static GoodsRequest? BirthFoodRequest(SocietyCheckpoint checkpoint, string actor)
    {
        var household = checkpoint.GetAgent(actor).HouseholdId ?? actor;
        var kinds = InventoryIndex.For(checkpoint.Inventory).OwnedBy(household)
            .Where(lot => IsEdibleFood(lot.ItemKind)).Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? null : new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(household), new GoodsKinds(kinds));
    }

    private static IReadOnlyList<GoodsMatch> BirthFoodSources(SocietyCheckpoint checkpoint, string actor,
        IReadOnlyList<TownRuntimeState> townStates) => BirthFoodRequest(checkpoint, actor) is { } request
            ? GoodsInventoryQuery.FindConsumableGoods(checkpoint.Inventory, request, (index, lot) =>
                lot.CarrierId is { } carrier && carrier != actor ? GoodsReason.Custody :
                InventoryHoldRules.For(index, lot).Any(hold => hold.Purpose == InventoryHoldPurpose.MarketStall) ? GoodsReason.MarketStall : null).Matches : [];

    private List<SocietyBirthFoodContribution>? BirthFood(string actor)
    {
        var remaining = 4;
        var contributions = new List<SocietyBirthFoodContribution>();
        if (BirthFoodRequest(society.Checkpoint, actor) is not { } request) return null;
        foreach (var offered in BirthFoodSources(society.Checkpoint, actor, towns))
        {
            if (RecheckGoods(request, offered.Lot.Id) is not { } current) return null;
            var quantity = Math.Min(remaining, current.Quantity);
            contributions.Add(new(current.Lot.Id, quantity));
            remaining -= quantity;
            if (remaining == 0) return contributions;
        }
        return null;
    }

    private bool CanReachDependent(string actor, AgentState parent, AgentState child) =>
        IsWithinInteractionRange(parent.Position, child.Position, ResourceInteractionRange) ||
        FindUnoccupiedRoute(actor, parent.Position, child.Position, ResourceInteractionRange).Count > 0;

    private int CaregiverFoodCarryRequirement(string actor, AgentState parent)
    {
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor) || NeedsUrgentFood(parent) ||
            PreferredFood(actor, actor).Any() ||
            !ChildrenNeedingCare(actor).Any(child => child.FullnessBasisPoints < 7_000 && CanReachDependent(actor, parent, child)))
            return 0;
        return MinimumFoodPickupCarryUnits(actor, parent.Position, forDependent: true);
    }

    private void AddParenthoodCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor) || survivalState is null)
        {
            return;
        }
        foreach (var child in ChildrenNeedingCare(actor).Where(child =>
                     !NeedsUrgentFood(agents[actor]) || IsWithinInteractionRange(agents[actor].Position, child.Position, ResourceInteractionRange)))
        {
            var parent = agents[actor];
            if (child.FullnessBasisPoints < 7_000 && !PreferredFood(actor, actor).Any())
            {
                if (NeedsUrgentFood(parent) || !CanReachDependent(actor, parent, child))
                    continue;
                var required = MinimumFoodPickupCarryUnits(actor, parent.Position, forDependent: true);
                if (required == 0)
                    continue;
                if (FreeCarryCapacity(actor) < required)
                {
                    AddMakeRoomForFoodCandidate(candidates, actor, parent, 2);
                    continue;
                }
            }
            candidates.Add(new("care:" + child.AgentId, "Bring food and warmth to your dependent child.", 2));
        }
        if (!ReadyForLesson(actor)) return;
        foreach (var person in agents.Values.Where(person => ActiveParenthood(person.Parenthood) &&
                     (person.AgentId == actor || person.Parenthood!.PartnerId == actor)))
        {
            // While the continuity rule holds a couple, "not yet" replaces refusal until their two days are up.
            var rule = ContinuityCoupleFor(person.AgentId, person.Parenthood!.PartnerId);
            var mayPostpone = rule is not null && ContinuityAllowsPostponement(rule);
            if (person.Parenthood is { Stage: "requested" } && person.AgentId != actor)
            {
                foreach (var option in ParenthoodCaregiverOptions(person.AgentId, actor))
                {
                    if (!FamilyResourcesReady(option.CaregiverId)) continue;
                    var household = society.Checkpoint.GetHousehold(option.HouseholdId);
                    var caregiver = society.Checkpoint.GetAgent(option.CaregiverId);
                    candidates.Add(new($"parent_accept:{person.AgentId}:{option.Selector}:{Uri.EscapeDataString(option.HouseholdId)}",
                        $"Agree to parenthood with {caregiver.Name} as primary caregiver in the {household.Name} household ({household.Id}); this records the intended home before preparation, which takes time and needs food and shelter.", 25));
                }
                if (rule is null)
                    candidates.Add(new("parent_decline:" + person.AgentId, "Decline the parenthood request.", 70));
                else if (mayPostpone)
                    candidates.Add(new("parent_postpone:" + person.AgentId, $"Say not yet to having a child. While fewer than {ContinuityEligibleCoupleThreshold} eligible adult couples " +
                        "who are not close relatives are available you may not refuse; the plan goes ahead when your two days are up.", 70));
            }
            else if (rule is null)
            {
                candidates.Add(new("parent_cancel:" + person.AgentId, "Withdraw consent before the planned birth.", 110));
            }
            else if (mayPostpone)
            {
                candidates.Add(new("parent_postpone:" + person.AgentId, $"Put off the child plan for now. While fewer than {ContinuityEligibleCoupleThreshold} eligible adult couples " +
                    "who are not close relatives are available you may not refuse; the plan goes ahead when your two days are up.", 110));
            }
        }
        // A couple held by the continuity rule may start sooner even with an older child, as long as no infant.
        var continuityCouple = ContinuityCoupleOf(actor);
        if (!FamilyResourcesReady(actor) || agents.Values.Any(person => ActiveParenthood(person.Parenthood) &&
                (person.AgentId == actor || person.Parenthood!.PartnerId == actor)) ||
            continuityCouple is null &&
            society.Checkpoint.Relationships.Any(item => item.Type == SocietyRelationshipType.Caregiver && item.ProposerId == actor &&
                item.State == SocietyRelationshipState.Accepted && agents.ContainsKey(item.TargetId) &&
                society.Checkpoint.GetAgent(item.TargetId).AgeBand is SocietyAgeBand.Infant or SocietyAgeBand.Child) ||
            agents.Values.Any(person => person.Parenthood is { } previous &&
                (person.AgentId == actor || previous.PartnerId == actor) && WorldTick - previous.LastTransitionTick < worldSystems.Config.TicksPerDay))
        {
            return;
        }
        var partner = agents.Keys.Order(StringComparer.Ordinal).FirstOrDefault(other => other != actor && Partners(actor, other) &&
            !agents.Values.Any(person => ActiveParenthood(person.Parenthood) &&
                (person.AgentId == other || person.Parenthood!.PartnerId == other)));
        if (partner is not null)
        {
            candidates.Add(new("parent_propose:" + partner, continuityCouple is null
                ? "Ask your partner whether to raise a child together. If they agree, they may choose either parent as the primary caregiver and that parent's household as the intended home. Their independent consent is required."
                : $"Ask your partner to start raising a child together now. While fewer than {ContinuityEligibleCoupleThreshold} eligible adult couples " +
                    "who are not close relatives are available " +
                    "they may say not yet but not refuse; the plan goes ahead when your two days are up. " +
                    "They may choose either parent as the primary caregiver and that parent's household as the intended home.", 75));
        }
    }

    private void ApplyParenthoodCandidate(string actor, string candidate)
    {
        var target = candidate[(candidate.IndexOf(':', StringComparison.Ordinal) + 1)..];
        if (candidate.StartsWith("care:", StringComparison.Ordinal))
        {
            CareForChild(actor, target);
            return;
        }
        if (candidate.StartsWith("parent_propose:", StringComparison.Ordinal))
        {
            if (Partners(actor, target) && FamilyResourcesReady(actor) &&
                !agents.Values.Any(person => ActiveParenthood(person.Parenthood) &&
                    (person.AgentId == actor || person.AgentId == target || person.Parenthood!.PartnerId == actor || person.Parenthood.PartnerId == target)))
            {
                SetParenthood(actor, new(target, "requested", WorldTick, WorldTick,
                    PrimaryCaregiverId: actor, IntendedHouseholdId: HouseholdFor(actor)));
                checkpointSchemaVersion = StateSchemaVersion;
            }
            return;
        }
        var isAccept = TryParseParentAcceptance(candidate, out var acceptingPlanId, out var selector, out var intendedHomeId);
        if (!isAccept) acceptingPlanId = target;
        if (!agents.TryGetValue(acceptingPlanId, out var owner) || owner.Parenthood is not { } plan ||
            !ActiveParenthood(plan) || (actor != target && actor != plan.PartnerId))
        {
            return;
        }
        if (isAccept && actor == plan.PartnerId &&
            plan.Stage == "requested" && Partners(acceptingPlanId, actor))
        {
            var selected = ParenthoodCaregiverOptions(acceptingPlanId, actor)
                .SingleOrDefault(option => option.Selector == selector && option.HouseholdId == intendedHomeId);
            if (selected.CaregiverId is null || !FamilyResourcesReady(selected.CaregiverId)) return;
            SetParenthood(acceptingPlanId, plan with
            {
                Stage = "preparing",
                PrimaryCaregiverId = selected.CaregiverId,
                IntendedHouseholdId = selected.HouseholdId,
            });
        }
        else if (ContinuityCoupleFor(target, plan.PartnerId) is { } rule)
        {
            // Refusal is not available while the rule holds the couple; "not yet" lasts only until the deadline.
            if (candidate.StartsWith("parent_postpone:", StringComparison.Ordinal) && ContinuityAllowsPostponement(rule))
                SetParenthood(target, plan with { Stage = "postponed" });
        }
        else if (candidate.StartsWith("parent_cancel:", StringComparison.Ordinal) || candidate.StartsWith("parent_decline:", StringComparison.Ordinal))
        {
            SetParenthood(acceptingPlanId, plan with { Stage = "cancelled" });
        }
    }

    private (string Selector, string CaregiverId, string HouseholdId)[] ParenthoodCaregiverOptions(
        string initiatorId, string acceptingParentId)
    {
        var options = new List<(string Selector, string CaregiverId, string HouseholdId)>();
        foreach (var option in new[] { (Selector: "initiator", CaregiverId: initiatorId),
                     (Selector: "acceptor", CaregiverId: acceptingParentId) })
        {
            var householdId = HouseholdFor(option.CaregiverId);
            if (householdId is null || options.Any(existing => existing.Selector == option.Selector)) continue;
            options.Add((option.Selector, option.CaregiverId, householdId));
        }
        return options.ToArray();
    }

    private static bool TryParseParentAcceptance(
        string candidate,
        out string initiatorId,
        out string selector,
        out string householdId)
    {
        var prefix = "parent_accept:";
        initiatorId = string.Empty;
        selector = string.Empty;
        householdId = string.Empty;
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var householdSeparator = candidate.LastIndexOf(':');
        var selectorSeparator = householdSeparator > 0 ? candidate.LastIndexOf(':', householdSeparator - 1) : -1;
        if (selectorSeparator <= prefix.Length || householdSeparator <= selectorSeparator + 1 ||
            householdSeparator == candidate.Length - 1)
            return false;
        initiatorId = candidate[prefix.Length..selectorSeparator];
        selector = candidate[(selectorSeparator + 1)..householdSeparator];
        if (selector is not ("initiator" or "acceptor")) return false;
        householdId = Uri.UnescapeDataString(candidate[(householdSeparator + 1)..]);
        return !string.IsNullOrWhiteSpace(initiatorId) && !string.IsNullOrWhiteSpace(householdId);
    }

    private void MaintainParenthood()
    {
        foreach (var person in agents.Values.OrderBy(person => person.Parenthood?.RequestedTick ?? long.MaxValue)
                     .ThenBy(person => person.AgentId, StringComparer.Ordinal).ToArray())
        {
            if (person.Parenthood is not { } plan || !ActiveParenthood(plan))
            {
                continue;
            }
            // A plan the continuity rule has sent ahead waits for food instead of expiring.
            if (!Partners(person.AgentId, plan.PartnerId) ||
                WorldTick - plan.LastTransitionTick > (plan.Stage == "requested" ? 120 : 2_400) &&
                !ContinuityPlanDue(person.AgentId, plan.PartnerId))
            {
                SetParenthood(person.AgentId, plan with { Stage = "cancelled" });
                continue;
            }
            if (plan.Stage != "preparing")
                continue;
            if (plan.PrimaryCaregiverId is not { } caregiverId || plan.IntendedHouseholdId is null ||
                HouseholdFor(caregiverId) is not { } birthHouseholdId)
            {
                SetParenthood(person.AgentId, plan with { Stage = "cancelled" });
                continue;
            }
            if (WorldTick - plan.LastTransitionTick < 600 || !FamilyFoodReady(caregiverId) ||
                !ReadyForLesson(person.AgentId) || !ReadyForLesson(plan.PartnerId))
            {
                continue;
            }
            // Losing shelter after agreement creates a housing need, not a
            // blocked birth. Keep the child near their caregiver in that case.
            var birthPosition = AccessibleShelters(caregiverId, includeStormRefuge: false).FirstOrDefault()?.Position ?? agents[caregiverId].Position;
            var site = map.Tiles.Where(tile => map.IsBuildable(tile.Position) &&
                IsWithinInteractionRange(tile.Position, birthPosition, ResourceInteractionRange) &&
                !agents.Values.Any(resident => resident.Position == tile.Position)).Select(tile => (GridPoint?)tile.Position).FirstOrDefault();
            if (site is null)
            {
                continue;
            }
            var requestId = ParenthoodRequestId(person.AgentId, plan.RequestedTick);
            var householdCaregivers = new[] { person.AgentId, plan.PartnerId }
                .Where(parent => HouseholdFor(parent) == birthHouseholdId)
                .Append(caregiverId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var birthFood = BirthFood(caregiverId)!;
            society.Apply(checkpoint => SocietyRules.CommitBirth(checkpoint,
                new(requestId, 1, person.AgentId, plan.PartnerId, birthHouseholdId,
                    householdCaregivers, [person.AgentId, plan.PartnerId], birthFood[0].LotId, 4, WorldTick,
                    PrimaryCaregiverId: caregiverId, FoodContributions: birthFood)));
            var birth = society.Checkpoint.Births.FirstOrDefault(item => item.RequestId == requestId);
            if (birth is null)
            {
                continue;
            }
            agents.Add(birth.ChildId, new(birth.ChildId, site.Value, 8_000, 0, "undecided", "find a purpose",
                Survival: new SurvivalCondition(), IdentityChoicePending: true));
            var housingBlocker = HousingBlocker(birth.ChildId);
            if (housingBlocker is not null)
            {
                SetHousing(birth.ChildId, new(Blocker: housingBlocker));
                AppendEvent("housing_blocked", $"{birth.ChildId}:{housingBlocker}");
            }
            if (TownForResident(caregiverId) is { } parentTownId)
                AddTownResident(parentTownId, birth.ChildId, "child_joined");
            SetParenthood(person.AgentId, plan with
            {
                Stage = "completed",
                ChildId = birth.ChildId,
                BirthHouseholdId = birth.HouseholdId,
            });
            AppendEvent("child_born", birth.ChildId);
        }
    }

    private void CareForChild(string actor, string childId)
    {
        if (!ReadyForBriefInteraction(actor) || !ChildrenNeedingCare(actor).Any(child => child.AgentId == childId))
        {
            return;
        }
        var parent = agents[actor];
        var child = agents[childId];
        if (NeedsUrgentFood(parent) && !IsWithinInteractionRange(parent.Position, child.Position, ResourceInteractionRange)) return;
        if (child.FullnessBasisPoints < 7_000 && PreferredFood(actor, actor).FirstOrDefault() is null)
        {
            if (NeedsUrgentFood(parent)) return;
            if (AvailableHouseholdServing(actor, parent.Position, returnTo: child.Position, returnRange: ResourceInteractionRange) is { } sharedFood)
            {
                TryCollectHouseholdServing(actor, parent, sharedFood, $"care-food:{WorldTick}:{actor}",
                    "caregiver_food", "care_food", child.Position, ResourceInteractionRange);
            }
            else
            {
                if (AvailableFoodSource(actor, parent.Position) is { } source)
                {
                    if (IsWithinInteractionRange(parent.Position, source.Position, ResourceInteractionRange))
                        HarvestFood(actor, parent);
                    else
                        MoveToward(actor, parent, source.Position, "care_food", ResourceInteractionRange);
                }
            }
            return;
        }
        if (!IsWithinInteractionRange(parent.Position, child.Position, ResourceInteractionRange))
        {
            MoveToward(actor, parent, child.Position, "care", ResourceInteractionRange);
            return;
        }
        if (child.FullnessBasisPoints < 7_000 && PreferredFood(actor, actor).FirstOrDefault() is { } serving)
        {
            society.Apply(checkpoint => SocietyRules.ConsumeInventory(checkpoint, actor, serving.Id, 1));
            child = child with { FullnessBasisPoints = Math.Min(10_000, child.FullnessBasisPoints + FoodNourishment(serving.ItemKind)), Survival = AfterMeal(child, serving) };
        }
        child = child with
        {
            Survival = (child.Survival ?? new SurvivalCondition()) with
            {
                WarmthBasisPoints = Math.Min(10_000, (child.Survival?.WarmthBasisPoints ?? 10_000) + 1_000),
                IllnessBasisPoints = Math.Max(0, (child.Survival?.IllnessBasisPoints ?? 0) - IllnessCareReliefBasisPoints),
            }
        };
        agents[childId] = child;
        AppendEvent("child_cared_for", childId);
    }

    private void SetParenthood(string owner, ParenthoodPlan plan)
    {
        if (agents[owner].Parenthood?.Stage != plan.Stage)
        {
            plan = plan with { LastTransitionTick = WorldTick };
            AppendEvent("parenthood_" + plan.Stage, owner);
        }
        agents[owner] = agents[owner] with { Parenthood = plan };
    }

    private static void ValidateParenthood(WorldRuntimeState state)
    {
        var known = state.Society.Society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var participants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var person in state.Agents)
        {
            if (person.Parenthood is not { } plan) continue;
            if (!known.Contains(plan.PartnerId) || plan.PartnerId == person.AgentId ||
                plan.Stage is not ("requested" or "preparing" or "completed" or "cancelled" or "postponed") || plan.RequestedTick < 0 ||
                plan.LastTransitionTick < plan.RequestedTick || plan.LastTransitionTick > state.Society.Society.WorldTick ||
                (ActiveParenthood(plan) || plan.Stage is "completed" or "postponed") &&
                    (plan.PrimaryCaregiverId is not { } caregiverId ||
                     caregiverId != person.AgentId && caregiverId != plan.PartnerId ||
                     plan.IntendedHouseholdId is not { } homeId ||
                     !state.Society.Society.Households.Any(household => household.Id == homeId)) ||
                plan.Stage == "completed" && (plan.ChildId is null || !known.Contains(plan.ChildId) ||
                    plan.BirthHouseholdId is not { } birthHomeId ||
                    !state.Society.Society.Births.Any(birth => birth.ChildId == plan.ChildId &&
                        birth.RequestId == ParenthoodRequestId(person.AgentId, plan.RequestedTick) &&
                        birth.PrimaryCaregiverId == plan.PrimaryCaregiverId &&
                        birth.HouseholdId == birthHomeId)) ||
                plan.Stage != "completed" && (plan.ChildId is not null || plan.BirthHouseholdId is not null) ||
                (ActiveParenthood(plan) || plan.Stage == "postponed") && state.Society.Society.Births.Any(birth =>
                    birth.RequestId == ParenthoodRequestId(person.AgentId, plan.RequestedTick)) ||
                ActiveParenthood(plan) && (!participants.Add(person.AgentId) || !participants.Add(plan.PartnerId)))
            {
                throw new InvalidDataException("The saved parenthood plan is invalid.");
            }
        }
    }
}
