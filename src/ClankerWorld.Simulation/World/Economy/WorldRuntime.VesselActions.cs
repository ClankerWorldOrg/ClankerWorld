using System.Globalization;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void ApplyVesselPlan(VesselActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckVesselPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var state = agents[plan.Actor];
        var kind = plan.Choice.Kind;
        var movement = kind switch
        {
            VesselActionKind.CollectJug => "collect_water_jug",
            VesselActionKind.FillJug => "fill_water_jug",
            VesselActionKind.ReturnJug => "return_water_jug",
            VesselActionKind.StoreFood => "store_food_in_pot",
            VesselActionKind.TakeFood => "take_food_from_pot",
            _ => "return_empty_vessel",
        };
        if (state.Position != plan.Target) { MoveToward(plan.Actor, state, plan.Target, movement, 0); return; }
        switch (kind)
        {
            case VesselActionKind.CollectJug:
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"water-jug-pickup:{WorldTick}:{plan.Actor}", plan.HouseholdId, plan.Actor, plan.VesselId, 1,
                    "water_jug_collected", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
                AppendEvent("water_jug_collected", $"{plan.Actor}:{plan.VesselId}");
                break;
            case VesselActionKind.FillJug:
                ApplyInventoryTransition(inventory => InventoryAuthority.AddLot(inventory,
                    $"{plan.VesselId}#water:{WorldTick.ToString(CultureInfo.InvariantCulture)}", InventoryContainerRules.FreshWater,
                    plan.Actor, plan.Quantity, WorldTick, containerLotId: plan.VesselId));
                AppendEvent("water_jug_filled", $"{plan.Actor}:{plan.VesselId}:{plan.Quantity}:{plan.Target.X},{plan.Target.Y}");
                break;
            case VesselActionKind.ReturnJug:
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"water-jug-return:{WorldTick}:{plan.Actor}", plan.Actor, plan.HouseholdId, plan.VesselId, 1,
                    "water_jug_returned", destinationStorageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));
                AppendEvent("water_jug_returned", $"{plan.Actor}:{plan.VesselId}:{plan.HouseId}");
                break;
            case VesselActionKind.ReturnEmpty:
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"empty-vessel-pickup:{WorldTick}:{plan.Actor}", plan.HouseholdId, plan.Actor, plan.VesselId, 1,
                    "empty_vessel_picked_up", destinationDeliveryBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
                AppendEvent("empty_vessel_picked_up", $"{plan.Actor}:{plan.VesselId}:{plan.SourceBuildingId}:{plan.HouseId}");
                break;
            case VesselActionKind.TakeFood:
                ApplyInventoryTransition(inventory => InventoryAuthority.TakeFromContainer(inventory,
                    $"food-pot-take:{WorldTick}:{plan.Actor}", plan.HouseholdId, plan.Actor, plan.VesselId, plan.Food!.StockId,
                    plan.Quantity, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
                AppendEvent("food_taken_from_pot", $"{plan.Actor}:{plan.VesselId}:{plan.Food!.StockId}:{plan.Quantity}");
                break;
            case VesselActionKind.StoreFood:
                StorePlannedPotFood(plan);
                break;
        }
    }

    private void StorePlannedPotFood(VesselActionPlan plan)
    {
        var food = plan.Food!;
        var operationId = FoodPotTransferId(WorldTick, plan.Actor);
        ApplyInventoryTransition(inventory =>
        {
            var sourceId = food.StockId;
            if (food.OwnerId == plan.Actor)
            {
                var source = inventory.GetLot(sourceId);
                inventory = InventoryAuthority.Transfer(inventory, operationId, plan.Actor, plan.HouseholdId, sourceId,
                    plan.Quantity, "food_stored_in_pot", destinationStorageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId));
                if (plan.Quantity != source.Quantity) sourceId = DerivedId.Create($"{sourceId}#transfer:{operationId}");
            }
            return InventoryAuthority.PutIntoContainer(inventory, operationId, plan.HouseholdId, plan.VesselId, sourceId,
                plan.Quantity, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor));
        });
        AppendEvent("food_stored_in_pot", $"{plan.Actor}:{plan.VesselId}:{food.StockId}:{plan.Quantity}");
    }
}
