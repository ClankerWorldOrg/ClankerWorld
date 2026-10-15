using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static void CompleteHandcartBuildMaterial(IHandcartWorld world, HandcartBuildMaterialActionPlan plan) =>
        world.CollectCartMaterial(plan);

    internal static void CompleteHandcartRepair(IHandcartWorld world, HandcartRepairActionPlan plan, InventoryLot cart)
    {
        var actor = plan.Actor;
        var reservations = world.RepairCartInventory(plan, cart);
        world.RecordNonLandRepairCompletion(actor, cart, cart.Id, world.NativeCartRepairId(world.WorldTick, actor, cart.Id),
            world.Agents[actor].Position, reservations);
        world.AppendEvent("handcart_repaired", $"{actor}:{cart.Id}");
    }

    internal static void CompleteHandcartCargo(IHandcartWorld world, HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
    {
        var actor = plan.Actor;
        var quantity = plan.Quantity;
        if (plan.Choice.Kind == HandcartCargoKind.Load)
        {
            world.LoadCartInventory(plan, cart, lot);
            world.AppendEvent("handcart_loaded", $"{actor}:{cart.Id}:{lot.ItemKind}:{quantity}");
        }
        else
        {
            world.UnloadCartInventory(plan, cart, lot);
            world.AppendEvent("handcart_unloaded", $"{actor}:{cart.Id}:{lot.ItemKind}:{quantity}");
        }
    }
}
