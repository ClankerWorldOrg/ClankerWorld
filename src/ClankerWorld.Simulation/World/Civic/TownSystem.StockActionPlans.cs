using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static readonly ActorCapabilityPolicy SalvageCapabilities = new(ActionLifeStages.NonInfants, AllowActiveOrder: true);

    internal IEnumerable<TownStockActionPlan> PlanTownSalvages(ITownWorld world, string actor)
    {
        var abandoned = towns.Where(town => town.IsAbandoned).Select(town => town.Id).ToArray();
        if (abandoned.Length == 0) yield break;
        foreach (var goods in world.SpendingGoods(actor, GoodsUse.Salvage, abandoned).OrderBy(match => match.Lot.Id, StringComparer.Ordinal))
        {
            var lot = goods.Lot;
            var quantity = TownSalvageQuantity(world, actor, lot);
            var range = lot.GroundPosition is not null ? WorldRuntime.CivicSharedRules.ResourceInteractionRange : 0;
            var family = InventoryContainerRules.IsContainer(lot.ItemKind)
                ? GoodsPlanFamily.Capture(world.Society.Inventory, lot) : null;
            var plan = new TownStockActionPlan(actor, new(TownStockActionKind.Salvage, lot.OwnerId, lot.Id), lot.ItemKind,
                quantity, world.HouseholdStockPosition(lot), range,
                Route: Array.AsReadOnly(new[] { world.Agents[actor].Position }), Family: family);
            if (CheckTownSalvagePlan(world, plan).Allowed) yield return plan;
        }
    }

    internal TownStockActionPlan? PlanTownSalvage(ITownWorld world, string actor, string candidate) =>
        PlanTownSalvages(world, actor).FirstOrDefault(plan => plan.CandidateId == candidate);

    internal ActionCheck CheckTownSalvagePlan(ITownWorld world, TownStockActionPlan plan) =>
        CheckTownSalvagePlan(world, plan, SalvageCapabilities);

    internal ActionCheck CheckTownSalvagePlan(ITownWorld world, TownStockActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        if (!world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, plan.Destination, plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "it's out of reach now");
        if (!towns.Any(town => town.Id == plan.Choice.TownId && town.IsAbandoned))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the Town is no longer abandoned");
        var goods = world.SpendingGoods(plan.Actor, GoodsUse.Salvage, [plan.Choice.TownId], plan.ItemKind)
            .SingleOrDefault(match => match.Lot.Id == plan.Choice.StockId);
        if (goods is null || plan.Quantity <= 0 || goods.Quantity < plan.Quantity ||
            TownSalvageQuantity(world, plan.Actor, goods.Lot) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected communal goods are no longer available");
        if (world.HouseholdStockPosition(goods.Lot) != plan.Destination ||
            (goods.Lot.GroundPosition is not null ? WorldRuntime.CivicSharedRules.ResourceInteractionRange : 0) != plan.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected goods have moved");
        if (!GoodsPlanFamily.Matches(world.Society.Inventory, goods.Lot, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel or its contents have changed");
        return new();
    }

    internal static void ApplyTownSalvagePlan(ITownWorld world, TownStockActionPlan plan)
    {
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
            WorldRuntime.CivicSharedRules.TownSalvageOperationId(world.WorldTick, plan.Actor, plan.CandidateId),
            plan.Choice.TownId, plan.Actor, plan.Choice.StockId, plan.Quantity,
            "town_stock_salvaged", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
        world.AppendEvent(new TownStockSalvagedEventPayload(plan.Actor, plan.Choice.TownId, plan.ItemKind, plan.Quantity), world.Agents[plan.Actor].Position);
    }

    private static int TownSalvageQuantity(ITownWorld world, string actor, InventoryLot lot)
    {
        var room = world.FreeCarryCapacity(actor);
        var cart = lot.ItemKind == InventoryContainerRules.Handcart;
        return InventoryContainerRules.IsContainer(lot.ItemKind)
            ? !WorldRuntime.CivicSharedRules.HasActiveContainerReservation(world.Society.Inventory, lot.Id) && (cart || world.VesselFits(lot, room)) ? 1 : 0
            : Math.Min(world.PhysicalUnreservedQuantity(lot), room);
    }

}
