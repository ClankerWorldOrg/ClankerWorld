using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal sealed record ExpansionOrderPlan(PlacedBuilding Building, GridPoint Position,
        BuildingFootprintRevision Footprint);
    internal sealed record MaterialGatherEffect(string LotId, string ItemKind, int Quantity, string ResourceId);
    internal sealed record FoodHarvestEffect(string LotId, string ItemKind, int Quantity, string ResourceId);
    internal sealed record TreeOrderPlan(GridPoint Site, string Species, InventoryLot Seed, bool Carried);
    internal sealed record DeliveryNativeEffect(string OperationId, string LotId, int Quantity, bool Delivered);
    internal sealed record DeliveryOrderPlan(string Route, PlacedBuilding Destination, string DestinationOwnerId,
        InventoryLot Carrier, InventoryLot Resource, int TransferQuantity, int ResourceQuantity,
        bool TakeFromContainer = false, bool DirectDelivery = false);
}
