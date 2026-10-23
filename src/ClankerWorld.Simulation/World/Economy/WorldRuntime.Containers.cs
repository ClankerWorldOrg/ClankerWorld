using System.Globalization;
using System.Runtime.CompilerServices;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>Physical storage and fresh-water actions for reusable household vessels.</summary>
public sealed partial class WorldRuntime
{
    private const string FillWaterJugPrefix = VesselChoice.FillPrefix;
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
                AddVesselCandidate(candidates, PlanVesselAction(actor, new(VesselActionKind.FillJug, fillAt), refillJug),
                    "Fill the water jug at a reachable riverbank or lakeshore.", 18, $"shore:{pointText}");
            }
            else
                AddVesselCandidate(candidates, PlanVesselAction(actor, new(VesselActionKind.ReturnJug)),
                    "Carry the water jug back to its household House.", 22, house.InstanceId);
        }
        else
            AddVesselCandidate(candidates, PlanVesselAction(actor, new(VesselActionKind.CollectJug)),
                "Collect the household's reusable jug before fetching fresh water.", 26,
                useVesselId: true);

        AddEmptyVesselReturnCandidate(candidates, actor, person);
        AddFoodPotCandidates(candidates, actor, person, householdId, house);
    }

    private void AddFoodPotCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState person, string householdId, PlacedBuilding house)
    {
        var foodToStore = FindFoodToStore(actor, householdId, house.InstanceId);
        if (foodToStore is not null)
            AddVesselCandidate(candidates, PlanVesselAction(actor, new(VesselActionKind.StoreFood), selectedFood: foodToStore),
                "Put available household food into its storage pot.", 24, foodToStore.Pot.Id);

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
        if (WantsFoodFromPot(actor) && FindFoodInPot(householdId, house.InstanceId) is { } storedFood)
            AddVesselCandidate(candidates, PlanVesselAction(actor, new(VesselActionKind.TakeFood), selectedFood: storedFood),
                "Take one serving from the household's storage pot.", NeedsUrgentFood(person) ? 1 : 4, storedFood.Food.Id);
    }

    private bool WantsFoodFromPot(string actor) => agents[actor].FullnessBasisPoints < NeedThresholds.FoodPickupFullness &&
        this.townSystem.MayCollectSharedFood(this.civicWorld, actor) && FreeCarryCapacity(actor) > 0 &&
        !FindGoods(new(GoodsUse.Eat, actor, GoodsOwners.One(actor), EdibleFoodGoods)).Matches.Any(match => match.Lot.ContainerLotId is null);

    private void AddVesselCandidate(List<CognitionCandidate> candidates, VesselActionPlan? plan,
        string description, int priority, string? destination = null, bool useVesselId = false)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, priority, useVesselId ? plan.VesselId : destination) { ActionPlan = plan });
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
                     .OrderBy(point => map.FootDistance(origin, point)))
        {
            if (ReachLeg(actor, origin, point, ReachPurpose.HouseholdVessel, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0 &&
                (point == origin || ReachLeg(actor, origin, point, ReachPurpose.HouseholdVessel, 0, additionalCarriedUnits, SwimmingPolicy.WhenCapable).Count > 0) &&
                (returnTo is null || PickupCarryCapacity(actor, point, returnTo.Value) > additionalCarriedUnits))
                return point;
        }
        return null;
    }

    private PlacedBuilding? WaterJugStockLocation(InventoryLot lot, string householdId) =>
        worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == lot.StorageBuildingId &&
            building.HouseholdId == householdId && worldContent.Buildings.Any(definition =>
                definition.CanonicalId == building.DefinitionId && definition.Tags.Any(tag => tag is "house" or "restaurant")));

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
}
