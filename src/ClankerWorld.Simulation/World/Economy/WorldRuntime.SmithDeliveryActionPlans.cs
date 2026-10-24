using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy SmithDeliveryPolicy => new(ActionLifeStages.Adults, AllowUrgentFood: false,
        AllowUrgentWarmth: true, AllowActiveOrder: true);

    private int SmithDeliveryQuantity(string actor, SmithDeliveryKind kind, string household, PlacedBuilding smith, InventoryLot stock)
    {
        var direct = stock.OwnerId == actor;
        var missing = BlacksmithInputTarget(smith.InstanceId, stock.ItemKind) - SmithStoredQuantity(household, smith.InstanceId, stock.ItemKind) -
            (kind == SmithDeliveryKind.Ore ? 0 : SmithIncomingQuantity(smith.InstanceId, stock.ItemKind));
        var available = kind == SmithDeliveryKind.Ore || !direct ? AvailableLotQuantity(stock) : TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, stock);
        var quantity = Math.Min(DestinationRoom(smith.InstanceId), Math.Min(missing, available));
        if (kind != SmithDeliveryKind.Ore) quantity = Math.Min(quantity, HouseHaulLoadQuantity);
        return direct ? quantity : Math.Min(quantity, PickupCarryCapacity(actor, stock, smith.Position));
    }

    private SmithDeliveryActionPlan? PlanSmithDelivery(string actor, SmithDeliveryKind kind,
        InventoryLot? selectedStock = null, PlacedBuilding? selectedSmith = null)
    {
        var candidate = kind == SmithDeliveryKind.Ore ? "deliver_smith_ore" : "haul_smith_input";
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), candidate, SmithDeliveryPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            (selectedSmith ?? this.HouseholdBuildingWithTag(household, "blacksmith")) is not { } smith)
            return null;
        var stock = selectedStock ?? (kind == SmithDeliveryKind.Ore ? PersonalSmithOre(actor) : BlacksmithInputForDelivery(household, smith, actor));
        if (stock is null) return null;
        var quantity = SmithDeliveryQuantity(actor, kind, household, smith, stock);
        if (quantity <= 0) return null;
        var direct = stock.OwnerId == actor;
        var target = direct ? smith.Position : HouseholdStockPosition(stock);
        var range = direct ? 0 : HouseholdStockInteractionRange(stock);
        var inventory = society.Checkpoint.Inventory;
        var vessel = InventoryContainerRules.IsContainer(stock.ItemKind);
        return new(actor, kind, household, smith.InstanceId, smith.Position, stock.Id, stock.OwnerId, stock.ItemKind,
            quantity, vessel ? ContainerFamilyQuantity(inventory, stock.Id) : quantity, direct, target, range,
            vessel ? GoodsPlanFamily.Capture(inventory, stock) : null,
            Reach(actor, target, ReachPurpose.SmithDelivery, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckSmithDeliveryPlan(SmithDeliveryActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || this.HouseholdBuildingWithTag(plan.HouseholdId, "blacksmith") is not { } smith ||
            smith.InstanceId != plan.SmithId || smith.Position != plan.Destination || CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their Blacksmith or delivery work has changed");
        var request = plan.Kind == SmithDeliveryKind.Ore
            ? new GoodsRequest(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind))
            : DeliveryGoodsRequest(plan.Actor, plan.OwnerId, plan.ItemKind, plan.SmithId);
        var inventory = society.Checkpoint.Inventory;
        if (RecheckGoods(request, plan.StockId) is not { } match || match.Lot.ContainerLotId is not null ||
            !GoodsPlanFamily.Matches(inventory, match.Lot, plan.Family) || plan.DirectDelivery != (match.Lot.OwnerId == plan.Actor) ||
            plan.DirectDelivery && !PersonalEquipmentRules.IsCarried(match.Lot, plan.Actor) ||
            !plan.DirectDelivery && (match.Lot.CarrierId is not null || HouseholdStockPosition(match.Lot) != plan.Target ||
                HouseholdStockInteractionRange(match.Lot) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Blacksmith supplies are no longer available");
        if (DestinationRoom(plan.SmithId) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Blacksmith has no room for the selected load");
        if (SmithDeliveryQuantity(plan.Actor, plan.Kind, plan.HouseholdId, smith, match.Lot) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load is no longer needed or available");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.InteractionRange) ||
            Reach(plan.Actor, plan.Target, ReachPurpose.SmithDelivery, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Blacksmith trip is out of reach now");
    }

    private void ApplyCheckedSmithDeliveryPlan(SmithDeliveryActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Target, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Target, plan.Kind == SmithDeliveryKind.Ore ? "smith_ore" : "smith_input", plan.InteractionRange);
            return;
        }
        var purpose = plan.Kind == SmithDeliveryKind.Ore ? "smith_ore_delivered" : plan.DirectDelivery ? "smith_input_delivered" : "smith_input_picked_up";
        var operationId = DerivedId.Create(plan.Kind == SmithDeliveryKind.Ore ? $"smith-ore-delivery:{WorldTick}:{plan.Actor}" :
            plan.DirectDelivery ? $"smith-input-delivery:{WorldTick}:{plan.Actor}" : $"smith-input-pickup:{WorldTick}:{plan.Actor}");
        RunInventoryCommand(InventorySystem.ApplyCheckedSmithDeliveryPlan(plan, operationId, purpose));
        AppendEvent(plan.Kind == SmithDeliveryKind.Ore
            ? new SmithOreDeliveredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.SmithId)
            : plan.DirectDelivery
                ? new SmithInputDeliveredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.SmithId)
                : new SmithInputPickedUpEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.SmithId));
    }
}
