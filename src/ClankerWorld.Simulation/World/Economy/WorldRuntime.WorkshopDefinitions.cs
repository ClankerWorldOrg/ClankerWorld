namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<SmithDeliveryKind, SmithDeliveryActionPlan> SmithDeliveryDefinition => WorkshopActions.Delivery;
    private static ActionDefinition<SmithGatherChoice, SmithGatherActionPlan> SmithGatherDefinition => WorkshopActions.Gather;
    private static ActionDefinition<ToolCollectionChoice, ToolCollectionActionPlan> ToolCollectionDefinition => WorkshopActions.Collection;
    private static ActionDefinition<ToolRepairChoice, ToolRepairActionPlan> ToolRepairDefinition => WorkshopActions.Repair;
    private static ActionDefinition<RareMiningChoice, RareMiningActionPlan> RareMiningDefinition => WorkshopActions.Mining;
    private static ActionDefinition<WorkstationSupplyChoice, WorkstationSupplyActionPlan> WorkstationSupplyDefinition => WorkshopActions.Supply;
    private static ActionDefinition<StoreStockChoice, StoreStockActionPlan> StoreStockDefinition => WorkshopActions.Store;

    private static ChoiceDecoder<TChoice> DecodeReferenceChoice<TChoice>(Func<string, TChoice?> decode) where TChoice : class =>
        (string candidate, out TChoice choice) => { choice = decode(candidate)!; return choice is not null; };

    private static class WorkshopActions
    {
        internal static readonly ActionDefinition<SmithDeliveryKind, SmithDeliveryActionPlan> Delivery = new(
            SmithDeliveryActionPlan.TryDecode, SmithDeliveryActionPlan.Candidate, SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanSmithDelivery(actor, choice),
            static (world, plan, policy) => world.CheckSmithDeliveryPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedSmithDeliveryPlan(plan); return true; });

        internal static readonly ActionDefinition<SmithGatherChoice, SmithGatherActionPlan> Gather = new(
            DecodeReferenceChoice(SmithGatherChoice.Decode), static choice => choice.Encode(), SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanSmithGather(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckSmithGatherPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedSmithGatherPlan(plan); return true; });

        internal static readonly ActionDefinition<ToolCollectionChoice, ToolCollectionActionPlan> Collection = new(
            DecodeReferenceChoice(ToolCollectionChoice.Decode), static choice => choice.Encode(), SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanToolCollection(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckToolCollectionPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedToolCollectionPlan(plan); return true; });

        internal static readonly ActionDefinition<ToolRepairChoice, ToolRepairActionPlan> Repair = new(
            DecodeReferenceChoice(ToolRepairChoice.Decode), static choice => choice.Encode(), SmithDeliveryPolicy,
            static (world, actor, choice, _) => world.PlanToolRepair(actor, choice.ToolId),
            static (world, plan, policy) => world.CheckToolRepairPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedToolRepairPlan(plan); return true; });

        internal static readonly ActionDefinition<RareMiningChoice, RareMiningActionPlan> Mining = new(
            DecodeReferenceChoice(RareMiningChoice.Decode), static choice => choice.Encode(), RareMiningPolicy,
            static (world, actor, choice, _) => world.PlanRareMining(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckRareMiningPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedRareMiningPlan(plan); return true; });

        internal static readonly ActionDefinition<WorkstationSupplyChoice, WorkstationSupplyActionPlan> Supply = new(
            DecodeReferenceChoice(WorkstationSupplyChoice.Decode), static choice => choice.Encode(), WorkstationSupplyPolicy,
            static (world, actor, choice, _) => world.PlanWorkstationSupply(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckWorkstationSupplyPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedWorkstationSupplyPlan(plan); return true; });

        internal static readonly ActionDefinition<StoreStockChoice, StoreStockActionPlan> Store = new(
            StoreStockChoice.TryDecode, static _ => StoreStockChoice.Encode(), StoreStockPolicy,
            static (world, actor, _, _) => world.PlanStoreStock(actor),
            static (world, plan, policy) => world.CheckStoreStockPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedStoreStockPlan(plan); return true; });
    }
}
