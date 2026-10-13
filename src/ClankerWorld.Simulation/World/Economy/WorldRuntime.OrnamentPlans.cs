using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly ActorCapabilityPolicy OrnamentPolicy = new(ActionLifeStages.Adults,
        AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: false);

    private bool IsOrnamentSource(string actor, InventoryLot lot) => lot.ContainerLotId is null &&
        (IsPersonallyCarriedOrnament(lot, actor) || HouseholdFor(actor) is { } household && lot.OwnerId == household &&
            (lot.CarrierId is null || lot.CarrierId == actor) && FreeCarryCapacity(actor) > 0 &&
            (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                building.InstanceId == lot.StorageBuildingId && building.HouseholdId == household)));

    private bool IsLocalOrnamentRecipient(string actor, string recipient) => recipient != actor &&
        LivingOrnamentAdult(recipient) && IsWithinInteractionRange(agents[actor].Position, agents[recipient].Position, ResourceInteractionRange) &&
        FreeCarryCapacity(recipient) > 0;

    private WearOrnamentActionPlan PlanOrnamentWear(string actor, InventoryLot stock)
    {
        var position = HouseholdStockPosition(stock);
        var range = HouseholdStockInteractionRange(stock);
        return new(actor, new(stock.Id), stock.OwnerId, stock.ItemKind, agents[actor].Equipment?.OrnamentLotId,
            position, range, Reach(actor, position, ReachPurpose.OrnamentPickup, range, 0, SwimmingPolicy.WhenCapable));
    }

    private GiftOrnamentActionPlan PlanOrnamentGift(string actor, InventoryLot stock, string recipient) =>
        new(actor, new(stock.Id, recipient), stock.ItemKind, agents[actor].Equipment?.OrnamentLotId);

    private ActionCheck CheckOrnamentPlan(OrnamentActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, OrnamentPolicy);
        if (!capability.Allowed) return capability;
        if (BusyWithOrnamentIncompatibleWork(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current work comes first");
        var slot = agents[plan.Actor].Equipment?.OrnamentLotId;
        switch (plan)
        {
            case WearOrnamentActionPlan wear:
                if (slot != wear.PreviousSlotId || slot == wear.Selected.StockId)
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their worn ornament has changed");
                if (RecheckGoods(OrnamentCollectionRequest(plan.Actor, wear.OwnerId), wear.Selected.StockId) is not { Lot: var stock } ||
                    stock.ItemKind != wear.ItemKind || !IsOrnamentSource(plan.Actor, stock))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected ornament is no longer available");
                if (PersonalEquipmentRules.IsCarried(stock, plan.Actor)) return new();
                if (HouseholdStockPosition(stock) != wear.Position || HouseholdStockInteractionRange(stock) != wear.InteractionRange)
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ornament has moved");
                return Reach(plan.Actor, wear.Position, ReachPurpose.OrnamentPickup, wear.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected ornament is out of reach now");
            case RemoveOrnamentActionPlan remove:
                return slot == remove.StockId ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer wear the selected ornament");
            case GiftOrnamentActionPlan gift:
                if ((slot == gift.Selected.StockId) != (gift.PreviousSlotId == gift.Selected.StockId))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their worn ornament has changed");
                if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(gift.ItemKind)),
                        gift.Selected.StockId) is not { Lot: var given } || !IsPersonallyCarriedOrnament(given, plan.Actor))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gift is no longer available");
                return IsLocalOrnamentRecipient(plan.Actor, gift.Selected.RecipientId) ? new() :
                    ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient is no longer nearby with room for the gift");
            default: throw new InvalidOperationException("An ornament plan has no registered operation.");
        }
    }

    private void ApplyOrnamentPlan(OrnamentActionPlan plan)
    {
        if (CheckOrnamentPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        switch (plan)
        {
            case WearOrnamentActionPlan wear:
                var stock = society.Checkpoint.Inventory.GetLot(wear.Selected.StockId);
                if (!PersonalEquipmentRules.IsCarried(stock, plan.Actor) &&
                    !IsWithinInteractionRange(agents[plan.Actor].Position, wear.Position, wear.InteractionRange))
                    MoveToward(plan.Actor, agents[plan.Actor], wear.Position, "ornament", wear.InteractionRange);
                else CompleteOrnamentWear(plan.Actor, stock);
                break;
            case RemoveOrnamentActionPlan remove: CompleteOrnamentRemoval(plan.Actor, remove.StockId); break;
            case GiftOrnamentActionPlan gift:
                CompleteOrnamentGift(plan.Actor, gift.Selected.RecipientId, society.Checkpoint.Inventory.GetLot(gift.Selected.StockId));
                break;
        }
    }
}
