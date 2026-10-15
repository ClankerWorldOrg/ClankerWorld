namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static GoodsRequest DepartureAllowanceRequest(string actor, string household) =>
        new(GoodsUse.DepartureAllowance, actor, GoodsOwners.One(household), EdibleFoodGoods);

    private List<DepartureAllowance> PlanDepartureAllowance(string actor, string household)
    {
        var result = new List<DepartureAllowance>();
        var remaining = 2;
        foreach (var match in FindGoods(DepartureAllowanceRequest(actor, household)).Matches)
        {
            var quantity = Math.Min(remaining, match.Quantity);
            if (quantity <= 0) continue;
            result.Add(new(match.Lot, quantity));
            remaining -= quantity;
            if (remaining == 0) break;
        }
        return result;
    }

    private DepartureActionPlan? PlanDepartureAction(string actor, DepartureChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        return new(actor, choice, household, MayRelocateFromHousehold(actor) ? "displaced" : "voluntary",
            MovingCareGroup(actor).ToArray(), household is null ? [] : PlanDepartureAllowance(actor, household));
    }

    private ActionCheck CheckDepartureActionPlan(DepartureActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (society.Checkpoint.GetAgent(plan.Actor).HouseholdId != plan.HouseholdId ||
            !MovingCareGroup(plan.Actor).SequenceEqual(plan.CareGroup, StringComparer.Ordinal) ||
            plan.HouseholdId is not null && plan.CareGroup.Any(id => society.Checkpoint.GetAgent(id).HouseholdId != plan.HouseholdId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or moving care group has changed");
        if ((MayRelocateFromHousehold(plan.Actor) ? "displaced" : "voluntary") != plan.Cause ||
            plan.Cause == "displaced" && (HasDependentInPrimaryCare(plan.Actor) || HoldsRelocationNotice(plan.Actor) && !MayRelocateFromHousehold(plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure is no longer needed or permitted");
        if (plan.Choice.Kind == DepartureChoiceKind.Leave && plan.HouseholdId is null ||
            plan.Choice.Kind == DepartureChoiceKind.Found && (agents[plan.Actor].Housing?.Request is not null ||
                !MayFoundHousehold(plan.Actor) || AskableHouseholds(plan.Actor).Any()))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that household choice is no longer available");
        if (plan.HouseholdId is { } household)
            foreach (var selected in plan.Allowance)
            {
                var current = RecheckGoods(DepartureAllowanceRequest(plan.Actor, household), selected.Stock.Id);
                if (current is null || current.Quantity < selected.Quantity ||
                    current.Lot.OwnerId != selected.Stock.OwnerId || current.Lot.ItemKind != selected.Stock.ItemKind ||
                    current.Lot.StorageBuildingId != selected.Stock.StorageBuildingId || current.Lot.GroundPosition != selected.Stock.GroundPosition ||
                    current.Lot.CarrierId != selected.Stock.CarrierId || current.Lot.ContainerLotId != selected.Stock.ContainerLotId)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected departure food allowance has changed");
            }
        return new();
    }

    private void ApplyDepartureActionPlan(DepartureActionPlan plan)
    {
        if (CheckDepartureActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteDepartureAction(plan);
    }
}
