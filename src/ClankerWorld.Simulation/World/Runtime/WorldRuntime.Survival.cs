using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record SurvivalCondition(int WarmthBasisPoints = 10_000, int IllnessBasisPoints = 0,
    int NutritionBasisPoints = 5_000, string? LastMealKind = null);
public sealed record BuildingFireState(string BuildingId, long FuelUntilTick);
public sealed record SurvivalState(long ActivatedTick, IReadOnlyList<BuildingFireState> Fires);

public static class IllnessRules
{
    public static int WorkRatePercent(int illnessBasisPoints) => Math.Clamp(illnessBasisPoints, 0, 10_000) switch
    {
        < 2_500 => 100,
        < 5_000 => 75,
        < 7_500 => 50,
        _ => 25,
    };

    public static int TravelDelayTicks(int illnessBasisPoints) => Math.Clamp(illnessBasisPoints, 0, 10_000) switch
    {
        < 2_500 => 0,
        < 7_500 => 1,
        _ => 2,
    };

    public static bool AllowsWork(string agentId, long worldTick, int illnessBasisPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        if (WorkRatePercent(illnessBasisPoints) == 100) return true;
        var phase = StablePhase(agentId);
        return (worldTick % 4 + phase) % 4 < WorkRatePercent(illnessBasisPoints) / 25;
    }

    private static int StablePhase(string agentId)
    {
        uint hash = 2_166_136_261;
        foreach (var character in agentId)
        {
            hash = unchecked((hash ^ character) * 16_777_619);
        }
        return (int)(hash % 4);
    }
}

public sealed partial class WorldRuntime
{
    private SurvivalState? survivalState;
    private static readonly HashSet<string> PerishableKinds = new(StringComparer.Ordinal)
        { "food", "fruit", "berries", "wild_greens", "cultivated_greens",
            "simple_meal", "bread", "porridge", "berry_porridge", "fruit_porridge", "stew", "restaurant_meal", "eggs", "milk", "cooked_eggs", "milk_porridge", "rich_meal" };
    private static readonly IReadOnlyDictionary<string, int> FoodSpoilageRates =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["bread"] = 2, ["milk"] = 28 };
    private const int IllnessRecoveryPerTick = 12;
    private const int ShelteredIllnessRecoveryBonusPerTick = 12;
    private const int IllnessCareReliefBasisPoints = 250;
    private const int ExposureIllnessIncreasePerTick = 8;
    private const int SpoiledMealIllnessIncreaseBasisPoints = 300;
    private const int FreshMealIllnessReliefBasisPoints = 100;

    private const int ComfortableFullness = NeedThresholds.ComfortableFullness;
    private const int ComfortableWarmth = NeedThresholds.ComfortableWarmth;
    private const int UrgentFullness = NeedThresholds.UrgentFullness;
    private const int UrgentWarmth = ClankerWorld.Contracts.NeedStates.ColdThreshold;
    // Provisional routine and outing reserves, not owner-approved balance.
    private const int RoutineFoodSeekFullness = NeedThresholds.RoutineFoodSeekFullness;
    private const int OutingFullnessReserve = 3_000;
    // Provisional balance (#641, #673): how much colder full night is than
    // the same weather by day, in the per-tick exposure units weather uses.
    // Dusk and dawn fade it in and out. Shelter, clothing and fire offset it
    // exactly as they offset weather.
    private const int NightChillAtFullDarkness = 15;
    // Provisional balance: trees give about half a building's storm protection.
    private const int NaturalStormProtection = 22;

    private static bool NeedsUrgentFood(AgentState person) => AgentQueries.NeedsUrgentFood(person, UrgentFullness);

    private bool NeedsUrgentWarmth(AgentState person) =>
        AgentQueries.NeedsUrgentWarmth(agentWorld, person, UrgentWarmth);

    private bool ReadyForBriefInteraction(string actor) => agents.TryGetValue(actor, out var person) &&
        !NeedsUrgentWarmth(person);

    private int WarmthChange(AgentState person)
    {
        var naturalCover = WeatherAt(person.Position) == WeatherKind.Storm && NaturalStormCover(person.Position);
        var protection = ClothingProtection(person.AgentId, person.Position) +
            (NearShelter(person.AgentId, person.Position) ? 45 : naturalCover ? NaturalStormProtection : 0);
        var heat = AccessibleHeatingBuildings(person.AgentId).Any(building => IsFireLit(building) &&
            IsWithinInteractionRange(person.Position, building.Position,
                building.HouseholdId is null ? 2 : 0)) ? 90 : 0;
        var loss = Math.Max(0, OutdoorExposure(person.Position) - protection);
        return IsSwimming(person.AgentId, person.Position)
            ? -Math.Max(SwimmingRules.WarmthLossPerTick, loss - heat)
            : -loss + heat + (loss == 0 ? 20 : 0);
    }

    private bool IsProtectiveProject(AgentProject? project) =>
        project is not null &&
        TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) &&
        (selection.IsBuilding
            ? worldContent.Buildings.Any(building => selection.DefinitionId == building.CanonicalId &&
                building.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking"))
            : worldContent.Recipes.Any(recipe => selection.DefinitionId == recipe.CanonicalId &&
                recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId))));

    private WeatherKind WeatherAt(GridPoint position) => environmentSystem.WeatherAt(map, position);

    /// <summary>The cold of being outdoors here now: weather, climate and season, plus night.</summary>
    private int OutdoorExposure(GridPoint position) => environmentSystem.OutdoorExposure(map, position, NightChillAtFullDarkness);

    private int WeatherExposure(GridPoint position) => environmentSystem.WeatherExposure(map, position);

    private bool HasCarriedItem(string actor, string kind) => society.Checkpoint.Inventory.Lots.Any(lot =>
        PersonalEquipmentRules.IsCarried(lot, actor) && lot.DeliveryBuildingId is null &&
        lot.ContainerLotId is null && lot.ItemKind == kind && AvailableLotQuantity(lot) > 0);

    /// <summary>Carried goods the agent owns, so may spend; borrowed household goods are excluded.</summary>
    private bool HasCarriedOwnItem(string actor, string kind) => society.Checkpoint.Inventory.Lots.Any(lot =>
        lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) && lot.DeliveryBuildingId is null &&
        lot.ContainerLotId is null && lot.ItemKind == kind && AvailableLotQuantity(lot) > 0);

    private static GoodsRequest SharedCollectionRequest(string actor, string owner, string kind) =>
        new(GoodsUse.Collect, actor, GoodsOwners.One(owner), GoodsKinds.One(kind));

    private InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) =>
        SharedStockItem(kind, actor, GoodsUse.Collect, returnTo, returnRange);

    // Preparation may free cargo space before pickup; it must still find reachable usable stock.
    private InventoryLot? SharedPreparationItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) =>
        SharedStockItem(kind, actor, GoodsUse.ReachableHoldings, returnTo, returnRange);

    private InventoryLot? SharedStockItem(string kind, string actor, GoodsUse use, GridPoint? returnTo, int returnRange)
        => SharedStockItems(kind, actor, use).FirstOrDefault(lot =>
            returnTo is null || CanPrepareSharedPickup(actor, lot, returnTo.Value, returnRange));

    private IEnumerable<InventoryLot> SharedStockItems(string kind, string actor, GoodsUse use)
    {
        if (HouseholdFor(actor) is { } household)
        {
            var request = new GoodsRequest(use, actor, GoodsOwners.One(household), GoodsKinds.One(kind));
            foreach (var match in FindGoods(request).Matches)
                if (match.Lot.ContainerLotId is null && match.Lot.CarrierId is null) yield return match.Lot;
        }
        if (kind == "food") yield break;
        foreach (var lot in AvailableWarehouseStock(actor, kind))
            if (RecheckGoods(new(use, actor, GoodsOwners.One(lot.OwnerId), GoodsKinds.One(kind)), lot.Id) is { } match)
                yield return match.Lot;
    }

    private IEnumerable<PlacedBuilding> BuildingsWithTag(string tag) => worldSimulation.Buildings.Where(building =>
        worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(tag, StringComparer.Ordinal)));

    private bool IsTownHall(PlacedBuilding building) => worldContent.Buildings.Any(definition =>
        definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(TownHallContent.HallTag, StringComparer.Ordinal));

    private bool IsTownHallStormRefuge(string actor, PlacedBuilding building) =>
        IsTownHall(building) && building.TownId is { } townId && TownForResident(actor) == townId &&
        !HasHome(actor) && WeatherAt(building.Position) == WeatherKind.Storm;

    private IEnumerable<PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge = true) => BuildingsWithTag("shelter")
        .Where(building => building.HouseholdId is null ||
            building.HouseholdId == society.Checkpoint.GetAgent(actor).HouseholdId
            || WeatherAt(building.Position) == WeatherKind.Storm && HasHouseGuestInvitation(actor, building.InstanceId))
        .Concat(includeStormRefuge ? BuildingsWithTag(TownHallContent.HallTag).Where(building => IsTownHallStormRefuge(actor, building)) : [])
        .DistinctBy(building => building.InstanceId);

    private bool NearShelter(string actor, GridPoint point) => AccessibleShelters(actor).Any(building =>
        ShelterBuildingCovers(actor, building, point));

    private int WarmthDestinationRange(string actor, GridPoint origin, PlacedBuilding building) =>
        !IsSwimming(actor, origin) && building.HouseholdId is null && !IsTownHall(building) ? ResourceInteractionRange : 0;

    private GridPoint? ReachableHallShelterPoint(string actor, GridPoint origin, PlacedBuilding hall) =>
        WorldContentSimulationRules.Footprint(worldContent.Buildings.Single(definition => definition.CanonicalId == hall.DefinitionId), hall)
            .Where(point => map.IsPassable(point) && CanReachLeg(actor, origin, point, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable))
            .OrderBy(point => map.FootDistance(origin, point)).ThenBy(point => point.Y).ThenBy(point => point.X)
            .Select(point => (GridPoint?)point).FirstOrDefault();

    private GridPoint? WarmthDestinationPoint(string actor, AgentState person, PlacedBuilding building) =>
        IsTownHall(building) ? ReachableHallShelterPoint(actor, person.Position, building) : building.Position;

    private bool NaturalStormCover(GridPoint point) =>
        map.VegetationAt(point) == VegetationCover.Forest ||
        map.Resources.Any(site => site.Position == point &&
            (site.TreeKind == "orchard" ||
             (site.TreeKind is "broadleaf" or "conifer") &&
             worldSystems.Ecology.GetResource(site.Id).Quantity > 0));

    private GridPoint? NearbyNaturalStormCover(string actor, GridPoint origin)
    {
        const int searchRadius = 8;
        var occupied = agents.Values.Where(person => person.AgentId != actor)
            .Select(person => person.Position).ToHashSet();
        GridPoint? best = null;
        var bestDistance = int.MaxValue;
        for (var dy = -searchRadius; dy <= searchRadius; dy++)
            for (var dx = -searchRadius; dx <= searchRadius; dx++)
            {
                var x = origin.X + dx;
                if (map.WrapsEastWest) x = (x % map.Width + map.Width) % map.Width;
                var candidate = new GridPoint(x, origin.Y + dy);
                if (!map.IsPassable(candidate) || occupied.Contains(candidate) || !NaturalStormCover(candidate))
                    continue;
                var distance = map.FootDistance(origin, candidate);
                if (distance >= bestDistance || ReachLeg(actor, origin, candidate, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
                best = candidate;
                bestDistance = distance;
            }
        return best;
    }

    private bool IsFireLit(PlacedBuilding building) => survivalState?.Fires.Any(fire =>
        fire.BuildingId == building.InstanceId && fire.FuelUntilTick > WorldTick) == true;

    private IEnumerable<PlacedBuilding> HeatingBuildings() => BuildingsWithTag("cooking").Concat(BuildingsWithTag("warmth"))
        .DistinctBy(building => building.InstanceId);

    private IEnumerable<PlacedBuilding> AccessibleHeatingBuildings(string actor) => HeatingBuildings()
        .Where(building => building.HouseholdId is null ||
            building.HouseholdId == society.Checkpoint.GetAgent(actor).HouseholdId);

    private PlacedBuilding? ReachableUnlitHearth(string actor, AgentState person) =>
        AccessibleHeatingBuildings(actor).FirstOrDefault(building =>
        {
            if (IsFireLit(building)) return false;
            var range = building.HouseholdId is null ? ResourceInteractionRange : 0;
            return IsWithinInteractionRange(person.Position, building.Position, range) ||
                Reach(actor, building.Position, ReachPurpose.FireFuel, range, 0, SwimmingPolicy.WhenCapable).Count > 0;
        });

    private void AdvanceSurvival()
    {
        if (survivalState is null && !contentRegistry.ExportState().Packages.Any(package => package.Manifest.PackageId == HearthContent.PackageId &&
            package.Lifecycle == ContentPackageLifecycle.Active))
        {
            return;
        }
        if (survivalState is null)
        {
            survivalState = new SurvivalState(WorldTick, []);
            // Do not retroactively rot years of legacy inventory on migration.
            ApplyInventoryTransition(inventory => InventoryAuthority.ProcessSpoilage(inventory, WorldTick, 0));
            AppendEvent(new SurvivalActivatedEventPayload(null));
        }
        var existingIds = worldSimulation.Buildings.Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
        foreach (var fire in survivalState.Fires.Where(fire => fire.FuelUntilTick <= WorldTick || !existingIds.Contains(fire.BuildingId)))
        {
            AppendEvent(new FireExtinguishedEventPayload(null, fire.BuildingId));
        }
        survivalState = survivalState with
        {
            Fires = survivalState.Fires.Where(fire =>
            fire.FuelUntilTick > WorldTick && existingIds.Contains(fire.BuildingId)).ToArray()
        };
        var sharedStorage = BuildingsWithTag("storage").Any(building => building.HouseholdId is null);
        var shelteredOwners = sharedStorage
            ? society.Checkpoint.Households.Select(household => household.Id).ToHashSet(StringComparer.Ordinal)
            : BuildingsWithTag("storage").Where(building => building.HouseholdId is not null)
                .Select(building => building.HouseholdId!).ToHashSet(StringComparer.Ordinal);
        ApplyInventoryTransition(inventory => InventoryAuthority.ProcessSpoilage(inventory, WorldTick, 4,
            PerishableKinds, shelteredOwners, FoodSpoilageRates));
        foreach (var person in agents.Values.ToArray())
        {
            var old = person.Survival ?? new SurvivalCondition();
            var warmth = Math.Clamp(old.WarmthBasisPoints + WarmthChange(person), 0, 10_000);
            var illnessChange = warmth < NeedThresholds.IllnessExposureWarmth || person.FullnessBasisPoints < NeedThresholds.IllnessStarvationFullness
                ? ExposureIllnessIncreasePerTick
                : warmth > NeedThresholds.ComfortableWarmth && person.FullnessBasisPoints > NeedThresholds.IllnessRecoveryFullness
                    ? -(IllnessRecoveryPerTick + (NearShelter(person.AgentId, person.Position) ? ShelteredIllnessRecoveryBonusPerTick : 0))
                    : 0;
            var illness = Math.Clamp(old.IllnessBasisPoints + illnessChange, 0, 10_000);
            agents[person.AgentId] = person with { Survival = old with { WarmthBasisPoints = warmth, IllnessBasisPoints = illness } };
            if (old.WarmthBasisPoints / 2_500 != warmth / 2_500 || old.IllnessBasisPoints / 2_500 != illness / 2_500)
            {
                AppendEvent(new SurvivalConditionChangedEventPayload(person.AgentId, warmth, illness));
            }
        }
    }

    private void AddSurvivalCandidates(List<CognitionCandidate> candidates, string actor, AgentState person)
    {
        if (person.Survival is not { } condition)
        {
            return;
        }
        AddEquipmentCandidates(candidates, actor, person);
        AddOrnamentCandidates(candidates, actor);
        var losingWarmth = WarmthChange(person) < 0;
        if (losingWarmth && condition.WarmthBasisPoints < ComfortableWarmth &&
            FireDefinition.Offer(this, actor, new(WarmthChoiceKind.Fire)) is { } fire)
            candidates.Add(new CognitionCandidate(FireDefinition.Encode(new(WarmthChoiceKind.Fire)), "Carry wood to an unlit hearth and keep it burning for warmth.", NeedsUrgentWarmth(person) ? 1 : 2)
            { ActionPlan = fire });
        var recoveringInCover = NearShelter(actor, person.Position) ||
            WeatherAt(person.Position) == WeatherKind.Storm && NaturalStormCover(person.Position) ||
            WarmthChange(person) > 20;
        if (condition.WarmthBasisPoints < ComfortableWarmth && (recoveringInCover || losingWarmth) &&
            WarmthDefinition.Offer(this, actor, new(WarmthChoiceKind.Protection)) is { } warmth)
            candidates.Add(new CognitionCandidate(WarmthDefinition.Encode(new(WarmthChoiceKind.Protection)), "Seek protection or remain in cover while recovering warmth.", NeedsUrgentWarmth(person) ? 2 : 3)
            { ActionPlan = warmth });
    }

    private void CollectEquipment(string actor, AgentState person, string kind,
        GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null)
    {
        if (FreeCarryCapacity(actor) <= 0 || HasCarriedEquipmentAtLeast(actor, kind) ||
            SharedItem(kind, actor, returnTo, returnRange) is not { ContainerLotId: null } item ||
            plannedLotId is not null && item.Id != plannedLotId)
        {
            return;
        }
        var storage = HouseholdStockPosition(item);
        var interactionRange = HouseholdStockInteractionRange(item);
        if (!IsWithinInteractionRange(person.Position, storage, interactionRange))
        {
            MoveToward(actor, person, storage, "equipment", interactionRange);
            return;
        }
        if (RecheckGoods(SharedCollectionRequest(actor, item.OwnerId, kind), item.Id) is null) return;
        ApplyPlannedEquipmentPickup(actor, new(item.Id, item.OwnerId, kind, storage, interactionRange));
    }

    private void ApplyPlannedEquipmentPickup(string actor, PlannedEquipmentPickup pickup)
    {
        var state = agents[actor];
        if (!IsWithinInteractionRange(state.Position, pickup.Position, pickup.InteractionRange))
        {
            MoveToward(actor, state, pickup.Position, "equipment", pickup.InteractionRange);
            return;
        }
        // Personal equipment changes custody; household work tools are borrowed.
        var borrowedTool = pickup.ItemKind == "tool" || ToolProgressionRules.Find(pickup.ItemKind) is not null;
        var keepOwner = pickup.OwnerId == actor ||
            borrowedTool && pickup.OwnerId == society.Checkpoint.GetAgent(actor).HouseholdId;
        var operationId = DerivedId.Create($"equipment:{WorldTick}:{actor}:{pickup.ItemKind}");
        ApplyInventoryTransition(inventory => keepOwner
            ? InventoryAuthority.Relocate(inventory, operationId, pickup.StockId, pickup.OwnerId, 1, actor, holdAccess: InventoryHoldRules.BorrowedBy(actor))
            : InventoryAuthority.Transfer(inventory, operationId,
                pickup.OwnerId, actor, pickup.StockId, 1, "equipment_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent(new EquipmentCollectedEventPayload(actor, pickup.ItemKind));
    }

    private bool HasCarriedEquipmentAtLeast(string actor, string kind)
    {
        if (ToolProgressionRules.Find(kind) is not { } requested)
            return HasCarriedOwnItem(actor, kind);
        var carried = ToolProgressionRules.BestUsableTool(society.Checkpoint.Inventory, actor, requested.Family);
        return carried is not null && ToolProgressionRules.Find(carried.ItemKind)!.Tier >= requested.Tier;
    }

    private sealed record FireFuelEffect(string BuildingId, Identity FuelReservationId);

    private FireFuelEffect? TendFireAt(string actor, AgentState person, PlacedBuilding building)
    {
        if (survivalState is null || IsFireLit(building) ||
            !AccessibleHeatingBuildings(actor).Any(item => item.InstanceId == building.InstanceId))
            return null;
        if (!HasCarriedOwnItem(actor, "wood"))
        {
            var range = building.HouseholdId is null ? ResourceInteractionRange : 0;
            if (SharedItem("wood", actor, building.Position, range) is not null)
                CollectEquipment(actor, person, "wood", building.Position, range);
            else if (MaterialSource("wood", actor, building.Position, range) is { } source)
                GatherProjectMaterial(actor, person, "wood", source, returnTo: building.Position, returnRange: range);
            return null;
        }
        var interactionRange = building.HouseholdId is null ? ResourceInteractionRange : 0;
        if (!IsWithinInteractionRange(person.Position, building.Position, interactionRange))
        {
            MoveToward(actor, person, building.Position, "fuel_fire", interactionRange);
            return null;
        }
        if (CarriedFirewood(actor) is not { } fuel) return null;
        return CompleteFireFuel(actor, building, fuel.Lot.Id);
    }

    private FireFuelEffect CompleteFireFuel(string actor, PlacedBuilding building, string fuelId)
    {
        var previousReservations = society.Checkpoint.Inventory.Reservations.Select(item => item.Id.Value).ToHashSet(StringComparer.Ordinal);
        var result = society.Apply(checkpoint => ClankerWorld.Simulation.Society.SocietyRules.ConsumeInventory(checkpoint, actor, fuelId, 1, "heating_fuel"));
        var payment = result.Checkpoint.Inventory.Reservations.Single(item => !previousReservations.Contains(item.Id.Value) &&
            item.OwnerId == actor && item.LotId == fuelId && item.Quantity == 1 && item.Purpose == "heating_fuel" &&
            item.State == InventoryReservationState.Completed && item.ExpiryTick == WorldTick);
        survivalState = survivalState! with { Fires = survivalState.Fires.Append(new BuildingFireState(building.InstanceId, WorldTick + 120)).ToArray() };
        AppendEvent(new FireFuelledEventPayload(null, building.InstanceId));
        return new FireFuelEffect(building.InstanceId, payment.Id);
    }

    private IEnumerable<PlacedBuilding> ReachableWarmthDestinations(string actor, AgentState person) =>
        AccessibleHeatingBuildings(actor).Where(IsFireLit).Concat(AccessibleShelters(actor))
            .DistinctBy(building => building.InstanceId)
            .Where(building => !(CanUseLitHearth(actor, building)
                    ? IsWithinInteractionRange(person.Position, building.Position, WarmthDestinationRange(actor, person.Position, building))
                    : ShelterBuildingCovers(actor, building, person.Position)) &&
                WarmthDestinationPoint(actor, person, building) is { } target &&
                Reach(actor, target, ReachPurpose.WarmthProtection, WarmthDestinationRange(actor, person.Position, building), 0, SwimmingPolicy.WhenCapable).Count > 0)
            .OrderBy(building => CanUseLitHearth(actor, building) ? 0 : 1)
            .ThenBy(building => map.FootDistance(person.Position, building.Position));

    private bool CanUseLitHearth(string actor, PlacedBuilding building) => IsFireLit(building) &&
        AccessibleHeatingBuildings(actor).Any(hearth => hearth.InstanceId == building.InstanceId);

    private bool NeedsRecipeOutput(RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null) =>
        !HasUnsupportedWaterOutput(recipe) && (HouseToolsContent.IsCrudeToolRecipe(recipe) ? NeedsHouseTool(recipe, ownerId) :
        HasToolMakingDemand(recipe, ownerId, requestWorker) || recipe.Outputs.Any(output =>
    {
        if (output.ResourceId == KnowledgeContent.Paper)
            return NeedsKnowledgePaper(ownerId);
        if (survivalState is null)
            return true;
        IEnumerable<string> owners = ownerId is null ? society.Checkpoint.Inventory.Lots.Select(lot => lot.OwnerId) : [ownerId];
        var kinds = IsPreparedMeal(output.ResourceId) && !recipe.Inputs.Any(input => IsPreparedMeal(input.ResourceId))
            ? PreparedMealGoods : GoodsKinds.One(output.ResourceId);
        var available = HoldingsTotal(owners, kinds);
        return available < (IsPreparedMeal(output.ResourceId) ? Math.Max(2, (ownerId is null ? agents.Count :
            society.Checkpoint.Agents.Count(person => person.HouseholdId == ownerId && agents.ContainsKey(person.Id))) * 2)
            : output.ResourceId == "food" ? agents.Count * 4 : Math.Max(1, agents.Count));
    }));

    /// <summary>One usable family tool per adult, counting better tools and work already paid for.</summary>
    private bool NeedsHouseTool(RecipeDefinition recipe, string? householdId)
    {
        if (householdId is null) return false;
        var adults = agents.Keys.Where(actor => IsAdult(actor) && HouseholdFor(actor) == householdId).ToArray();
        if (adults.Length == 0) return false;
        var family = ToolProgressionRules.Find(recipe.Outputs[0].ResourceId)!.Family;
        var tools = society.Checkpoint.Inventory.Lots.Where(lot => lot.ContainerLotId is null &&
            lot.DeliveryBuildingId is null && ToolProgressionRules.Find(lot.ItemKind)?.Family == family &&
            AvailableLotQuantity(lot) > 0).ToArray();
        // Private extras and borrowed tools held by the same adult cover only that adult.
        var uncovered = adults.Where(actor => !tools.Any(lot =>
            lot.OwnerId == householdId && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) ||
            lot.OwnerId == actor && (ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) ||
                lot.CarrierId is null && HouseToolStockIsAccessible(actor, householdId, lot)))).ToArray();
        if (uncovered.Length == 0) return false;
        // Free shared stock can wait for an adult's route to reopen; do not keep making
        // replacements merely because that adult is temporarily away from the household.
        var available = tools.Where(lot => lot.OwnerId == householdId && lot.CarrierId is null &&
                adults.Any(actor => HouseToolStockIsAccessible(actor, householdId, lot)))
            .Sum(lot => (long)AvailableLotQuantity(lot));
        if (available >= uncovered.Length) return false;
        var committed = worldSimulation.ProductionJobs.Where(job => job.OwnerId == householdId &&
                job.ToolMakingRequestId is null &&
                job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused &&
                worldSimulation.Buildings.Any(building => building.InstanceId == job.BuildingInstanceId &&
                    building.HouseholdId == householdId && adults.Any(actor =>
                        agents[actor].Position == building.Position ||
                        Reach(actor, building.Position, ReachPurpose.PrivateEquipment, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)))
            .SelectMany(job => worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == job.RecipeId)?.Outputs ?? [])
            .Where(output => ToolProgressionRules.Find(output.ResourceId)?.Family == family)
            .Sum(output => (long)output.Amount);
        return available + committed < uncovered.Length;
    }

    private bool HouseToolStockIsAccessible(string actor, string householdId, InventoryLot lot)
    {
        if (OnBorrowedMarketStall(lot))
            return false;
        if (lot.StorageBuildingId is { } storage && !worldSimulation.Buildings.Any(building =>
                building.InstanceId == storage && building.HouseholdId == householdId))
            return false;
        var position = HouseholdStockPosition(lot);
        var range = HouseholdStockInteractionRange(lot);
        return CanReach(actor, position, ReachPurpose.PrivateEquipment, range, 0, SwimmingPolicy.WhenCapable);
    }

    private string FoodSource(InventoryLot lot)
    {
        if (IsPreparedMeal(lot.ItemKind)) return lot.ItemKind;
        var inventory = society.Checkpoint.Inventory;
        (lot, var originId) = InventoryIndex.For(inventory).ProvenanceOrigin(lot);
        var job = WorldProductionJob.OutputSource(worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? []), originId);
        if (job is not null)
        {
            return worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == job.RecipeId)?.IsCrop == true ? "crops" : "cooked";
        }
        if (lot.ItemKind == "fruit") return "orchard";
        if (FarmFieldRules.IsCrop(lot.ItemKind)) return "crops";
        return originId.StartsWith("food:harvest:", StringComparison.Ordinal) ? "foraged" : "rations";
    }

    private static bool IsPreparedMeal(string kind) => PreparedMealGoods.Ids.Contains(kind, StringComparer.Ordinal);

    private static bool IsEdibleFood(string kind) => FoodRules.IsEdible(kind);

    private static int FoodNourishment(string kind) => kind switch
    {
        "restaurant_meal" => 6_000,
        "rich_meal" => 7_000,
        "cooked_eggs" => 4_000,
        "milk_porridge" => 5_000,
        "berry_porridge" or "fruit_porridge" or "stew" => 5_000,
        "simple_meal" or "porridge" or "bread" => 4_000,
        "berries" => 2_000,
        "wild_greens" => 1_500,
        "cultivated_greens" => 4_000,
        _ => 3_000,
    };

    private IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null)
    {
        var previous = actor is not null && agents.TryGetValue(actor, out var person) ? person.Survival?.LastMealKind : null;
        var inventory = society.Checkpoint.Inventory;
        var request = new GoodsRequest(owner == actor ? GoodsUse.Eat : GoodsUse.ConsumeAt,
            actor, GoodsOwners.One(owner), EdibleFoodGoods);
        return FindGoods(inventory, request).Matches.Where(match => owner != actor
                ? match.Lot.ContainerLotId is null && match.Lot.CarrierId is null
                : match.Lot.ContainerLotId is null || match.Root.ItemKind == InventoryContainerRules.StoragePot &&
                    !InventoryIndex.For(inventory).HasReservedFamily(match.Root.Id))
            .Select(match => match.Lot)
            .OrderBy(lot => previous is not null && FoodSource(lot) == previous ? 1 : 0)
            .ThenByDescending(lot => lot.FreshnessBasisPoints).ThenBy(lot => lot.Id, StringComparer.Ordinal);
    }

    private SurvivalCondition? AfterMeal(AgentState person, InventoryLot food)
    {
        if (person.Survival is not { } condition)
        {
            return null;
        }
        var kind = FoodSource(food);
        var nutrition = Math.Clamp(condition.NutritionBasisPoints + (condition.LastMealKind is null ? 0 : condition.LastMealKind != kind ? 500 : -100) +
            (food.ItemKind == "restaurant_meal" ? 500 : food.ItemKind is "berry_porridge" or "fruit_porridge" ? 250 : 0), 0, 10_000);
        var illness = Math.Clamp(condition.IllnessBasisPoints +
            (food.FreshnessBasisPoints < 2_500 ? SpoiledMealIllnessIncreaseBasisPoints : -FreshMealIllnessReliefBasisPoints), 0, 10_000);
        AppendEvent(new MealEatenEventPayload(person.AgentId, kind, nutrition));
        return condition with { NutritionBasisPoints = nutrition, LastMealKind = kind, IllnessBasisPoints = illness };
    }

    private static void ValidateSurvival(WorldRuntimeState state)
    {
        if (state.Survival is { } survival && (survival.ActivatedTick < 0 ||
            survival.ActivatedTick > state.Society.Society.WorldTick || survival.Fires.Count > (state.WorldSimulation?.Buildings.Count ?? 0) ||
            survival.Fires.Select(fire => fire.BuildingId).Distinct(StringComparer.Ordinal).Count() != survival.Fires.Count ||
            survival.Fires.Any(fire => fire.FuelUntilTick <= state.Society.Society.WorldTick || fire.FuelUntilTick - state.Society.Society.WorldTick > 120 ||
                state.WorldSimulation?.Buildings.Any(building => building.InstanceId == fire.BuildingId) != true)))
        {
            throw new InvalidDataException("The saved survival state is invalid.");
        }
        foreach (var person in state.Agents)
        {
            if (person.Survival is { } condition && (state.Survival is null ||
                condition.WarmthBasisPoints is < 0 or > 10_000 || condition.IllnessBasisPoints is < 0 or > 10_000 ||
                condition.NutritionBasisPoints is < 0 or > 10_000 || condition.LastMealKind is not (null or "crops" or "cooked" or "foraged" or "rations" or "orchard") &&
                !IsPreparedMeal(condition.LastMealKind!)))
            {
                throw new InvalidDataException("The saved agent survival condition is invalid.");
            }
        }
    }
}
