using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void MoveToward(
        string agentId,
        AgentState state,
        GridPoint destination,
        string reason,
        int interactionRange = 0,
        int horseMovementBudget = 100)
    {
        guardianPlacementActions.Add(agentId);
        if (IsWithinInteractionRange(state.Position, destination, interactionRange))
        {
            AppendEvent("destination_reached", $"{agentId}:{reason}");
            return;
        }

        if (state.TravelCooldownTicks > 0)
        {
            agents[agentId] = state with { TravelCooldownTicks = state.TravelCooldownTicks - 1 };
            KeepPlannedRoute(agentId, reason, destination);
            return;
        }

        if (state.Departures is { Count: > 0 } && MovingCareGroup(agentId).Any(id => id != agentId &&
                agents[id].GuardianPlacement is null &&
                !IsWithinInteractionRange(agents[id].Position, state.Position, 2)))
        {
            RecordMovementBlocked(agentId, state, "waiting_for_dependent");
            return;
        }
        if (AttachedHandcart(agentId) is { } heldCart && !CanPullHandcart(heldCart))
        {
            ParkHandcart(agentId, "equipment_unusable");
            RecordMovementBlocked(agentId, state, "cart_unusable");
            return;
        }
        var route = FindUnoccupiedRoute(agentId, state.Position, destination, interactionRange);
        if (route.Count < 2)
        {
            RecordMovementBlocked(agentId, state, "no_route");
            return;
        }

        var next = route[1];
        var travelCost = TravelStepCost(agentId, state.Position, next);
        var mounted = RidingAnimal(agentId) is not null;
        if (mounted)
        {
            travelCost = (travelCost + 1) / 2;
            if (horseMovementBudget < 100 && travelCost > horseMovementBudget) return;
        }
        MoveAttachedHandcart(agentId, state.Position, next);
        MoveAttachedAnimal(agentId, state.Position, next);
        agents[agentId] = state with
        {
            Position = next,
            MoveWaitTicks = 0,
            TravelCooldownTicks = (travelCost + 99) / 100 - 1 +
                IllnessRules.TravelDelayTicks(state.Survival?.IllnessBasisPoints ?? 0),
        };
        RecordPlannedRoute(agentId, reason, destination, route);
        RecordBridgeTraffic(agentId, state.Position, next);
        WearCarryAid(agentId);
        RecordNonLandConduct(agentId, "travel", next, null, null, 1,
            $"move:{WorldTick}:{agentId}:{nextEventId}");
        AppendEvent("agent_moved", $"{agentId}:{state.Position.X},{state.Position.Y}->{next.X},{next.Y}:{reason}");
        if (mounted && horseMovementBudget == 100 && travelCost <= 50 && agents[agentId].TravelCooldownTicks == 0)
            MoveToward(agentId, agents[agentId], destination, reason, interactionRange, 100 - travelCost);
    }

    private List<GridPoint> FindUnoccupiedRoute(
        string agentId,
        GridPoint origin,
        GridPoint destination,
        int interactionRange,
        bool allowSwimming = true,
        int additionalCarriedUnits = 0)
    {
        var occupied = agents.Values
            .Where(item => item.AgentId != agentId)
            .Select(item => item.Position)
            .ToHashSet();
        occupied.UnionWith(animalWorld.Animals.Where(animal => animal.DiedTick is null && animal.RiderId != agentId &&
            animal.LeaderId != agentId && animal.Position != destination).Select(animal => animal.Position));
        if (interactionRange == 0 && animalWorld.Animals.Any(animal => (animal.RiderId == agentId || animal.LeaderId == agentId)) &&
            animalWorld.Animals.Any(animal => animal.DiedTick is null && animal.RiderId != agentId && animal.LeaderId != agentId && animal.Position == destination))
            return [];
        if (interactionRange == 0 && worldSimulation.Buildings.Any(building =>
                building.Position == destination &&
                building.HouseholdId is not null &&
                (building.HouseholdId == society.Checkpoint.GetAgent(agentId).HouseholdId ||
                 WeatherAt(building.Position) == WeatherKind.Storm && HasHouseGuestInvitation(agentId, building.InstanceId) ||
                 CanEnterGuardianPlacementHouse(agentId, building))))
            occupied.Remove(destination);
        if (interactionRange == 0 && occupied.Contains(destination) &&
            CanShareTownDestination(agentId, destination))
            occupied.Remove(destination);
        // Other occupied exact destinations cannot be reached. Avoid searching
        // an entire map when neither household nor public Town sharing applies.
        if (interactionRange == 0 && origin != destination && occupied.Contains(destination))
            return [];
        if (AttachedHandcart(agentId) is null)
            return SharedUnoccupiedRoute(origin, occupied, destination, interactionRange,
                allowSwimming && CanSwim(agentId, origin, additionalCarriedUnits));

        var open = new PriorityQueue<GridPoint, (int Cost, int Y, int X, int Order)>();
        var best = new Dictionary<GridPoint, int> { [origin] = 0 };
        var predecessor = new Dictionary<GridPoint, GridPoint>();
        var order = 0;
        open.Enqueue(origin, (0, origin.Y, origin.X, order++));

        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != best[current])
                continue;
            if (IsWithinInteractionRange(current, destination, interactionRange))
            {
                var route = new List<GridPoint> { current };
                while (current != origin)
                {
                    current = predecessor[current];
                    route.Add(current);
                }

                route.Reverse();
                return route;
            }

            foreach (var next in map.FootNeighbors(current))
            {
                if (handcartHitches.Any(hitch => hitch.PullerId == agentId) && !LegalHandcartStep(current, next) ||
                    occupied.Contains(next) ||
                    map.IsDiagonalFootStep(current, next) &&
                    (occupied.Contains(new GridPoint(next.X, current.Y)) ||
                     occupied.Contains(new GridPoint(current.X, next.Y))))
                {
                    continue;
                }

                var cost = checked(priority.Cost + TravelStepCost(agentId, current, next));
                if (best.TryGetValue(next, out var previous) && previous <= cost)
                    continue;
                best[next] = cost;
                predecessor[next] = current;
                open.Enqueue(next, (cost, next.Y, next.X, order++));
            }
        }

        return [];
    }

    private bool CanShareTownDestination(string actor, GridPoint destination) =>
        WarehousesAccessibleTo(actor).Any(warehouse => warehouse.Position == destination) ||
        TownForResident(actor) is { } townId && towns.Any(town => town.Id == townId &&
            town.Projects.Any(project => project.Stage is "supplying" or "working" &&
                TownProjectRules.WorkSite(project.Plan) == destination));

    private void RecordMovementBlocked(
        string agentId,
        AgentState state,
        string reason)
    {
        var waitTicks = checked(state.MoveWaitTicks + 1);
        agents[agentId] = state with { MoveWaitTicks = waitTicks };
        if (waitTicks == 1 || waitTicks % 30 == 0)
        {
            if (AttachedHandcart(agentId) is not null || reason == "cart_unusable")
                AppendEvent("handcart_blocked", $"{agentId}:{reason}");
            AppendEvent("movement_blocked", $"{agentId}:{reason}:wait={waitTicks}");
        }
    }

    private bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) =>
        AgentQueries.IsWithinInteractionRange(agentWorld, origin, destination, interactionRange);

    private IEnumerable<MapResource> EligibleFoodSources(string actor, GridPoint position) => map.Resources
        .Where(resource => resource.Kind is "food" or "fruit" &&
            resources.GetValueOrDefault(resource.Id) == ResourceState.Available &&
            CanReachByFootOrSwimming(actor, position, resource.Position))
        .OrderBy(resource => map.FootDistance(resource.Position, position))
        .ThenBy(resource => resource.Id, StringComparer.Ordinal)
        .Where(resource => IsWithinInteractionRange(position, resource.Position, ResourceInteractionRange) ||
            ReachLeg(actor, position, resource.Position, ReachPurpose.FoodSource, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0);

    private MapResource? AvailableFoodSource(string actor, GridPoint position)
    {
        var room = FreeCarryCapacity(actor);
        return EligibleFoodSources(actor, position).FirstOrDefault(source => FoodHarvestCarryUnits(source) <= room);
    }

    private int MinimumFoodPickupCarryUnits(string actor, GridPoint position, bool forDependent)
    {
        var serving = forDependent ? AvailableHouseholdServing(actor, position) : AvailableSharedFood(actor);
        if (serving is not null)
            return 1;
        return EligibleFoodSources(actor, position).Select(FoodHarvestCarryUnits).DefaultIfEmpty(0).Min();
    }

    private static int FoodHarvestQuantity(MapResource source) => source.TreeKind == TreeGrowthRules.Orchard
        ? TreeGrowthRules.OrchardFruitPerPick : HarvestFoodYield;

    private static int FoodHarvestCarryUnits(MapResource source) => FoodHarvestQuantity(source) +
        (source.TreeKind == TreeGrowthRules.Orchard ? TreeGrowthRules.OrchardSeedsPerPick : 0);

    private FoodHarvestEffect? HarvestFood(
        string agentId,
        AgentState state,
        MapResource? requestedSource = null)
    {
        var source = requestedSource ?? AvailableFoodSource(agentId, state.Position);
        if (source is null || !IsWithinInteractionRange(state.Position, source.Position, ResourceInteractionRange))
        {
            AppendEvent("harvest_failed", $"{agentId}:not_at_available_food");
            return null;
        }

        var harvestYield = FoodHarvestQuantity(source);
        if (FreeCarryCapacity(agentId) < FoodHarvestCarryUnits(source))
        {
            AppendEvent("carrying_full", agentId);
            return null;
        }
        return CompleteFoodHarvest(agentId, source, FoodHarvestKind(source), harvestYield,
            source.TreeKind == TreeGrowthRules.Orchard ? TreeGrowthRules.OrchardSeedsPerPick : 0);
    }

    private FoodHarvestEffect? CompleteFoodHarvest(string agentId, MapResource source, string itemKind,
        int harvestYield, int seedQuantity)
    {
        var ecologyResource = worldSystems.Ecology.GetResource(source.Id);
        var harvest = EcologyRules.Harvest(ecologyResource, 1);
        if (!harvest.IsValid || harvest.Resource is null)
        {
            SyncEcologyResourceStates();
            AppendEvent("harvest_failed", $"{agentId}:{harvest.Failure ?? "food_depleted"}");
            return null;
        }

        var harvested = source.TreeKind == "orchard" && harvest.Resource.Quantity == 0
            ? harvest.Resource with
            {
                NextRegenerationDay = WorldCalendarRules.FromTick(WorldTick, worldSystems.Config).DayIndex +
                    harvest.Resource.RegenerationIntervalDays,
            }
            : harvest.Resource;
        environmentSystem.ReplaceEcologyResource(source.Id, harvested);
        SyncEcologyResourceStates();
        var harvestedLotId = FoodHarvestLotId(WorldTick, agentId);
        ApplyInventoryTransition(inventory => InventoryAuthority.AddLot(
            inventory, harvestedLotId,
            itemKind,
            agentId,
            harvestYield,
            WorldTick));

        RecordNonLandConduct(agentId, "gather_food", source.Position, source.Id,
            itemKind,
            harvestYield, $"gather-food:{WorldTick}:{agentId}:{nextEventId}");
        AppendEvent("food_harvested", $"{agentId}:{harvestYield}");
        if (seedQuantity > 0)
        {
            var lotId = OrchardSeedLotId(WorldTick, agentId);
            ApplyInventoryTransition(inventory => InventoryAuthority.Reserve(
                InventoryAuthority.AddLot(inventory, lotId, TreeGrowthRules.OrchardSeedItem, agentId,
                    seedQuantity, WorldTick), OrchardReplantingReservationId(lotId),
                agentId, lotId, 1, "orchard_replanting", long.MaxValue, holdAccess: InventoryHoldRules.BorrowedBy(agentId)));
            AppendEvent("fruit_harvested", $"{agentId}:{source.Id}:{harvestYield}:picked");
        }
        return new FoodHarvestEffect(harvestedLotId,
            itemKind,
            harvestYield, source.Id);
    }

    private string HouseholdFor(string actor) => AgentQueries.HouseholdFor(agentWorld, actor);

    private string ProductionOwnerFor(PlacedBuilding? building, string workerId) =>
        building?.HouseholdId ?? society.Checkpoint.GetAgent(workerId).HouseholdId ?? workerId;

    private static bool IsHouseholdBuildingTag(string tag) => HouseholdBuildingKinds.IsKindTag(tag);

    private static string? HouseholdBuildingKind(BuildingDefinition definition) => HouseholdBuildingKinds.KindOf(definition);

    private PlacedBuilding? HouseForHousehold(string householdId) => HouseholdBuildingWithTag(householdId, "house");

    /// <summary>The household's own building of a kind, such as its Farmhouse; first by instance ID.</summary>
    private PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => worldSimulation.Buildings
        .Where(building => building.HouseholdId == householdId &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                definition.Tags.Contains(tag, StringComparer.Ordinal)))
        .OrderBy(building => building.InstanceId, StringComparer.Ordinal).FirstOrDefault();

    private GridPoint HouseholdStockPosition(InventoryLot lot) => AgentQueries.HouseholdStockPosition(agentWorld, lot);

    private static int HouseholdStockInteractionRange(InventoryLot lot) =>
        lot.StorageBuildingId is null && lot.GroundPosition is null ? ResourceInteractionRange : 0;

    private InventoryLot? AvailableHouseholdServing(string actor, GridPoint position, string? requiredItemKind = null,
        GridPoint? returnTo = null, int returnRange = 0)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId)
            return null;
        var loose = PreferredFood(householdId, actor).FirstOrDefault(lot =>
            !OnBorrowedMarketStall(lot) && (requiredItemKind is null || lot.ItemKind == requiredItemKind) &&
            lot.CarrierId is null && (returnTo is null || PickupCarryCapacity(actor, lot, returnTo.Value, returnRange) >= 1) && (lot.StorageBuildingId is null ||
             householdId == lot.OwnerId) &&
            (IsWithinInteractionRange(position, HouseholdStockPosition(lot), HouseholdStockInteractionRange(lot)) ||
             ReachLeg(actor, position, HouseholdStockPosition(lot), ReachPurpose.HouseholdFood, HouseholdStockInteractionRange(lot), 0, SwimmingPolicy.WhenCapable).Count > 0));
        if (loose is not null)
            return loose;
        return HouseForHousehold(householdId) is { } house &&
            FindFoodInPot(householdId, house.InstanceId, requiredItemKind) is { } potFood &&
            (returnTo is null || PickupCarryCapacity(actor, potFood.Food, returnTo.Value, returnRange) >= 1) &&
            (position == house.Position || ReachLeg(actor, position, house.Position, ReachPurpose.HouseholdFood, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)
                ? potFood.Food : null;
    }

    private InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => MayCollectSharedFood(actor)
        ? AvailableHouseholdServing(actor, agents[actor].Position, requiredItemKind) : null;

    private bool TryCollectHouseholdServing(string actor, AgentState state, InventoryLot food,
        string operationId, string purpose, string movementReason, GridPoint? returnTo = null, int returnRange = 0)
    {
        operationId = DerivedId.Create(operationId);
        if (FreeCarryCapacity(actor) < 1 || AvailableLotQuantity(food) < 1 || food.CarrierId is not null ||
            society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId || food.OwnerId != householdId ||
            returnTo is { } destination && PickupCarryCapacity(actor, food, destination, returnRange) < 1)
            return false;
        var supplyPoint = HouseholdStockPosition(food);
        var interactionRange = HouseholdStockInteractionRange(food);
        if (!IsWithinInteractionRange(state.Position, supplyPoint, interactionRange))
        {
            MoveToward(actor, state, supplyPoint, movementReason, interactionRange);
            return false;
        }
        if (food.ContainerLotId is { } potId)
        {
            if (HouseForHousehold(householdId) is not { } house ||
                society.Checkpoint.Inventory.GetLot(potId) is not { ItemKind: InventoryContainerRules.StoragePot } pot ||
                pot.OwnerId != householdId || pot.StorageBuildingId != house.InstanceId || pot.ConditionBasisPoints <= 0 ||
                HasActiveContainerReservation(society.Checkpoint.Inventory, potId))
                return false;
            ApplyInventoryTransition(inventory => InventoryAuthority.TakeFromContainer(inventory,
                operationId, householdId, actor, potId, food.Id, 1, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
            AppendEvent("food_taken_from_pot", $"{actor}:{potId}:{food.Id}:1");
        }
        else
            ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                operationId, householdId, actor, food.Id, 1, purpose, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        return true;
    }

    private void CollectSharedFood(
        string agentId,
        AgentState state,
        string? requiredItemKind = null)
    {
        if (FreeCarryCapacity(agentId) == 0) return;
        if (AvailableSharedFood(agentId, requiredItemKind) is not { } lot)
            return;
        CompleteSharedFoodCollection(agentId, state, lot);
    }

    private void CompleteSharedFoodCollection(string agentId, AgentState state, InventoryLot lot)
    {
        if (TryCollectHouseholdServing(agentId, state, lot, $"household-food:{WorldTick}:{agentId}",
                "household_food_share", "household_food"))
            AppendEvent("household_food_collected", $"{agentId}:{lot.Id}:1");
    }

    private string? ConsumeFood(string agentId, AgentState state, string? requiredItemKind = null)
    {
        var lot = PreferredFood(agentId, agentId)
            .FirstOrDefault(item => requiredItemKind is null || item.ItemKind == requiredItemKind);
        if (lot is null)
        {
            AppendEvent("consumption_failed", $"{agentId}:no_food");
            return null;
        }

        return CompleteFoodConsumption(agentId, state, lot);
    }

    private string CompleteFoodConsumption(string agentId, AgentState state, InventoryLot lot)
    {
        society.Apply(checkpoint => SocietyRules.ConsumeInventory(checkpoint, agentId, lot.Id, 1));
        agents[agentId] = state with
        {
            FullnessBasisPoints = Math.Min(10_000, state.FullnessBasisPoints + FoodNourishment(lot.ItemKind)),
            Survival = AfterMeal(state, lot)
        };
        AppendEvent("food_consumed", agentId);
        return lot.Id;
    }

}
