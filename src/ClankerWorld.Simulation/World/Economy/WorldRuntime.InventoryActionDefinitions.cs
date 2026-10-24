namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private static class InventoryActions
    {
        internal static readonly ActionDefinition<OrnamentRequestChoice, OrnamentActionPlan> Ornament = new(
            DecodeReferenceChoice(OrnamentRequestChoice.Decode), static choice => choice.Encode(), OrnamentPolicy,
            static (world, actor, choice, _) => world.CurrentOrnamentPlan(actor, choice.CandidateId),
            static (world, plan, policy) => world.CheckOrnamentPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedOrnament(plan); return true; });

        internal static readonly ActionDefinition<FoodCargoChoice, FoodCargoActionPlan> FoodCargo = new(
            FoodCargoChoice.TryDecode, static _ => FoodCargoChoice.Encode(), FoodCargoPolicy,
            static (world, actor, _, _) => world.PlanFoodCargo(actor),
            static (world, plan, policy) => world.CheckFoodCargoPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCargoStorage(plan.Storage); return true; });
    }
}
