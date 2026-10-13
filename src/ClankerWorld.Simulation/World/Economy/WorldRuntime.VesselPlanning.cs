using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private VesselActionPlan CreateVesselPlan(string actor, VesselChoice choice, string household,
        PlacedBuilding house, InventoryLot vessel, GridPoint target, int quantity,
        PotFoodChoice? food = null, GridPoint? shore = null, GridPoint? returnTo = null) =>
        new(actor, choice, household, house.InstanceId, house.Position, vessel.Id, vessel.OwnerId, vessel.ItemKind,
            target, vessel.StorageBuildingId, quantity,
            choice.Kind is VesselActionKind.StoreFood or VesselActionKind.TakeFood ? null : GoodsPlanFamily.Capture(society.Checkpoint.Inventory, vessel),
            food is null ? null : new(food.Food.Id, food.Food.OwnerId, food.Food.ItemKind), shore, returnTo,
            Reach(actor, target, ReachPurpose.HouseholdVessel, 0, 0, SwimmingPolicy.WhenCapable));

    private VesselActionPlan? PlanVesselAction(string actor, VesselChoice choice,
        InventoryLot? selectedVessel = null, PotFoodChoice? selectedFood = null)
    {
        if (HouseholdFor(actor) is not { } household || HouseForHousehold(household) is not { } house) return null;
        var inventory = society.Checkpoint.Inventory;
        switch (choice.Kind)
        {
            case VesselActionKind.CollectJug:
                return PlanStoredJugPickup(actor, choice, household, house);
            case VesselActionKind.FillJug:
                if (choice.Shore is not { } shore || (selectedVessel ?? CarriedWaterJugForRefill(actor)) is not { } jug) return null;
                var destination = WaterJugRefillReturnDestination(actor, jug);
                return CreateVesselPlan(actor, choice, household, house, jug, shore,
                    WaterFillQuantity(actor, jug, shore, destination), shore: shore, returnTo: destination);
            case VesselActionKind.ReturnJug:
                return (selectedVessel ?? CarriedWaterJugForReturn(actor, house.InstanceId)) is { } returning
                    ? CreateVesselPlan(actor, choice, household, house, returning, house.Position, 1) : null;
            case VesselActionKind.StoreFood:
                if ((selectedFood ?? FindFoodToStore(actor, household, house.InstanceId)) is not { } storage) return null;
                var room = storage.Food.OwnerId == actor ? DestinationRoom(house.InstanceId) : int.MaxValue;
                var quantity = Math.Min(AvailableLotQuantity(storage.Food) - (storage.Food.OwnerId == actor ? 1 : 0),
                    Math.Min(InventoryContainerRules.StoragePotCapacity - ContainerContentsQuantity(inventory, storage.Pot.Id), room));
                return CreateVesselPlan(actor, choice, household, house, storage.Pot, house.Position, quantity, storage);
            case VesselActionKind.TakeFood:
                return (selectedFood ?? FindFoodInPot(household, house.InstanceId)) is { } serving
                    ? CreateVesselPlan(actor, choice, household, house, serving.Pot, house.Position,
                        Math.Min(1, AvailableLotQuantity(serving.Food)), serving) : null;
            case VesselActionKind.ReturnEmpty:
                return PlanEmptyVesselPickup(actor, choice, household, house);
            default:
                return null;
        }
    }

    private int WaterFillQuantity(string actor, InventoryLot jug, GridPoint shore, GridPoint? returnTo)
    {
        var quantity = Math.Min(InventoryContainerRules.WaterJugCapacity -
            ContainerContentsQuantity(society.Checkpoint.Inventory, jug.Id), FreeCarryCapacity(actor));
        return returnTo is { } destination ? Math.Min(quantity, PickupCarryCapacity(actor, shore, destination)) : quantity;
    }

    private VesselActionPlan? PlanStoredJugPickup(string actor, VesselChoice choice, string household, PlacedBuilding house)
    {
        foreach (var match in FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(household),
                     GoodsKinds.One(InventoryContainerRules.WaterJug), ExtraUnits: 1, ContentsKind: InventoryContainerRules.FreshWater)).Matches)
        {
            var jug = match.Lot;
            if (WaterJugStockLocation(jug, household) is not { } source ||
                FindFreshWaterShore(actor, source.Position, match.MoveUnits, house.Position) is not { } shore) continue;
            var plan = CreateVesselPlan(actor, choice, household, house, jug, source.Position, 1, shore: shore, returnTo: house.Position);
            if (CheckVesselPlan(plan).Allowed) return plan;
        }
        return null;
    }

    private VesselActionPlan? PlanEmptyVesselPickup(string actor, VesselChoice choice, string household, PlacedBuilding house)
    {
        foreach (var match in FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(household),
                     new GoodsKinds([InventoryContainerRules.StoragePot, InventoryContainerRules.WaterJug, InventoryContainerRules.Handcart])))
                     .Matches.Where(match => choice.StockId is null || match.Lot.Id == choice.StockId).OrderBy(match => match.Lot.Id, StringComparer.Ordinal))
        {
            var vessel = match.Lot;
            if (worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == vessel.StorageBuildingId) is not { } source) continue;
            var plan = CreateVesselPlan(actor, choice with { StockId = vessel.Id }, household, house, vessel, source.Position, 1);
            if (CheckVesselPlan(plan).Allowed) return plan;
        }
        return null;
    }
}
