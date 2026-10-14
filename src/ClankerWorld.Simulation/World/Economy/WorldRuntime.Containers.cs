using System.Globalization;
using System.Runtime.CompilerServices;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>Physical storage and fresh-water actions for reusable household vessels.</summary>
public sealed partial class WorldRuntime
{
    private const string FillWaterJugPrefix = "fill_water_jug:";
    private static readonly ConditionalWeakTable<WorldMap, GridPoint[]> FreshWaterShoreCache = new();

    private sealed record PotFoodChoice(InventoryLot Pot, InventoryLot Food);

    private IEnumerable<InventoryLot> CarriedContainers(string actor, string kind) =>
        FindGoods(new(GoodsUse.ReturnVessel, actor, GoodsOwners.One(actor), GoodsKinds.One(kind)))
            .Matches.Where(match => PersonalEquipmentRules.IsCarried(match.Lot, actor)).Select(match => match.Lot);

    // Presence is physical: a reserved or damaged jug still occupies the agent's hands.
    private InventoryLot? CarriedContainer(string actor, string kind) => InventoryIndex.For(society.Checkpoint.Inventory)
        .OwnedBy(actor).FirstOrDefault(lot => lot.ItemKind == kind && lot.DeliveryBuildingId is null &&
            PersonalEquipmentRules.IsCarried(lot, actor));

    private InventoryLot? CarriedWaterJugForRefill(string actor) =>
        FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.WaterJug),
            ExtraUnits: 1, ContentsKind: InventoryContainerRules.FreshWater))
            .Matches.FirstOrDefault(match => PersonalEquipmentRules.IsCarried(match.Lot, actor))?.Lot;

    private InventoryLot? CarriedWaterJugForReturn(string actor, string houseId)
    {
        var inventory = society.Checkpoint.Inventory;
        var room = DestinationRoom(houseId);
        var refillJug = CarriedWaterJugForRefill(actor);
        if (refillJug is not null && ContainerFamilyQuantity(inventory, refillJug.Id) <= room)
            return refillJug;
        return CarriedContainers(actor, InventoryContainerRules.WaterJug).FirstOrDefault(jug =>
            ContainerFamilyQuantity(inventory, jug.Id) <= room);
    }

    private static int ContainerContentsQuantity(InventoryCheckpoint inventory, string containerId) =>
        InventoryIndex.For(inventory).ContentsQuantity(containerId);

    private static int ContainerFamilyQuantity(InventoryCheckpoint inventory, string containerId) =>
        InventoryIndex.For(inventory).FamilyQuantity(containerId);

    private static bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) =>
        InventoryIndex.For(inventory).HasReservedFamily(containerId);

    private void AddContainerCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState person)
    {
        if (!IsAdult(actor) || society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house)
            return;

        var inventory = society.Checkpoint.Inventory;
        var refillJug = CarriedWaterJugForRefill(actor);
        var carriedJug = refillJug ?? CarriedContainer(actor, InventoryContainerRules.WaterJug);
        if (carriedJug is not null)
        {
            var shore = refillJug is not null && FreeCarryCapacity(actor) > 0
                ? FindFreshWaterShore(actor, person.Position, returnTo: WaterJugRefillReturnDestination(actor, carriedJug))
                : null;
            if (shore is { } fillAt)
            {
                var pointText = $"{fillAt.X.ToString(CultureInfo.InvariantCulture)},{fillAt.Y.ToString(CultureInfo.InvariantCulture)}";
                candidates.Add(new CognitionCandidate(
                    $"{FillWaterJugPrefix}{pointText}",
                    "Fill the water jug at a reachable riverbank or lakeshore.", 18,
                    $"shore:{pointText}"));
            }
            else if (CarriedWaterJugForReturn(actor, house.InstanceId) is not null &&
                     FindUnoccupiedRoute(actor, person.Position, house.Position, 0).Count > 0)
            {
                candidates.Add(new CognitionCandidate("return_water_jug",
                    "Carry the water jug back to its household House.", 22, house.InstanceId));
            }
        }
        else
        {
            var storedJug = StoredWaterJugForRefill(actor, person, householdId);
            if (storedJug is not null && WaterJugStockLocation(storedJug, householdId) is { } source &&
                FindFreshWaterShore(actor, source.Position, ContainerFamilyQuantity(inventory, storedJug.Id), house.Position) is not null &&
                (person.Position == source.Position ||
                 FindUnoccupiedRoute(actor, person.Position, source.Position, 0).Count > 0))
            {
                candidates.Add(new CognitionCandidate("collect_water_jug",
                    "Collect the household's reusable jug before fetching fresh water.", 26, storedJug.Id));
            }
        }

        AddEmptyVesselReturnCandidate(candidates, actor, person);
        AddFoodPotCandidates(candidates, actor, person, householdId, house);
    }

    private void AddFoodPotCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState person, string householdId, PlacedBuilding house)
    {
        var inventory = society.Checkpoint.Inventory;
        var foodToStore = FindFoodToStore(actor, householdId, house.InstanceId);
        if (foodToStore is not null &&
            FindUnoccupiedRoute(actor, person.Position, house.Position, 0).Count > 0)
        {
            candidates.Add(new CognitionCandidate("store_food_in_pot",
                "Put available household food into its storage pot.", 24, foodToStore.Pot.Id));
        }

        AddFoodPotRetrievalCandidate(candidates, actor, person, householdId, house);
    }

    private void AddUrgentFoodPotCandidate(List<CognitionCandidate> candidates, string actor,
        AgentState person)
    {
        if (!NeedsUrgentFood(person) || society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house)
            return;

        AddFoodPotRetrievalCandidate(candidates, actor, person, householdId, house);
    }

    private void AddFoodPotRetrievalCandidate(List<CognitionCandidate> candidates, string actor,
        AgentState person, string householdId, PlacedBuilding house)
    {
        var inventory = society.Checkpoint.Inventory;
        // Like collecting other household food, a serving follows the Town's
        // food policy, and only food that can be eaten now is offered.
        if (person.FullnessBasisPoints < 7_000 && MayCollectSharedFood(actor) &&
            !FindGoods(new(GoodsUse.Eat, actor, GoodsOwners.One(actor), EdibleFoodGoods))
                .Matches.Any(match => match.Lot.ContainerLotId is null) &&
            FreeCarryCapacity(actor) > 0 &&
            FindFoodInPot(householdId, house.InstanceId) is { } storedFood &&
            FindUnoccupiedRoute(actor, person.Position, house.Position, 0).Count > 0)
        {
            candidates.Add(new CognitionCandidate("take_food_from_pot",
                "Take one serving from the household's storage pot.",
                NeedsUrgentFood(person) ? 1 : 4, storedFood.Food.Id));
        }
    }

    private PotFoodChoice? FindFoodToStore(string actor, string householdId, string houseId)
    {
        var inventory = society.Checkpoint.Inventory;
        var houseStorageRoom = DestinationRoom(houseId);
        var pots = FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(householdId),
            GoodsKinds.One(InventoryContainerRules.StoragePot), AtBuilding: houseId, ExtraUnits: 1));
        if (pots.First is not { } pot) return null;
        var food = FindGoods(new(GoodsUse.ConsumeAt, actor, new GoodsOwners([actor, householdId]),
                new GoodsKinds(FoodRules.StorageKinds))).Matches.Where(match =>
                match.Lot.ContainerLotId is null && match.Quantity > (match.Lot.OwnerId == actor ? 1 : 0) &&
                (match.Lot.OwnerId == actor && houseStorageRoom > 0 &&
                    PersonalEquipmentRules.IsCarried(match.Lot, actor) && match.Lot.DeliveryBuildingId is null ||
                 match.Lot.OwnerId == householdId && match.Lot.StorageBuildingId == houseId))
            .OrderBy(match => match.Lot.OwnerId == actor ? 0 : 1).ThenBy(match => match.Lot.Id, StringComparer.Ordinal)
            .FirstOrDefault()?.Lot;
        if (food is not null) return new PotFoodChoice(pot.Lot, food);
        return null;
    }

    private GridPoint[] FreshWaterShorePositions() =>
        FreshWaterShoreCache.GetValue(map, static currentMap =>
        {
            var shores = new HashSet<GridPoint>();
            foreach (var water in currentMap.Tiles.Select(tile => tile.Position)
                         .Where(point => currentMap.HydrologyAt(point) is WaterKind.River or WaterKind.Lake))
            {
                foreach (var (dx, dy) in new (int X, int Y)[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                {
                    var candidate = currentMap.WrapColumn(new GridPoint(water.X + dx, water.Y + dy));
                    if (currentMap.Contains(candidate) && currentMap.HydrologyAt(candidate) == WaterKind.Land &&
                        currentMap.IsPassable(candidate))
                        shores.Add(candidate);
                }
            }
            return shores.OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
        });

    private GridPoint? FindFreshWaterShore(string actor, GridPoint origin, int additionalCarriedUnits = 0,
        GridPoint? returnTo = null)
    {
        foreach (var point in FreshWaterShorePositions()
                     .Where(point => CanReachByFootOrSwimming(actor, origin, point))
                     .OrderBy(point => map.FootDistance(origin, point))
                     .ThenBy(point => point.Y).ThenBy(point => point.X))
        {
            if ((point == origin || FindUnoccupiedRoute(actor, origin, point, 0,
                    additionalCarriedUnits: additionalCarriedUnits).Count > 0) &&
                (returnTo is null || PickupCarryCapacity(actor, point, returnTo.Value) > additionalCarriedUnits))
                return point;
        }
        return null;
    }

    private PlacedBuilding? WaterJugStockLocation(InventoryLot lot, string householdId) =>
        worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == lot.StorageBuildingId &&
            building.HouseholdId == householdId && worldContent.Buildings.Any(definition =>
                definition.CanonicalId == building.DefinitionId && definition.Tags.Any(tag => tag is "house" or "restaurant")));

    private InventoryLot? StoredWaterJugForRefill(string actor, AgentState person, string householdId)
    {
        var inventory = society.Checkpoint.Inventory;
        return FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(householdId),
                GoodsKinds.One(InventoryContainerRules.WaterJug), ExtraUnits: 1, ContentsKind: InventoryContainerRules.FreshWater))
            .Matches.Where(match => match.Lot.CarrierId is null &&
                // Keep one place for the water fetched after this real pickup.
                match.MoveUnits < FreeCarryCapacity(actor) && CanRemoveWorkstationStock(inventory, match.Lot, 1))
            .Select(match => match.Lot).FirstOrDefault(lot =>
                WaterJugStockLocation(lot, householdId) is { } source &&
                FindFreshWaterShore(actor, source.Position, ContainerFamilyQuantity(inventory, lot.Id),
                    HouseForHousehold(householdId)?.Position) is not null &&
                (person.Position == source.Position ||
                 FindUnoccupiedRoute(actor, person.Position, source.Position, 0).Count > 0));
    }

    private GridPoint? WaterJugRefillReturnDestination(string actor, InventoryLot jug)
    {
        if (HouseholdFor(actor) is not { } household || HouseForHousehold(household) is not { } house)
            return null;
        // An already carried jug can be filled for local use while its House
        // is full. A House return is available only if at least one more water
        // unit can accompany the intact family into its storage.
        return DestinationRoom(house.InstanceId) >
            ContainerFamilyQuantity(society.Checkpoint.Inventory, jug.Id) ? house.Position : null;
    }

    private void CollectWaterJug(string actor, AgentState person)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house)
            return;
        var inventory = society.Checkpoint.Inventory;
        var jug = StoredWaterJugForRefill(actor, person, householdId);
        if (jug is null || ContainerFamilyQuantity(inventory, jug.Id) >= FreeCarryCapacity(actor))
            return;
        var source = WaterJugStockLocation(jug, householdId)!;
        if (person.Position != source.Position)
        {
            MoveToward(actor, person, source.Position, "collect_water_jug", 0);
            return;
        }
        if (!CanRemoveWorkstationStock(inventory, jug, 1)) return;
        ApplyInventoryTransition(current => InventoryAuthority.Transfer(current,
            $"water-jug-pickup:{WorldTick}:{actor}", householdId, actor, jug.Id, 1,
            "water_jug_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent("water_jug_collected", $"{actor}:{jug.Id}");
    }

    private void FillWaterJug(string actor, AgentState person, string target)
    {
        if (!TryParsePoint(target, out var shore) || !FreshWaterShorePositions().Contains(shore) ||
            CarriedWaterJugForRefill(actor) is not { } jug)
            return;
        var inventory = society.Checkpoint.Inventory;
        if (!CanReachByFootOrSwimming(actor, person.Position, shore) ||
            FindUnoccupiedRoute(actor, person.Position, shore, 0).Count == 0)
            return;
        if (person.Position != shore)
        {
            MoveToward(actor, person, shore, "fill_water_jug", 0);
            return;
        }
        var remaining = Math.Min(
            InventoryContainerRules.WaterJugCapacity - ContainerContentsQuantity(inventory, jug.Id),
            FreeCarryCapacity(actor));
        if (WaterJugRefillReturnDestination(actor, jug) is { } destination)
            remaining = Math.Min(remaining, PickupCarryCapacity(actor, shore, destination));
        if (remaining <= 0 || HasActiveContainerReservation(inventory, jug.Id) ||
            inventory.Lots.Any(lot => lot.ContainerLotId == jug.Id && lot.ItemKind != InventoryContainerRules.FreshWater))
            return;
        ApplyInventoryTransition(current => InventoryAuthority.AddLot(current,
            $"{jug.Id}#water:{WorldTick.ToString(CultureInfo.InvariantCulture)}",
            InventoryContainerRules.FreshWater, actor, remaining, WorldTick, containerLotId: jug.Id));
        AppendEvent("water_jug_filled", $"{actor}:{jug.Id}:{remaining}:{shore.X},{shore.Y}");
    }

    private void ReturnWaterJug(string actor, AgentState person)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house ||
            CarriedWaterJugForReturn(actor, house.InstanceId) is not { } jug)
            return;
        if (person.Position != house.Position)
        {
            MoveToward(actor, person, house.Position, "return_water_jug", 0);
            return;
        }
        ApplyInventoryTransition(current => InventoryAuthority.Transfer(current,
            $"water-jug-return:{WorldTick}:{actor}", actor, householdId, jug.Id, 1,
            "water_jug_returned", destinationStorageBuildingId: house.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, house.InstanceId)));
        AppendEvent("water_jug_returned", $"{actor}:{jug.Id}:{house.InstanceId}");
    }

    private void StoreFoodInPot(string actor, AgentState person)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house ||
            FindFoodToStore(actor, householdId, house.InstanceId) is not { } choice)
            return;
        if (person.Position != house.Position)
        {
            MoveToward(actor, person, house.Position, "store_food_in_pot", 0);
            return;
        }

        var inventory = society.Checkpoint.Inventory;
        var contentRoom = InventoryContainerRules.StoragePotCapacity -
            ContainerContentsQuantity(inventory, choice.Pot.Id);
        var storageRoom = choice.Food.OwnerId == actor ? DestinationRoom(house.InstanceId) : int.MaxValue;
        var available = AvailableLotQuantity(choice.Food) - (choice.Food.OwnerId == actor ? 1 : 0);
        var quantity = Math.Min(available, Math.Min(contentRoom, storageRoom));
        if (quantity <= 0)
            return;

        var operationId = FoodPotTransferId(WorldTick, actor);
        ApplyInventoryTransition(current =>
        {
            var sourceId = choice.Food.Id;
            if (choice.Food.OwnerId == actor)
            {
                current = InventoryAuthority.Transfer(current, operationId, actor, householdId, choice.Food.Id,
                    quantity, "food_stored_in_pot", destinationStorageBuildingId: house.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, house.InstanceId));
                if (quantity != choice.Food.Quantity)
                    sourceId = DerivedId.Create($"{choice.Food.Id}#transfer:{operationId}");
            }
            return InventoryAuthority.PutIntoContainer(current, operationId, householdId, choice.Pot.Id, sourceId, quantity, holdAccess: InventoryHoldRules.BorrowedBy(actor));
        });
        AppendEvent("food_stored_in_pot", $"{actor}:{choice.Pot.Id}:{choice.Food.Id}:{quantity}");
    }

    private void TakeFoodFromPot(string actor, AgentState person)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house ||
            FindFoodInPot(householdId, house.InstanceId) is not { } choice)
            return;
        if (person.Position != house.Position)
        {
            MoveToward(actor, person, house.Position, "take_food_from_pot", 0);
            return;
        }
        var quantity = Math.Min(1, AvailableLotQuantity(choice.Food));
        if (FreeCarryCapacity(actor) < quantity)
            return;
        if (quantity <= 0)
            return;
        ApplyInventoryTransition(inventory => InventoryAuthority.TakeFromContainer(inventory,
            $"food-pot-take:{WorldTick}:{actor}", householdId, actor, choice.Pot.Id, choice.Food.Id, quantity, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent("food_taken_from_pot", $"{actor}:{choice.Pot.Id}:{choice.Food.Id}:{quantity}");
    }

    private PotFoodChoice? FindFoodInPot(string householdId, string houseId, string? requiredItemKind = null)
    {
        var inventory = society.Checkpoint.Inventory;
        var foods = FindGoods(new(GoodsUse.ConsumeAt, null, GoodsOwners.One(householdId),
            requiredItemKind is null ? EdibleFoodGoods : GoodsKinds.One(requiredItemKind), AtBuilding: houseId)).Matches;
        return FindGoods(new(GoodsUse.VesselFor, null, GoodsOwners.One(householdId),
                GoodsKinds.One(InventoryContainerRules.StoragePot), AtBuilding: houseId)).Matches
            .Select(pot => foods.Where(food => food.Root.Id == pot.Lot.Id && IsEdibleFood(food.Lot.ItemKind))
                .Select(food => new PotFoodChoice(pot.Lot, food.Lot)).FirstOrDefault())
            .FirstOrDefault(choice => choice is not null);
    }

    private static bool TryParsePoint(string value, out GridPoint point)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            point = new GridPoint(x, y);
            return true;
        }
        point = default;
        return false;
    }
}
