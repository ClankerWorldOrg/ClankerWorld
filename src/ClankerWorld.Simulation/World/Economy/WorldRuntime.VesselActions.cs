using System.Globalization;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void ApplyVesselPlan(VesselActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedVesselPlan(plan);
    }

    private void ApplyCheckedVesselPlan(VesselActionPlan plan)
    {
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
                RunInventoryCommand(InventorySystem.CollectWaterJug(InventoryEconomyWorld, plan));
                AppendEvent("water_jug_collected", $"{plan.Actor}:{plan.VesselId}");
                break;
            case VesselActionKind.FillJug:
                RunInventoryCommand(InventorySystem.FillCarriedWaterJug(InventoryEconomyWorld, plan));
                AppendEvent("water_jug_filled", $"{plan.Actor}:{plan.VesselId}:{plan.Quantity}:{plan.Target.X},{plan.Target.Y}");
                break;
            case VesselActionKind.ReturnJug:
                RunInventoryCommand(InventorySystem.ReturnFilledWaterJug(InventoryEconomyWorld, plan));
                AppendEvent("water_jug_returned", $"{plan.Actor}:{plan.VesselId}:{plan.HouseId}");
                break;
            case VesselActionKind.ReturnEmpty:
                RunInventoryCommand(InventorySystem.CollectEmptyVessel(InventoryEconomyWorld, plan));
                AppendEvent("empty_vessel_picked_up", $"{plan.Actor}:{plan.VesselId}:{plan.SourceBuildingId}:{plan.HouseId}");
                break;
            case VesselActionKind.TakeFood:
                RunInventoryCommand(InventorySystem.TakeFoodFromPot(InventoryEconomyWorld, plan));
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
        RunInventoryCommand(InventorySystem.StorePlannedPotFood(plan, food, operationId));
        AppendEvent("food_stored_in_pot", $"{plan.Actor}:{plan.VesselId}:{food.StockId}:{plan.Quantity}");
    }
}
