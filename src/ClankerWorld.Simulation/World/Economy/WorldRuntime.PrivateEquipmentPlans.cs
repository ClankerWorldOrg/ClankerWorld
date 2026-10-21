using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy PrivateEquipmentPolicy(PrivateEquipmentKind kind) =>
        new(kind == PrivateEquipmentKind.CarryAid ? ActionLifeStages.Adults : ActionLifeStages.NonInfants,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static int CarryAidCapacity(string kind) => kind == "leather_sack" ? 32 :
        kind == "sack" ? PersonalEquipmentRules.SackCapacity : PersonalEquipmentRules.BasketCapacity;

    private bool IsPrivateEquipmentUpgrade(string actor, InventoryLot item, bool carryAid)
    {
        if (!CanEquipPrivateItem(actor, item, carryAid)) return false;
        if (carryAid) return PersonalEquipmentRules.IsCarryAid(item.ItemKind) &&
            CarryAidCapacity(item.ItemKind) > PersonalEquipmentRules.Capacity(society.Checkpoint.Inventory, actor, agents[actor].Equipment);
        if (!PersonalEquipmentRules.IsGarment(item.ItemKind)) return false;
        var current = EquippedGarment(actor);
        var weather = WeatherAt(agents[actor].Position);
        return item.Id != current?.Id && (current is null || PersonalEquipmentRules.Protection(item, weather) > PersonalEquipmentRules.Protection(current, weather));
    }

    private PrivateEquipmentActionPlan? PlanPrivateEquipment(string actor, bool carryAid, InventoryLot? selected = null)
    {
        if ((selected ?? (carryAid ? BetterCarryAid(actor) : BetterGarment(actor))) is not { } item) return null;
        var position = item.OwnerId == actor ? agents[actor].Position : HouseholdStockPosition(item);
        var range = item.OwnerId == actor ? 0 : HouseholdStockInteractionRange(item);
        var equipment = agents[actor].Equipment;
        return new(actor, carryAid ? PrivateEquipmentKind.CarryAid : PrivateEquipmentKind.Garment,
            HouseholdFor(actor), item.Id, item.OwnerId, item.ItemKind,
            carryAid ? equipment?.CarryAidLotId : equipment?.ClothingLotId, position, range,
            Reach(actor, position, ReachPurpose.PrivateEquipment, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckPrivateEquipmentPlan(PrivateEquipmentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var carryAid = plan.Kind == PrivateEquipmentKind.CarryAid;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? PrivateEquipmentPolicy(plan.Kind));
        if (!capability.Allowed) return capability;
        var equipment = agents[plan.Actor].Equipment;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId ||
            (carryAid ? equipment?.CarryAidLotId : equipment?.ClothingLotId) != plan.PreviousSlotId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or equipped item has changed");
        if (RecheckGoods(new(GoodsUse.Equip, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind)), plan.StockId) is not { Lot: var item } ||
            item.OwnerId != plan.Actor && (item.CarrierId is not null || HouseholdStockPosition(item) != plan.Position || HouseholdStockInteractionRange(item) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected equipment is no longer available");
        if (!IsPrivateEquipmentUpgrade(plan.Actor, item, carryAid))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected item is no longer a useful equipment upgrade");
        return item.OwnerId == plan.Actor || Reach(plan.Actor, plan.Position, ReachPurpose.PrivateEquipment,
            plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected equipment is out of reach now");
    }

    private void ApplyCheckedPrivateEquipmentPlan(PrivateEquipmentActionPlan plan)
    {
        var person = agents[plan.Actor];
        if (plan.OwnerId != plan.Actor && !IsWithinInteractionRange(person.Position, plan.Position, plan.InteractionRange))
        {
            MoveToward(plan.Actor, person, plan.Position, "equipment", plan.InteractionRange);
            return;
        }
        CompletePrivateEquipment(plan.Actor, society.Checkpoint.Inventory.GetLot(plan.StockId), plan.Kind == PrivateEquipmentKind.CarryAid);
    }
}
