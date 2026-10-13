using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly ActorCapabilityPolicy EquipmentRepairPolicy = new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private static GoodsRequest EquipmentRepairTargetRequest(string actor) => new(GoodsUse.EquipmentRepair,
        actor, GoodsOwners.One(actor), new(["clothing", "padded_coat", "rain_cloak", "basket", "sack", "leather_sack"]));

    private EquipmentRepairActionPlan? PlanEquipmentRepair(string actor, InventoryLot? selected = null)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), "repair_equipment", EquipmentRepairPolicy).Allowed ||
            HouseholdFor(actor) is not { } household) return null;
        foreach (var target in selected is null ? WornEquipmentItems(actor) : [selected])
        {
            if (EquipmentRepairSite(actor, target) is not { } site) continue;
            var materials = new List<PlannedRepairMaterial>();
            var inputs = new List<PlannedEquipmentRepairInput>();
            foreach (var need in PersonalEquipmentRules.RepairMaterials(target.ItemKind))
            {
                if (SpendingGoods(actor, GoodsUse.Spend, [actor], need.ResourceId).FirstOrDefault(match =>
                    PersonalEquipmentRules.IsCarried(match.Lot, actor) && match.Quantity >= need.Amount) is { } carried)
                    materials.Add(new(carried.Lot.Id, need.ResourceId, need.Amount));
                else if (SharedItem(need.ResourceId, actor, site.Position) is { } stock)
                    inputs.Add(new(need.Amount, new(stock.Id, stock.OwnerId, need.ResourceId,
                        HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock))));
                else break;
            }
            if (materials.Count + inputs.Count != PersonalEquipmentRules.RepairMaterials(target.ItemKind).Count) continue;
            var plan = new EquipmentRepairActionPlan(actor, target.Id, target.ItemKind, household,
                site.InstanceId, site.Position, materials.AsReadOnly(), inputs.AsReadOnly(),
                Reach(actor, site.Position, ReachPurpose.EquipmentRepair, 0, 0, SwimmingPolicy.WhenCapable));
            if (CheckEquipmentRepairPlan(plan).Allowed) return plan;
        }
        return null;
    }

    private ActionCheck CheckEquipmentRepairPlan(EquipmentRepairActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, EquipmentRepairPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId ||
            RecheckGoods(EquipmentRepairTargetRequest(plan.Actor), plan.TargetId) is not { Lot: var target } ||
            target.ItemKind != plan.ItemKind)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected worn item is no longer available for repair");
        if (EquipmentRepairSite(plan.Actor, target) is not { } site || site.InstanceId != plan.BuildingId || site.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their equipment repair site has changed");
        foreach (var material in plan.Materials)
            if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(material.ItemKind)),
                    material.StockId) is not { } match || match.Quantity < material.Quantity || !PersonalEquipmentRules.IsCarried(match.Lot, plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material is no longer available");
        if (plan.Inputs.Sum(input => input.Quantity) > FreeCarryCapacity(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected repair inputs");
        foreach (var input in plan.Inputs)
        {
            var pickup = input.Pickup;
            if (HasCarriedOwnItem(plan.Actor, pickup.ItemKind))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry this repair input");
            if (RecheckGoods(SharedCollectionRequest(plan.Actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { } match ||
                match.Quantity < input.Quantity || match.Lot.ContainerLotId is not null || match.Lot.CarrierId is not null ||
                HouseholdStockPosition(match.Lot) != pickup.Position || HouseholdStockInteractionRange(match.Lot) != pickup.InteractionRange)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair supplies are no longer available");
            if (!CanPrepareSharedPickup(plan.Actor, match.Lot, plan.Destination, 0))
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected supplies cannot return to the repair site");
        }
        return agents[plan.Actor].Position == plan.Destination || Reach(plan.Actor, plan.Destination,
            ReachPurpose.EquipmentRepair, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the equipment repair site is out of reach now");
    }

    private void ApplyEquipmentRepairPlan(EquipmentRepairActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckEquipmentRepairPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (plan.Inputs.Count > 0) { ApplyPlannedEquipmentPickup(plan.Actor, plan.Inputs[0].Pickup); return; }
        var person = agents[plan.Actor];
        if (person.Position != plan.Destination) { MoveToward(plan.Actor, person, plan.Destination, "repair_equipment", 0); return; }
        StartEquipmentRepair(plan.Actor, person, plan.TargetId, plan.BuildingId, plan.Materials);
    }
}
