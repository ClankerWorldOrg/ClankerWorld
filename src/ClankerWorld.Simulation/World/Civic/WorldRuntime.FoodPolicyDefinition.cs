namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<FoodPolicyChoice, FoodPolicyActionPlan> FoodPolicyDefinition => FoodPolicyActions.Definition;

    private static class FoodPolicyActions
    {
        internal static readonly ActionDefinition<FoodPolicyChoice, FoodPolicyActionPlan> Definition = new(
            DecodeReferenceChoice(FoodPolicyChoice.Decode), static choice => choice.Encode(), TownSystem.FoodPolicyCapabilities,
            static (world, actor, choice, _) => world.townSystem.PlanFoodPolicy(actor, choice.Encode()),
            static (world, plan, policy) => world.townSystem.CheckFoodPolicy(world.civicWorld, plan, policy),
            static (world, plan, _, _) => { world.townSystem.ApplyCheckedFoodPolicy(world.civicWorld, plan); return true; });
    }
}
