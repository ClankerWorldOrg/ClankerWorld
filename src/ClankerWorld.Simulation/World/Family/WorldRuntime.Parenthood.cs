using ClankerWorld.Simulation.Cognition;
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

    private bool RecentlyCaredForIllness(string dependent) => events.Any(item =>
        (item.Kind is "child_cared_for" or "dependent_cared_for") && item.Detail == dependent &&
        item.WorldTick > WorldTick - IllnessCareCooldownTicks);

    /// <summary>Derives the birth food gate from one captured checkpoint, also used by owner and parent guidance.</summary>
    public static ParenthoodFood ParenthoodFoodReadiness(SocietyCheckpoint checkpoint, string caregiverId,
        IReadOnlyList<TownRuntimeState> townStates) =>
        FamilySystem.ParenthoodFoodReadiness(checkpoint, caregiverId, townStates);

    public static string ParenthoodFoodNote(SocietyCheckpoint checkpoint, string caregiverId,
        IReadOnlyList<TownRuntimeState> townStates, bool showAmounts = true) =>
        FamilySystem.ParenthoodFoodNote(checkpoint, caregiverId, townStates, showAmounts);

    private void AddParenthoodCandidateOptions(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor) || survivalState is null)
        {
            return;
        }
        foreach (var child in FamilySystem.ChildrenNeedingCare(this.FamilyParenthoodWorld, actor).Where(child =>
                     !NeedsUrgentFood(agents[actor]) || IsWithinInteractionRange(agents[actor].Position, child.Position, ResourceInteractionRange)))
        {
            var parent = agents[actor];
            if (child.FullnessBasisPoints < NeedThresholds.FoodPickupFullness && !PreferredFood(actor, actor).Any())
            {
                if (NeedsUrgentFood(parent) || !CanReach(actor, child.Position, ReachPurpose.PracticalCare, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable))
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
            if (PracticalCareDefinition.Offer(this, actor, new(PracticalCareKind.Child, child.AgentId)) is { } care)
                candidates.Add(new CognitionCandidate(care.CandidateId, "Bring food and warmth to your dependent child.", 2) { ActionPlan = care });
        }
        if (!CommunitySystem.ReadyForLesson(this.CommunityLearningWorld, actor)) return;
        foreach (var person in agents.Values.Where(person => FamilySystem.ActiveParenthood(person.Parenthood) &&
                     (person.AgentId == actor || person.Parenthood!.PartnerId == actor)))
        {
            // While the continuity rule holds a couple, "not yet" replaces refusal until their two days are up.
            var rule = this.familySystem.ContinuityCoupleFor(this.FamilyContinuityWorld, person.AgentId, person.Parenthood!.PartnerId);
            var mayPostpone = rule is not null && FamilySystem.ContinuityAllowsPostponement(this.FamilyContinuityWorld, rule);
            if (person.Parenthood is { Stage: "requested" } && person.AgentId != actor)
            {
                foreach (var option in FamilySystem.ParenthoodCaregiverOptions(this.FamilyParenthoodWorld, person.AgentId, actor))
                {
                    if (!FamilySystem.FamilyResourcesReady(this.FamilyParenthoodWorld, option.CaregiverId)) continue;
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
        var continuityCouple = this.familySystem.ContinuityCoupleOf(this.FamilyContinuityWorld, actor);
        var partner = agents.Keys.Order(StringComparer.Ordinal).FirstOrDefault(other => other != actor && FamilySystem.MayProposeParenthood(this.FamilyParenthoodWorld, actor, other));
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

    private void CareForChild(string actor, string childId, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as PracticalCareActionPlan ?? PlanPracticalCare(actor, new(PracticalCareKind.Child, childId));
        if (plan is not null) ApplyPracticalCarePlan(plan);
    }

    private void CompleteChildFoodPickup(string actor, SharedFoodActionPlan shared, GridPoint destination) =>
        TryCollectHouseholdServing(actor, agents[actor], society.Checkpoint.Inventory.GetLot(shared.StockId), $"care-food:{WorldTick}:{actor}",
            "caregiver_food", "care_food", destination, ResourceInteractionRange);

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
                (FamilySystem.ActiveParenthood(plan) || plan.Stage is "completed" or "postponed") &&
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
                (FamilySystem.ActiveParenthood(plan) || plan.Stage == "postponed") && state.Society.Society.Births.Any(birth =>
                    birth.RequestId == ParenthoodRequestId(person.AgentId, plan.RequestedTick)) ||
                FamilySystem.ActiveParenthood(plan) && (!participants.Add(person.AgentId) || !participants.Add(plan.PartnerId)))
            {
                throw new InvalidDataException("The saved parenthood plan is invalid.");
            }
        }
    }
}
