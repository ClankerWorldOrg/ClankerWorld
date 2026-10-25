using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Read-only, deterministic selection. Recheck immediately before applying an action.</summary>
    public GoodsAnswer FindGoods(GoodsRequest request) => FindGoods(society.Checkpoint.Inventory, request);

    public GoodsMatch? RecheckGoods(GoodsRequest request, string lotId) =>
        RecheckGoods(society.Checkpoint.Inventory, request, lotId);

    private GoodsMatch? RecheckGoods(InventoryCheckpoint inventory, GoodsRequest request, string lotId)
    {
        GoodsInventoryQuery.Validate(request);
        var index = InventoryIndex.For(inventory);
        return index.Find(lotId) is { } lot && GoodsExclusion(request, index, lot) is null
            ? MakeGoodsMatch(index, lot, request) : null;
    }

    private GoodsAnswer FindGoods(InventoryCheckpoint inventory, GoodsRequest request)
    {
        GoodsInventoryQuery.Validate(request);
        var routes = request.Use is GoodsUse.Collect or GoodsUse.ReachableHoldings or GoodsUse.Recover or GoodsUse.Deliver or GoodsUse.Equip
            ? new Dictionary<(GridPoint Position, int Range), bool>() : null;
        var deliveries = request.Use == GoodsUse.Deliver ? new Dictionary<string, int>(StringComparer.Ordinal) : null;
        return GoodsInventoryQuery.FindGoods(inventory, request,
            (index, lot) => GoodsContextExclusion(request, index, lot, routes, deliveries),
            (index, lot) => MakeGoodsMatch(index, lot, request, deliveries));
    }

    private GoodsMatch MakeGoodsMatch(InventoryIndex index, InventoryLot lot, GoodsRequest request,
        Dictionary<string, int>? deliveries = null)
    {
        var quantity = request.Use == GoodsUse.Deliver ? DeliveryGoodsQuantity(request, index, lot, deliveries) : GoodsQuantity(index, lot, request);
        var root = index.Root(lot);
        var family = InventoryContainerRules.IsContainer(root.ItemKind) &&
            (InventoryContainerRules.IsContainer(lot.ItemKind) || request.Use is GoodsUse.Collect or GoodsUse.Deliver);
        var partialFarmLoad = request.Use == GoodsUse.Deliver && request.DeliveryPurpose == GoodsDeliveryPurpose.FarmStock &&
            (index.FamilyQuantity(root.Id) > DeliveryGoodsCapacity(request, root, deliveries) ||
             UnusableDeliveryStock(society.Checkpoint.Inventory, root));
        return new(lot, root, GoodsPlaceFor(root), quantity, family && !partialFarmLoad ? index.FamilyQuantity(root.Id) : quantity);
    }

    private GoodsReason? GoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes = null)
    {
        return GoodsInventoryQuery.Exclusion(request, index, lot) ?? GoodsContextExclusion(request, index, lot, routes);
    }

    private GoodsReason? GoodsContextExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes = null, Dictionary<string, int>? deliveries = null)
    {
        if (request.Use is GoodsUse.DepartureAllowance or GoodsUse.ToolRequestStock or GoodsUse.ProjectMaterialContribution) return null;
        if (request.Use == GoodsUse.MedicalDose) return MedicalDoseExclusion(request.Actor!, index, lot);
        if (request.Use == GoodsUse.HandcartControl) return HandcartControlExclusion(request.Actor!, lot);
        if (request.Use == GoodsUse.AnimalPayment) return AnimalPaymentExclusion(request.Actor!, index, lot);
        if (request.Use == GoodsUse.DismountCargo) return DismountCargoExclusion(request.Actor!, index, lot);
        if (request.Use == GoodsUse.AnimalSupplyTrip) return AnimalTripGoodsExclusion(request, index, lot);
        if (request.Use is GoodsUse.AnimalProduct or GoodsUse.AnimalHide) return AnimalProductGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.EquipmentRepair) return EquipmentRepairGoodsExclusion(request.Actor!, index, lot);
        if (request.Use == GoodsUse.Equip) return EquippingGoodsExclusion(request, index, lot, routes);
        if (request.Use == GoodsUse.TreePlanting) return TreePlantingGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.FieldPlanting) return FieldPlantingGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.CarriedInputs) return CarriedInputGoodsExclusion(request.Actor!, index, lot);
        if (request.Use is GoodsUse.SpareCargo or GoodsUse.OrchardFoodRecovery) return SpareCargoGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.CarriedDelivery) return CarriedDeliveryGoodsExclusion(request.Actor!, index, lot);
        if (request.Use == GoodsUse.Salvage) return SalvageGoodsExclusion(request.Actor!, index, lot);
        if (GoodsInventoryQuery.IsRecovery(request.Use)) return RecoveryGoodsExclusion(request, index, lot, routes);
        if (request.Use is GoodsUse.Spend or GoodsUse.Sell or GoodsUse.StorePersonal)
            return SpendingGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.Cargo)
            return lot.OwnerId == request.Actor && index.Root(lot).ItemKind == InventoryContainerRules.Handcart
                ? null : GoodsReason.Custody;
        if (request.Use == GoodsUse.Eat)
        {
            var root = index.Root(lot);
            if (!FoodRules.IsEdible(lot.ItemKind)) return GoodsReason.Kind;
            if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } eatingPromise) return eatingPromise;
            return PersonalEquipmentRules.IsCarried(root, request.Actor!) ? null : GoodsReason.Custody;
        }
        if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } promise) return promise;
        if (request.Use == GoodsUse.Deliver) return DeliveryGoodsExclusion(request, index, lot, routes, deliveries);
        if (request.Use == GoodsUse.Collect) return CollectGoodsExclusion(request.Actor!, index, lot, routes);
        if (request.Use == GoodsUse.ReachableHoldings) return GoodsRouteExclusion(request.Actor!, index.Root(lot), routes);
        return null;
    }

    private GoodsPlace GoodsPlaceFor(InventoryLot root)
    {
        if (root.StorageBuildingId is { } id)
            return new(GoodsPlaceKind.Stored, id,
                worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == id)?.Position,
                DeliveryBuildingId: root.DeliveryBuildingId);
        if (root.GroundPosition is { } ground)
            return new(GoodsPlaceKind.Ground, Position: new GridPoint(ground.X, ground.Y), DeliveryBuildingId: root.DeliveryBuildingId);
        var carrier = root.CarrierId ?? (agents.ContainsKey(root.OwnerId) ? root.OwnerId : null);
        return carrier is not null
            ? new(GoodsPlaceKind.Carried, Position: agents.GetValueOrDefault(carrier)?.Position,
                CarrierId: carrier, DeliveryBuildingId: root.DeliveryBuildingId)
            : new(GoodsPlaceKind.Unlocated, DeliveryBuildingId: root.DeliveryBuildingId);
    }

    /// <summary>Free physical storage after production, trade and inbound delivery promises.</summary>
    public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) =>
        DestinationRoom(society.Checkpoint.Inventory, buildingId, movingRoot);

    private int DestinationRoom(InventoryCheckpoint inventory, string buildingId, InventoryLot? movingRoot = null,
        int outgoingQuantity = 0, int releasedShopReservation = 0)
    {
        var building = worldSimulation.Buildings.Single(item => item.InstanceId == buildingId);
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (BuildingStorageRules.Capacity(definition, building) is not { } capacity) return int.MaxValue;
        var index = InventoryIndex.For(inventory);
        // Resolve the current family; caller-supplied location or quantities never release space.
        var moving = movingRoot is not null && index.Find(movingRoot.Id) is { } current ? index.Root(current) : null;
        // Stock-supply trips retain their destination outside the inventory delivery marker.
        // Care and saddling trips consume or equip their inputs rather than storing them.
        var yardInbound = animalWorld.SupplyTrips.Where(trip => trip.YardId == buildingId && trip.Action is null)
            .Select(trip => index.Find(trip.LotId)).OfType<InventoryLot>()
            .Where(root => root.StorageBuildingId != buildingId)
            .SelectMany(root => new[] { root }.Concat(index.ContentsOf(root.Id)));
        var inbound = index.InboundTo(buildingId).Concat(yardInbound).DistinctBy(lot => lot.Id)
            .Where(lot => moving is null || lot.Id != moving.Id && lot.ContainerLotId != moving.Id)
            .Sum(lot => (long)lot.Quantity);
        var used = (long)StoredQuantity(buildingId, inventory) + ReservedStorageGrowth(buildingId, inventory) +
            this.marketSystem.ReservedShopStorageSpace(this.MarketShopWorld, buildingId, inventory) + inbound;
        return (int)Math.Clamp(capacity - used + outgoingQuantity + releasedShopReservation, 0, int.MaxValue);
    }

    private int GoodsQuantity(InventoryIndex index, InventoryLot lot, GoodsRequest request)
    {
        var quantity = request.Use == GoodsUse.TreePlanting ? TreePlantingGoodsQuantity(index, lot)
            : request.Use == GoodsUse.FieldPlanting ? FieldPlantingGoodsQuantity(request, index, lot)
            : GoodsInventoryQuery.Quantity(index, lot, request.Use);
        if (request.Use is not (GoodsUse.Collect or GoodsUse.Recover) || PersonalEquipmentRules.IsCarried(index.Root(lot), request.Actor!))
            return quantity;
        var room = FreeCarryCapacity(request.Actor!);
        var root = index.Root(lot);
        return InventoryContainerRules.IsContainer(root.ItemKind)
            ? index.FamilyQuantity(root.Id) <= room ? quantity : 0 : Math.Min(quantity, room);
    }

    private GoodsReason? CollectGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes)
    {
        if (!agents.TryGetValue(actor, out var person)) return GoodsReason.Custody;
        var root = index.Root(lot);
        if (root.CarrierId is { } carrier && carrier != actor) return GoodsReason.Custody;
        if (!MayCollectGoodsAt(actor, root)) return GoodsReason.Place;
        if (InventoryContainerRules.IsContainer(root.ItemKind) && !InventoryRules.CanMoveFamily(index, root, InventoryHoldRules.BorrowedBy(actor)))
            return GoodsReason.Reservation;
        if (handcartHitches.Any(hitch => hitch.CartLotId == root.Id) || agents.Values.Any(person =>
                PersonalEquipmentRules.IsSelected(person.Equipment, root.Id))) return GoodsReason.Custody;
        if (PersonalEquipmentRules.IsCarried(root, actor)) return null;
        var room = FreeCarryCapacity(actor);
        if (room <= 0 || InventoryContainerRules.IsContainer(root.ItemKind) && index.FamilyQuantity(root.Id) > room)
            return GoodsReason.CarryRoom;
        return GoodsRouteExclusion(actor, root, routes);
    }

    private GoodsReason? GoodsRouteExclusion(string actor, InventoryLot root,
        Dictionary<(GridPoint Position, int Range), bool>? routes)
    {
        if (!agents.ContainsKey(actor)) return GoodsReason.Custody;
        if (PersonalEquipmentRules.IsCarried(root, actor)) return null;
        var key = (HouseholdStockPosition(root), HouseholdStockInteractionRange(root));
        if (routes is not null && routes.TryGetValue(key, out var reachable)) return reachable ? null : GoodsReason.Route;
        reachable = Reach(actor, key.Item1, ReachPurpose.GoodsCollection, key.Item2, 0, SwimmingPolicy.WhenCapable).Count > 0;
        routes?.Add(key, reachable);
        return reachable ? null : GoodsReason.Route;
    }

    private bool MayCollectGoodsAt(string actor, InventoryLot root)
    {
        if (root.OwnerId == actor)
            return PersonalEquipmentRules.IsCarried(root, actor) || MayRecoverPersonalGoodsAt(actor, root) && PersonalRecoveryPropertyExclusion(actor, InventoryIndex.For(society.Checkpoint.Inventory), root) is null;
        if (root.OwnerId == HouseholdFor(actor)) return true;
        if (PersonalEquipmentRules.IsCarried(root, actor) && worldSimulation.Buildings.Any(building =>
                building.HouseholdId == root.OwnerId && worldContent.Buildings.Any(definition =>
                    definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag)) &&
                MaySupplyAnimalYard(actor, building))) return true;
        if (root.StorageBuildingId is not { } storage) return false;
        var building = worldSimulation.Buildings.FirstOrDefault(item => item.InstanceId == storage);
        if (building is null) return false;
        if (building.TownId == root.OwnerId && MayCollectWarehouseStock(actor, building)) return true;
        return building.HouseholdId == root.OwnerId && worldContent.Buildings.Any(definition =>
            definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag)) &&
            MaySupplyAnimalYard(actor, building);
    }

    private GoodsRequest? PersonalRecoveryRequest(string actor, GoodsUse use, string? kind = null)
    {
        var kinds = kind is not null ? new[] { kind } : InventoryIndex.For(society.Checkpoint.Inventory)
            .OwnedBy(actor).Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? null : new(use, actor, GoodsOwners.One(actor), new GoodsKinds(kinds));
    }

    private IEnumerable<InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) =>
        PersonalRecoveryRequest(actor, GoodsUse.Recover, kind) is { } request
            ? FindGoods(request).Matches.Select(match => match.Lot) : [];

    private GoodsReason? RecoveryGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes)
    {
        var actor = request.Actor!;
        if (!agents.ContainsKey(actor)) return GoodsReason.Custody;
        if (PersonalRecoveryPropertyExclusion(actor, index, lot) is { } reason) return reason;
        if (!MayRecoverPersonalGoodsAt(actor, lot)) return GoodsReason.Place;
        if (request.Use == GoodsUse.RecoveryHoldings) return null;
        var room = FreeCarryCapacity(actor);
        if (room <= 0 || InventoryContainerRules.IsContainer(lot.ItemKind) && index.FamilyQuantity(lot.Id) > room)
            return GoodsReason.CarryRoom;
        return PersonalRecoveryRouteExclusion(actor, lot, routes);
    }

    private GoodsReason? PersonalRecoveryPropertyExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        // Recovery moves physical property, including damaged vessels and spoiled contents.
        // A parked cart is pulled with its cargo rather than carried as a recovery load.
        if (lot.OwnerId != actor) return GoodsReason.Owner;
        if (PersonalEquipmentRules.IsCarried(lot, actor) || lot.CarrierId is not null) return GoodsReason.Custody;
        if (lot.ItemKind == InventoryContainerRules.Handcart || lot.ContainerLotId is not null) return GoodsReason.Place;
        if (MarketTradeRules.IsLoose(lot) && MarketSystem.OnMarketStall(this.MarketStallWorld, lot)) return GoodsReason.MarketStall;
        if (InventoryHoldRules.For(index, lot).Any(hold => hold.Purpose == InventoryHoldPurpose.Delivery)) return GoodsReason.Delivery;
        if (Math.Max(0, InventoryRules.UnreservedQuantity(index, lot)) <= 0 ||
            InventoryContainerRules.IsContainer(lot.ItemKind) && !InventoryRules.CanMoveFamily(index, lot))
            return GoodsReason.Reservation;
        return null;
    }

    private bool MayRecoverPersonalGoodsAt(string actor, InventoryLot lot) => lot.GroundPosition is not null ||
        lot.StorageBuildingId is { } storageId && worldSimulation.Buildings.Any(building => building.InstanceId == storageId &&
            (building.HouseholdId is { } home && (society.Checkpoint.GetAgent(actor).HouseholdId == home ||
             agents[actor].Departures?.Any(departure => departure.HouseholdId == home) == true ||
             HasCareGroupDepartureFrom(actor, home) || IsStoredSettledBequest(actor, lot, storageId)) ||
             towns.Any(town => town.LandRecords.Cases.Any(item => item.Property?.Transfer?.PriorBuilding.InstanceId == storageId))));

    private GoodsReason? PersonalRecoveryRouteExclusion(string actor, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes)
    {
        var position = agents[actor].Position;
        var key = (HouseholdStockPosition(lot), lot.GroundPosition is not null ? ResourceInteractionRange : 1);
        if (routes is not null && routes.TryGetValue(key, out var reachable)) return reachable ? null : GoodsReason.Route;
        reachable = IsWithinInteractionRange(position, key.Item1, key.Item2) ||
            Reach(actor, key.Item1, ReachPurpose.GoodsRecovery, key.Item2, 0, SwimmingPolicy.WhenCapable).Count > 0;
        routes?.Add(key, reachable);
        return reachable ? null : GoodsReason.Route;
    }

    private GoodsReason? SalvageGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.TryGetValue(actor, out var person)) return GoodsReason.Custody;
        if (towns.SingleOrDefault(town => town.Id == lot.OwnerId) is not { IsAbandoned: true } town)
            return GoodsReason.Owner;
        if (lot.CarrierId is not null || lot.ContainerLotId is not null) return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (lot.GroundPosition is null && (lot.StorageBuildingId is not { } storage ||
            !WarehousesForTown(town.Id).Any(building => building.InstanceId == storage && building.HouseholdId is null)))
            return GoodsReason.Place;
        var range = lot.GroundPosition is not null ? ResourceInteractionRange : 0;
        if (!IsWithinInteractionRange(person.Position, HouseholdStockPosition(lot), range)) return GoodsReason.Route;
        var access = InventoryHoldRules.BorrowedBy(actor);
        if (index.ContentsOf(lot.Id).Prepend(lot).Any(member => !InventoryHoldRules.Allows(index, member, access)))
            return GoodsReason.Custody;
        return null;
    }

    // A single request owns the common quantity/commitment rules. Callers add
    // their existing physical-place, food-reserve and business-purpose rules.
    private IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners,
        string? kind = null, string? building = null, InventoryCheckpoint? inventory = null)
    {
        inventory ??= society.Checkpoint.Inventory;
        var index = InventoryIndex.For(inventory);
        var kinds = kind is not null ? new[] { kind } : owners.SelectMany(index.OwnedBy)
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        if (kinds.Length == 0) return [];
        return FindGoods(inventory, new(use, actor, new(owners), new(kinds), AtBuilding: building)).Matches;
    }

    private GoodsReason? SpendingGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var actor = request.Actor!;
        if (!agents.ContainsKey(actor)) return GoodsReason.Custody;
        var root = index.Root(lot);
        if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } promise) return promise;
        if (request.Use == GoodsUse.StorePersonal)
        {
            // Storing physical property keeps damage/spoilage, just as relocation
            // does; it is not a usable-goods spending or recovery action.
            if (lot.OwnerId != actor) return GoodsReason.Owner;
            if (lot.ContainerLotId is not null) return GoodsReason.Place;
            if (!PersonalEquipmentRules.IsCarried(lot, actor) ||
                PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id)) return GoodsReason.Custody;
            if (InventoryContainerRules.IsContainer(lot.ItemKind) && !InventoryRules.CanMoveFamily(index, lot))
                return GoodsReason.Reservation;
            return null;
        }
        // Sale stock on a borrowed stall remains available to its actual seller;
        // the Market caller still validates the exact live occupancy and stock receipt.
        return null;
    }
}
