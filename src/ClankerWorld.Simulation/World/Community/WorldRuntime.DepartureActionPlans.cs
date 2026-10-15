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

    private DepartureActionPlan? PlanDepartureAction(string actor, DepartureChoice choice) =>
        CommunitySystem.PlanDepartureAction(CommunityDepartureWorld, actor, choice);

    private ActionCheck CheckDepartureActionPlan(DepartureActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckDepartureActionPlan(CommunityDepartureWorld, plan, policy);

    private void ApplyDepartureActionPlan(DepartureActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteDepartureAction(plan);
    }
}
