using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal int RestaurantInputDemand(IMarketDemandWorld world, string actor, string itemKind, int spentPersonalQuantity = 0)
    {
        if (world.HouseholdFor(actor) is not { } householdId) return 0;
        var inventory = world.Society.Inventory;
        var restaurants = world.Buildings.Where(building => building.HouseholdId == householdId &&
            world.Content.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                definition.Tags.Contains("restaurant", StringComparer.Ordinal))).ToArray();
        var missing = restaurants.Sum(building => Math.Max(0, world.WorkstationInputTarget(building, itemKind) -
            world.WorkstationOnsiteQuantity(inventory, building, itemKind) -
            world.WorkstationIncomingQuantity(inventory, building, itemKind)));
        if (missing == 0) return 0;

        // Personal stock is usable only by its actual owner; other members'
        // uncommitted possessions are not a household promise.
        var carried = inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == itemKind &&
                lot.DeliveryBuildingId is null && PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor))
            .Where(lot => restaurants.Any(building => world.WorkstationInputTarget(building, itemKind) >
                    world.WorkstationOnsiteQuantity(inventory, building, itemKind) +
                    world.WorkstationIncomingQuantity(inventory, building, itemKind) &&
                world.DestinationRoom(inventory, building.InstanceId) > 0 &&
                (lot.ContainerLotId is not { } containerId ||
                 !world.HasActiveContainerReservation(inventory, containerId) &&
                 !world.UnusableDeliveryStock(inventory, inventory.GetLot(containerId)) &&
                 world.ContainerFamilyQuantity(inventory, containerId) <= world.DestinationRoom(inventory, building.InstanceId))))
            .Sum(lot => world.UsableWorkstationQuantity(inventory, lot));
        var householdStock = restaurants.Where(building => world.WorkstationInputTarget(building, itemKind) >
                world.WorkstationOnsiteQuantity(inventory, building, itemKind) + world.WorkstationIncomingQuantity(inventory, building, itemKind))
            .SelectMany(building => world.FindGoods(world.DeliveryGoodsRequest(actor, householdId, itemKind, building.InstanceId)).Matches
                .Where(match => world.WorkstationPickupQuantity(actor, inventory, match.Root, building.InstanceId, int.MaxValue) > 0))
            .Select(match => match.Lot).DistinctBy(lot => lot.Id)
            .Where(lot => lot.CarrierId is null &&
                (lot.StorageBuildingId is null || world.Buildings.Any(building =>
                    building.InstanceId == lot.StorageBuildingId && world.Content.Buildings.Any(definition =>
                        definition.CanonicalId == building.DefinitionId &&
                        definition.Tags.Any(tag => tag is "house" or "silo" or "farmhouse" or "restaurant")))))
            .GroupBy(lot => lot.StorageBuildingId)
            // Reserve the source's target once across all of its lots.
            .Sum(group => Math.Min(group.Sum(lot => world.UsableWorkstationQuantity(inventory, lot)),
                world.WorkstationSourceSurplus(inventory, group.First())));
        var purchases = OpenShopTrades(world.Shops, inventory).Where(pair => pair.Offer.ExpiryTick >= world.WorldTick &&
                pair.Trade.GoodsKind == itemKind &&
                world.Society.Agents.Any(person => person.Id == pair.Trade.BuyerId &&
                    person.HouseholdId == householdId && person.Status == SocietyAgentStatus.Active) &&
                pair.Offer.AcceptedBy.Contains(pair.Trade.BuyerId, StringComparer.Ordinal))
            .Where(pair => inventory.Lots.Any(lot => lot.Id == pair.Offer.FirstLotId &&
                    lot.OwnerId == pair.Trade.SellerHouseholdId &&
                    lot.StorageBuildingId == pair.Trade.BuildingInstanceId &&
                    lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0 &&
                    lot.Quantity >= pair.Offer.FirstQuantity) &&
                inventory.Reservations.Any(reservation => reservation.Id == DerivedId.Create(pair.Offer.Id + ":first") &&
                    reservation.State == InventoryReservationState.Reserved &&
                    reservation.Quantity >= pair.Offer.FirstQuantity))
            .Sum(pair => pair.Offer.FirstQuantity);
        return Math.Max(0, missing - Math.Max(0, carried - spentPersonalQuantity) - householdStock - purchases);
    }

    internal static bool ShopBuyerWantsWithoutRestaurant(IMarketDemandWorld world, string actor, InventoryLot lot)
    {
        if (world.MedicalSupplyWanted(actor, lot.ItemKind)) return true;
        if (world.WantsFieldPlantingStock(actor, lot.ItemKind)) return true;
        if (world.WantsOrnamentInput(actor, lot.ItemKind)) return true;
        var inventory = world.Society.Inventory;
        if (ToolProgressionRules.Find(lot.ItemKind) is { } offeredTool)
        {
            var carriedTool = ToolProgressionRules.BestUsableTool(inventory, actor, offeredTool.Family);
            return carriedTool is null ||
                ToolProgressionRules.Find(carriedTool.ItemKind)!.Tier < offeredTool.Tier;
        }
        if (PersonalEquipmentRules.IsGarment(lot.ItemKind))
        {
            var weather = world.WeatherAt(world.PhysicalAgents.Living[actor].Position);
            var bestProtection = inventory.Lots.Where(item =>
                    ToolProgressionRules.IsTopLevelCarriedLot(item, actor) &&
                    PersonalEquipmentRules.IsGarment(item.ItemKind) && world.AvailableLotQuantity(item) > 0)
                .Select(item => PersonalEquipmentRules.Protection(item, weather)).DefaultIfEmpty(0).Max();
            return PersonalEquipmentRules.Protection(lot, weather) > bestProtection;
        }
        if (world.WantsTradeItem(actor, lot)) return true;
        if (lot.ItemKind == "iron")
        {
            var household = world.HouseholdFor(actor);
            // Trial reserve for real iron-tool work, not a tool to equip.
            return world.HouseholdBuildingWithTag(household, "blacksmith") is not null &&
                world.HoldingsTotal(new[] { actor, household }.OfType<string>(), GoodsKinds.One("iron")) < 2;
        }
        if (PersonalEquipmentRules.IsCarryAid(lot.ItemKind))
            return PersonalEquipmentRules.Capacity(world.Society.Inventory, actor, world.PhysicalAgents.Living[actor].Equipment) <
                (lot.ItemKind == "leather_sack" ? 32 : lot.ItemKind == "sack" ? PersonalEquipmentRules.SackCapacity : PersonalEquipmentRules.BasketCapacity);
        return false;
    }
}
