namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<FoodChoice, ConsumeFoodActionPlan> FoodConsumptionDefinition => SurvivalActions.Consumption;
    private static ActionDefinition<FoodChoice, SharedFoodActionPlan> SharedFoodDefinition => SurvivalActions.SharedFood;
    private static ActionDefinition<FoodChoice, FoodResourceActionPlan> FoodResourceDefinition => SurvivalActions.Resource;
    private static ActionDefinition<WarmthChoice, FireActionPlan> FireDefinition => SurvivalActions.Fire;
    private static ActionDefinition<WarmthChoice, WarmthActionPlan> WarmthDefinition => SurvivalActions.Warmth;

    private static class SurvivalActions
    {
        internal static readonly ActionDefinition<FoodChoice, ConsumeFoodActionPlan> Consumption = new(
            FoodChoice.TryDecode, static choice => choice.Encode(), FoodPolicy(consumption: true),
            static (world, actor, _, _) => world.PlanFoodConsumption(actor),
            static (world, plan, policy) => world.CheckFoodPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFoodPlan(plan); return true; });

        internal static readonly ActionDefinition<FoodChoice, SharedFoodActionPlan> SharedFood = new(
            FoodChoice.TryDecode, static choice => choice.Encode(), FoodPolicy(consumption: false),
            static (world, actor, _, _) => world.PlanSharedFood(actor),
            static (world, plan, policy) => world.CheckFoodPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFoodPlan(plan); return true; });

        internal static readonly ActionDefinition<FoodChoice, FoodResourceActionPlan> Resource = new(
            FoodChoice.TryDecode, static choice => choice.Encode(), FoodPolicy(consumption: false),
            static (world, actor, choice, _) => world.PlanFoodResource(actor, choice.Kind == FoodActionKind.Harvest),
            static (world, plan, policy) => world.CheckFoodPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFoodPlan(plan); return true; });

        internal static readonly ActionDefinition<WarmthChoice, FireActionPlan> Fire = new(
            WarmthChoice.TryDecode, static choice => choice.Encode(), FirePolicy,
            static (world, actor, _, policy) => world.PlanFire(actor, policy),
            static (world, plan, policy) => world.CheckFirePlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFirePlan(plan); return true; });

        internal static readonly ActionDefinition<WarmthChoice, WarmthActionPlan> Warmth = new(
            WarmthChoice.TryDecode, static choice => choice.Encode(), WarmthPolicy,
            static (world, actor, _, policy) => world.PlanWarmth(actor, policy),
            static (world, plan, policy) => world.CheckWarmthPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedWarmthPlan(plan); return true; });
    }
}
