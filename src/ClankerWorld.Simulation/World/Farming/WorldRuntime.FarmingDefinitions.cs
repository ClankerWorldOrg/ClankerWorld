namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<FieldChoice, FieldActionPlan> FieldDefinition => FarmingActions.Field;
    private static ActionDefinition<TreeChoice, TreeActionPlan> TreeDefinition => FarmingActions.Tree;
    private static ActionDefinition<FarmStockActionKind, FarmStockActionPlan> FarmStockDefinition => FarmingActions.Stock;

    private static class FarmingActions
    {
        internal static readonly ActionDefinition<FieldChoice, FieldActionPlan> Field = new(
            static (string candidate, out FieldChoice choice) =>
            {
                choice = FieldChoice.Decode(candidate)!;
                return choice is not null;
            },
            static choice => choice.Encode(), FarmingSystem.FieldPolicy,
            static (world, actor, choice, policy) => world.farmingSystem.PlanFieldAction(world.farmingWorld, actor, choice, capabilityPolicy: policy),
            static (world, plan, policy) => world.farmingSystem.CheckFieldPlan(world.farmingWorld, plan, policy),
            static (world, plan, _, _) =>
            {
                world.farmingSystem.ApplyFieldPlan(world.farmingWorld, plan);
                return true;
            });

        internal static readonly ActionDefinition<TreeChoice, TreeActionPlan> Tree = new(
            static (string candidate, out TreeChoice choice) =>
            {
                var decoded = TreeActionPlan.Decode(candidate);
                choice = decoded.GetValueOrDefault();
                return decoded.HasValue;
            },
            TreeActionPlan.Candidate, FarmingSystem.TreePolicy,
            static (world, actor, choice, policy) => FarmingSystem.PlanTreeAction(world.farmingWorld, actor, choice, capabilityPolicy: policy),
            static (world, plan, policy) => FarmingSystem.CheckTreePlan(world.farmingWorld, plan, policy),
            static (world, plan, _, _) =>
            {
                FarmingSystem.ApplyTreePlan(world.farmingWorld, plan);
                return true;
            });

        internal static readonly ActionDefinition<FarmStockActionKind, FarmStockActionPlan> Stock = new(
            FarmStockActionPlan.TryDecode, FarmStockActionPlan.Candidate, FarmStockPolicy,
            static (world, actor, choice, policy) => world.PlanFarmStockAction(actor, choice, policy),
            static (world, plan, policy) => world.CheckFarmStockAction(plan, policy),
            static (world, plan, _, _) =>
            {
                world.ApplyCheckedFarmStockAction(plan);
                return true;
            });
    }
}
