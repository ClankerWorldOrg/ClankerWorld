using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PersonalGoodsActionPlan? PlanPersonalGoods(string actor, PersonalGoodsChoice choice, int maximum = int.MaxValue,
        PersonalGoodsMode mode = PersonalGoodsMode.Choice, string? requiredHouse = null,
        string? requiredOwner = null, GridPoint? requiredPosition = null) =>
        CommunitySystem.PlanPersonalGoods(CommunityGoodsWorld, actor, choice, maximum, mode, requiredHouse, requiredOwner, requiredPosition);

    private ActionCheck CheckPersonalGoods(PersonalGoodsActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckPersonalGoods(CommunityGoodsWorld, plan, policy);

    private (string MoveId, string ItemKind, int Quantity)? ApplyPersonalGoods(PersonalGoodsActionPlan? plan)
    {
        if (plan is null) return null;
        if (CheckActionPlan(plan).Refusal is { } refusal)
        {
            if (plan.Mode == PersonalGoodsMode.Choice) RefuseChoice(plan, refusal);
            return null;
        }
        return ApplyCheckedPersonalGoods(plan);
    }

    private (string MoveId, string ItemKind, int Quantity)? ApplyCheckedPersonalGoods(PersonalGoodsActionPlan plan) =>
        CommunitySystem.ApplyCheckedPersonalGoods(CommunityGoodsWorld, plan);
}
