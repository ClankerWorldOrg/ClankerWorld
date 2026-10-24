using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;


/// <summary>
/// A household plans only the buildings it needs for itself: one of each
/// household kind it does not hold, and only once the materials are in hand.
/// Buildings the Town shares wait for governance and are never offered here.
/// </summary>
public sealed partial class WorldRuntime
{
    private static void ValidateBuildingExpansionState(WorldContentSimulationState simulation,
        DeclarativeWorldContentState content, SocietyCheckpoint society, WorldMap map, int schemaVersion)
    {
        if (schemaVersion < 30 && (simulation.Buildings.Any(item => item.Footprint is not null) ||
                simulation.BuildingExpansions is { Count: > 0 } || simulation.GuestInvitations is { Count: > 0 }))
            throw new InvalidDataException("Building expansions and guest invitations require world schema 30.");
        BuildingStore.ValidateLocalState(WorldConstructionState.From(simulation));
        var buildings = simulation.Buildings.ToDictionary(item => item.InstanceId, StringComparer.Ordinal);
        var definitions = content.BuildingDefinitions;
        var people = society.Agents.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var allJobIds = simulation.ProductionJobs.Concat(simulation.CropBuilds ?? []).Select(item => item.JobId)
            .ToHashSet(StringComparer.Ordinal);
        var activeBuildings = new HashSet<string>(StringComparer.Ordinal);
        var activeExpansionTiles = new HashSet<GridPoint>();
        var expansionJobs = simulation.BuildingExpansions ?? [];
        foreach (var job in expansionJobs)
        {
            if (job.State == WorldProductionJobState.Paused && job.PausedAtTick > society.WorldTick)
                throw new InvalidDataException("The saved expansion pause differs from the world clock.");
            var hasBuilding = buildings.TryGetValue(job.BuildingInstanceId, out var building);
            var definitionId = job.DefinitionId ?? building?.DefinitionId;
            // Paused work keeps its building and footprint exactly as running work does.
            var active = job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused;
            if (!allJobIds.Add(job.JobId) || !people.ContainsKey(job.WorkerId) ||
                active && (!hasBuilding || job.OwnerId != (building!.HouseholdId ?? building.TownId)) ||
                !hasBuilding && (active || schemaVersion < 35 || string.IsNullOrWhiteSpace(job.DefinitionId)) ||
                active && hasBuilding &&
                    job.DefinitionId is not null && job.DefinitionId != building!.DefinitionId ||
                definitionId is null || !definitions.TryGetValue(definitionId, out var definition) ||
                !BuildingStorageRules.IsSupported(definition, job.TargetFootprint) ||
                job.StartedTick > society.WorldTick ||
                job.State == WorldProductionJobState.Running && job.CompletionTick <= society.WorldTick ||
                !map.Contains(job.TargetPosition))
                throw new InvalidDataException("The saved building expansion is malformed.");
            if (job.State is not (WorldProductionJobState.Running or WorldProductionJobState.Paused)) continue;
            if (!hasBuilding || !activeBuildings.Add(job.BuildingInstanceId) || building!.Position != job.ExpectedPosition ||
                (building.Footprint?.Revision ?? 0) != job.ExpectedRevision)
                throw new InvalidDataException("The saved expansion no longer refers to its original building footprint.");
            var target = BuildingStorageRules.WithSize(definition!, job.TargetFootprint.Width, job.TargetFootprint.Height);
            var targetTiles = WorldContentSimulationRules.Footprint(target, job.TargetPosition).ToArray();
            if (!WorldContentSimulationRules.Footprint(definition, building).All(targetTiles.Contains) ||
                targetTiles.Any(tile => !map.IsBuildable(tile)) ||
                targetTiles.Any(tile => !activeExpansionTiles.Add(tile)))
                throw new InvalidDataException("The saved expansion has an invalid or competing footprint.");
            var reserved = new Dictionary<string, int>(StringComparer.Ordinal);
            var buildingSite = new InventoryGroundPosition(building.Position.X, building.Position.Y);
            foreach (var id in job.InputReservationIds)
            {
                var reservation = society.Inventory.Reservations.SingleOrDefault(item => item.Id == id);
                var lot = reservation is null ? null : society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation.LotId);
                if (reservation is null || lot is null || reservation.Purpose != job.JobId ||
                    reservation.OwnerId != lot.OwnerId || lot.OwnerId != job.OwnerId && lot.OwnerId != job.WorkerId ||
                    (lot.OwnerId == job.OwnerId
                        ? lot.StorageBuildingId != building.InstanceId && lot.GroundPosition != buildingSite ||
                            lot.DeliveryBuildingId is not null
                        : lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null ||
                            lot.GroundPosition is not null) ||
                    reservation.State != InventoryReservationState.Reserved || reservation.ExpiryTick != (job.State == WorldProductionJobState.Paused ? long.MaxValue : job.CompletionTick))
                    throw new InvalidDataException("The expansion is missing its reserved materials.");
                reserved[lot.ItemKind] = reserved.GetValueOrDefault(lot.ItemKind) + reservation.Quantity;
            }
            var expected = BuildingStorageRules.ExpansionCosts(definition, building, job.TargetFootprint);
            if (expected.Count != reserved.Count || expected.Any(cost => reserved.GetValueOrDefault(cost.ResourceId) != cost.Amount))
                throw new InvalidDataException("The expansion's reserved materials do not match its footprint.");
        }
        var invitations = simulation.GuestInvitations ?? [];
        foreach (var invitation in invitations)
        {
            if (!buildings.TryGetValue(invitation.HouseInstanceId, out var house) || house.HouseholdId is null ||
                !definitions[house.DefinitionId].Tags.Contains("house", StringComparer.Ordinal) ||
                !people.ContainsKey(invitation.GuestId) || !people.ContainsKey(invitation.InvitedById) ||
                invitation.ChangedTick > society.WorldTick)
                throw new InvalidDataException("The saved House guest invitation is malformed.");
        }

    }
    private BuildingMaterialActionPlan? PlanBuildingMaterial(string actor, string household)
    {
        if (NeededBuildingMaterial(actor, household) is not { } need) return null;
        var house = HouseForHousehold(household);
        var destination = house?.Position ?? SharedStoragePosition;
        var range = house is null ? ResourceInteractionRange : 0;
        var source = need.Source;
        var bonus = UseHarvestBonusForBuildingMaterial(actor, need.Material.ResourceId, source, destination, range);
        var harvest = ProjectMaterialHarvest(actor, need.Material.ResourceId, source, bonus);
        if (harvest is null) return null;
        var tool = harvest.ToolLotId is { } id ? society.Checkpoint.Inventory.GetLot(id) : null;
        return new(actor, new(need.Material.ResourceId), household, need.Building, need.Material, house?.InstanceId,
            destination, range, source, harvest, tool?.OwnerId, tool?.ItemKind,
            Reach(actor, source.Position, ReachPurpose.BuildingPreparation, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            HarvestReturnRoute(actor, source, destination, ReachPurpose.BuildingPreparation,
                checked(harvest.Quantity + harvest.TreeSeedQuantity), range));
    }

    private ActionCheck CheckBuildingMaterial(BuildingMaterialActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ProjectMaterialCapabilities);
        if (!capability.Allowed) return capability;
        var house = HouseForHousehold(plan.HouseholdId);
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || house?.InstanceId != plan.HouseId ||
            (house?.Position ?? SharedStoragePosition) != plan.Destination ||
            HouseholdBuildingProjectInProgress(plan.HouseholdId, retryingActor: plan.Actor) ||
            !PlannableHouseholdBuildings(plan.HouseholdId, plan.Actor).Any(definition => definition.CanonicalId == plan.Building.CanonicalId &&
                definition.PayloadDigest == plan.Building.PayloadDigest) ||
            !WorkBuildingHasInputs(plan.Actor, plan.HouseholdId, plan.Building) ||
            HouseholdMaterialInHand(plan.HouseholdId, plan.Material.ResourceId) >= plan.Material.Amount)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household building need or storage destination has changed");
        var harvest = CheckPlannedProjectHarvest(plan.Actor, plan.Source, plan.Harvest, plan.ToolOwnerId, plan.ToolKind);
        if (!harvest.Allowed) return harvest;
        if (FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected building material harvest");
        if (HarvestReturnRoute(plan.Actor, plan.Source, plan.Destination, ReachPurpose.BuildingPreparation,
                plan.PhysicalQuantity, plan.DestinationRange).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building materials cannot return to household storage");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Source.Position, ReachPurpose.BuildingPreparation, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building material source is out of reach");
    }

    private void ApplyCheckedBuildingMaterial(BuildingMaterialActionPlan plan)
    {
        GatherPlannedProjectMaterial(plan.Actor, agents[plan.Actor], plan.Choice.ItemKind, plan.Source, plan.Harvest, plan.HouseId);
    }
    private void CopyConstructionProjectPlansFrom(WorldRuntime source)
    {
        if (ReferenceEquals(this, source)) return;
        constructionSystem.CopyPlansFrom(source.constructionSystem, (actor, started, plan) =>
            agents.TryGetValue(actor, out var person) && person.Project is { Stage: not ("completed" or "cancelled") } project &&
            project.StartedTick == started && project.CandidateId == plan.CandidateId && project.OrderInstructionId == plan.InstructionId);
    }

    private ConstructionActionPlan PlanConstruction(string actor, BuildingDefinition definition, GridPoint site, string? selectedInstance = null) =>
        ConstructionSystem.PlanConstruction(ConstructionWorld, actor, definition, site, selectedInstance);

    private ConstructionMaterialInput[]? PlanConstructionInputs(IReadOnlyList<ContentQuantity> costs, string owner) =>
        ConstructionSystem.PlanConstructionInputs(ConstructionWorld, costs, owner);

    private static ActorCapabilityPolicy ConstructionCapabilities(BuildingDefinition definition) => ConstructionSystem.ConstructionCapabilities(definition);

    private ActionCheck CheckConstruction(ConstructionActionPlan plan, bool checkDestination = true, ActorCapabilityPolicy? policy = null) =>
        constructionSystem.CheckConstruction(ConstructionWorld, plan, checkDestination, policy);

    private ConstructionActionPlan? RetainedConstruction(string actor, AgentProject project) => constructionSystem.RetainedPlan(actor, project);
    private ExpansionActionPlan? PlanExpansion(string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction = null) =>
        ConstructionSystem.PlanExpansion(ExpansionPlanningWorld, actor, building, instruction);

    private ExpansionMaterialStep? PlanExpansionMaterialStep(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs)
    {
        var missing = costs.FirstOrDefault(cost => !HasExpansionMaterials(actor, building, [cost]) &&
            ExpansionDelivery(actor, building, cost.ResourceId) is not null);
        if (missing.Amount == 0) missing = costs.FirstOrDefault(cost => !HasExpansionMaterials(actor, building, [cost]));
        if (missing.Amount == 0) return null;
        if (ExpansionDelivery(actor, building, missing.ResourceId) is { } delivery)
        {
            var quantity = Math.Min(AvailableLotQuantity(delivery), DestinationRoom(building.InstanceId, delivery));
            return new ExpansionDeliveryStep(missing.ResourceId, delivery.Id,
                quantity > 0 ? quantity : AvailableLotQuantity(delivery), quantity > 0);
        }
        var pickup = ExpansionSharedMaterialPickup(actor, building, missing);
        if (pickup.Source is { } shared)
            return new ExpansionPickupStep(missing.ResourceId, shared.Id, shared.OwnerId, pickup.Quantity,
                HouseholdStockPosition(shared), HouseholdStockInteractionRange(shared));
        if (MaterialSource(missing.ResourceId, actor, building.Position) is not { } source ||
            ProjectMaterialHarvest(actor, missing.ResourceId, source) is not { } harvest) return null;
        var tool = harvest.ToolLotId is { } id ? society.Checkpoint.Inventory.GetLot(id) : null;
        return new ExpansionGatherStep(missing.ResourceId, source, harvest, tool?.OwnerId, tool?.ItemKind,
            HarvestReturnRoute(actor, source, building.Position, ReachPurpose.BuildingExpansion,
                checked(harvest.Quantity + harvest.TreeSeedQuantity), 0));
    }

    private ActionCheck CheckExpansionMaterialStep(ExpansionActionPlan plan, PlacedBuilding building)
    {
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var costs = BuildingStorageRules.ExpansionCosts(definition, building, plan.TargetFootprint);
        if (plan.MaterialStep is { } material)
        {
            var cost = costs.FirstOrDefault(cost => cost.ResourceId == material.ItemKind);
            if (cost.Amount == 0 || HasExpansionMaterials(plan.Actor, building, [cost]))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion material need is already satisfied");
            var remaining = cost.Amount - ExpansionMaterialLots(plan.Actor, building).Where(lot => lot.ItemKind == material.ItemKind).Sum(AvailableLotQuantity) -
                ExpansionInboundQuantity(building, material.ItemKind);
            if (material is ExpansionPickupStep pickup && remaining < pickup.Quantity || material is ExpansionGatherStep && remaining <= 0)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion material need is already covered by inbound stock");
        }
        GridPoint destination;
        int range;
        switch (plan.MaterialStep)
        {
            case ExpansionDeliveryStep step:
                if (ExpansionDelivery(plan.Actor, building, step.ItemKind, step.LotId) is not { } delivery ||
                    AvailableLotQuantity(delivery) < step.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion delivery lot is unavailable");
                if (step.Store && DestinationRoom(building.InstanceId, delivery) < step.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected expansion storage has no room for the delivery");
                destination = building.Position;
                range = 0;
                break;
            case ExpansionPickupStep step:
                if (RecheckGoods(DeliveryGoodsRequest(plan.Actor, step.OwnerId, step.ItemKind, building.InstanceId,
                        GoodsDeliveryPurpose.Expansion), step.LotId) is not { } match || match.Quantity < step.Quantity ||
                    HouseholdStockPosition(match.Lot) != step.Position || HouseholdStockInteractionRange(match.Lot) != step.InteractionRange ||
                    match.Lot.StorageBuildingId == building.InstanceId || match.Lot.CarrierId is not null ||
                    match.Lot.GroundPosition == ExpansionGroundPosition(building) || match.Lot.ContainerLotId is not null)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion pickup lot or location has changed");
                if (PickupCarryCapacity(plan.Actor, match.Lot, building.Position) < step.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room to carry the selected expansion pickup");
                destination = step.Position;
                range = step.InteractionRange;
                break;
            case ExpansionGatherStep step:
                var harvest = CheckPlannedProjectHarvest(plan.Actor, step.Source, step.Harvest, step.ToolOwnerId, step.ToolKind);
                if (!harvest.Allowed) return harvest;
                var quantity = checked(step.Harvest.Quantity + step.Harvest.TreeSeedQuantity);
                if (FreeCarryCapacity(plan.Actor) < quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected expansion harvest");
                if (HarvestReturnRoute(plan.Actor, step.Source, building.Position, ReachPurpose.BuildingExpansion, quantity, 0).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected expansion harvest cannot return to the building");
                destination = step.Source.Position;
                range = ResourceInteractionRange;
                break;
            default:
                destination = building.Position;
                range = 0;
                break;
        }
        return IsWithinInteractionRange(agents[plan.Actor].Position, destination, range) ||
            Reach(plan.Actor, destination, ReachPurpose.BuildingExpansion, range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected expansion material step is out of reach");
    }

    private ExpansionMaterialInput[]? PlanExpansionInputs(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) =>
        ConstructionSystem.PlanExpansionInputs(ExpansionPlanningWorld, actor, building, costs);

    private static ActorCapabilityPolicy ExpansionCapabilities => ConstructionSystem.ExpansionCapabilities;

    private ActionCheck CheckExpansion(ExpansionActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        constructionSystem.CheckExpansion(ExpansionPlanningWorld, plan, policy);
    // This is a prerequisite for choosing work, not a reservation of future
    // production inputs. Offers, delayed choices and unstarted projects read it afresh.
    private bool WorkBuildingHasInputs(string actor, string household, BuildingDefinition building)
    {
        var kind = HouseholdBuildingKind(building);
        if (kind is null or "house" or "store") return true;
        var town = towns.SingleOrDefault(item => item.ResidentIds.Contains(actor, StringComparer.Ordinal));
        var home = HouseForHousehold(household)?.Position ?? agents[actor].Position;
        var owners = new GoodsOwners(agents.Keys.Where(member => HouseholdFor(member) == household)
            .Prepend(household).Concat(WarehousesAccessibleTo(actor).Select(warehouse => warehouse.TownId!))
            .Distinct(StringComparer.Ordinal).ToArray());
        var tools = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
        var inputs = new Dictionary<(string Item, int Quantity, bool NearTown), bool>();
        var seedArea = new Lazy<HashSet<GridPoint>>(() =>
            FarmingSystem.NearbyFarmTiles(farmingWorld, town?.OriginSite ?? home).ToHashSet());
        bool Reachable(GridPoint position, int range = 0) =>
            IsWithinInteractionRange(agents[actor].Position, position, range) ||
            Reach(actor, position, ReachPurpose.WorkstationSupply, range, 0, SwimmingPolicy.WhenCapable,
                ReachObstacles.Terrain).Count > 0;
        bool Input(string item, int quantity, bool nearTown = false)
        {
            var key = (item, quantity, nearTown);
            if (!inputs.TryGetValue(key, out var available))
                inputs.Add(key, available = InputAvailable(item, quantity, nearTown));
            return available;
        }
        bool InputAvailable(string item, int quantity, bool nearTown)
        {
            // Construction must not spend the only stock that would give the
            // new station work. Natural supplies can be gathered afterwards.
            var cost = building.BuildCosts.Where(input => input.ResourceId == item).Sum(input => input.Amount);
            var stock = FindGoods(new(GoodsUse.ReachableHoldings, actor, owners, GoodsKinds.One(item))).Matches
                .Where(match => (match.Root.CarrierId is null || match.Root.CarrierId == actor) &&
                    MayCollectGoodsAt(actor, match.Root) && (!nearTown || match.Place.Position is { } position &&
                        seedArea.Value.Contains(position)))
                .Sum(match => (long)match.Quantity);
            if (stock >= quantity + cost)
                return true;
            if (item == InventoryContainerRules.FreshWater)
                return FindGoods(new(GoodsUse.Collect, actor, owners, GoodsKinds.One(InventoryContainerRules.WaterJug))).First is not null &&
                    FindFreshWaterShore(actor, agents[actor].Position, returnTo: home) is not null;
            return map.Resources.Where(source => (source.Kind == item || item == "wood" && source.Kind == "construction") &&
                (!nearTown || seedArea.Value.Contains(source.Position)) &&
                resources.GetValueOrDefault(source.Id) == ResourceState.Available &&
                worldSystems.Ecology.GetResource(source.Id).Quantity >= quantity)
                .OrderBy(source => map.FootDistance(agents[actor].Position, source.Position))
                .ThenBy(source => source.Id, StringComparer.Ordinal)
                .Any(source => CanGatherFromSource(actor, item, source, tools) && Reachable(source.Position, ResourceInteractionRange));
        }
        if (kind is "farmhouse" or "silo")
            return fields.Any(field => field.HouseholdId == household && Reachable(field.Position)) ||
                new[] { FarmFieldRules.GrainSeed, FarmFieldRules.GreensSeed, FarmFieldRules.Potatoes }
                    .Any(seed => Input(seed, 1, nearTown: true));
        if (kind == AnimalContent.YardTag)
            return !worldSimulation.Buildings.Any(yard => yard.TownId == town?.Id &&
                    worldContent.BuildingDefinitions[yard.DefinitionId].Tags.Contains(AnimalContent.YardTag) &&
                    yard.HouseholdId is { } holder && HasAnimalSpace(holder, yard)) &&
                Animals.Any(animal => animal.DiedTick is null && animal.HouseholdId is null && animal.LeaderId is null &&
                    (animal.TamingWork is null || animal.TamingWork.ActorId == actor) && MayLeadAnimal(actor, animal) &&
                    (agents[actor].Position == animal.Position || Reach(actor, animal.Position,
                        ReachPurpose.AnimalWork, 0, 0, SwimmingPolicy.Never, ReachObstacles.Terrain).Count > 0));

        var recipes = worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.CanonicalId && !recipe.IsCrop);
        var foodSurplus = new Lazy<long>(() => RestaurantFoodSurplus(actor, household, town));
        return recipes.Any(recipe => recipe.Inputs.All(input => Input(input.ResourceId, input.Amount)) &&
            (kind != "restaurant" || foodSurplus.Value >= Math.Max(1,
                recipe.Inputs.Where(input => IsEdibleFood(input.ResourceId)).Sum(input => input.Amount))));
    }

    private long RestaurantFoodSurplus(string actor, string household, TownRuntimeState? town)
    {
        var residents = society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
            (town is null ? person.HouseholdId == household : town.ResidentIds.Contains(person.Id, StringComparer.Ordinal))).ToArray();
        var owners = residents.Select(person => person.Id).Concat(residents.Select(person => person.HouseholdId).OfType<string>())
            .Concat(town is null ? [] : new[] { town.Id }).Distinct(StringComparer.Ordinal).ToArray();
        var stock = FindGoods(new(GoodsUse.ReachableHoldings, actor, new(owners), EdibleFoodGoods)).Total;
        return stock - residents.Length * FarmFieldRules.MealsPerPersonPerDay;
    }
    private const string GatherBuildingMaterialPrefix = BuildingMaterialChoice.Prefix;
    // Provisional balance: tune the shared offer/admission limit here.
    private const int ResidentsPerSharedShop = 8;

    private void AddHouseholdBuildingPlans(List<CognitionCandidate> candidates, SocietyAgent agent,
        bool needsWarmth, string householdId, List<string> buildingsWithoutSites)
    {
        TownLayoutContext? sharedLayout = null;
        foreach (var definition in PlannableHouseholdBuildings(householdId, agent.Id)
                     .Where(building => !needsWarmth || building.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking")))
        {
            // The identity reads the same actor, design and reservations for this
            // synchronous query; compute it once rather than for every building.
            var instanceId = BuildInstanceId(agent.Id, definition);
            if (worldSimulation.Buildings.Any(item => item.InstanceId == instanceId) ||
                !HouseholdHasMaterialsInHand(householdId, definition.BuildCosts) ||
                !WorkBuildingHasInputs(agent.Id, householdId, definition))
                continue;

            var layout = HouseholdBuildingKind(definition) == "silo"
                ? CreateTownLayoutContext(agent.Id, building: definition)
                : sharedLayout ??= CreateTownLayoutContext(agent.Id);
            var sites = TownLayoutService.RankConstructionSites(layout, definition);
            if (sites.Count == 0) buildingsWithoutSites.Add(definition.DisplayName);
            for (var rank = 0; rank < sites.Count; rank++)
            {
                var site = sites[rank];
                var description = string.Join(" ", site.Reasons.Select(reason => reason.Description));
                var plan = PlanConstruction(agent.Id, definition, site.Position, instanceId);
                if (CheckConstruction(plan).Allowed) candidates.Add(new CognitionCandidate(
                    plan.CandidateId,
                    $"Plan {definition.DisplayName} at ({site.Position.X}, {site.Position.Y}): {description}",
                    20 + rank + HouseholdBuildingKinds.PlanOrder(HouseholdBuildingKind(definition)),
                    $"build-site:{site.Position.X},{site.Position.Y}")
                { ActionPlan = plan });
            }
        }

        AddBuildingMaterialCandidate(candidates, agent.Id, householdId);
    }

    /// <summary>Active building designs this household may plan now, in a stable order.</summary>
    private IEnumerable<BuildingDefinition> PlannableHouseholdBuildings(string householdId, string actor) => worldContent.Buildings
        .Where(definition => !RetiredBuildings.Contains(definition) &&
            HouseholdBuildingKind(definition) is { } kind && HouseholdMayPlan(householdId, kind, actor))
        .OrderBy(definition => HouseholdBuildingKinds.PlanOrder(HouseholdBuildingKind(definition)))
        .ThenBy(definition => definition.CanonicalId, StringComparer.Ordinal);

    private bool HouseholdMayPlan(string householdId, string kind, string actor) =>
        HouseholdBuildingWithTag(householdId, kind) is null &&
        TownHasRoomForSharedShop(actor, kind) &&
        (kind != "silo" || FarmhouseForHousehold(householdId) is not null) &&
        (kind != AnimalContent.YardTag || HouseForHousehold(householdId) is not null) &&
        !HouseholdBuildingProjectInProgress(householdId, kind, actor);

    /// <summary>Placed shops and other residents' live construction plans hold a Town slot.</summary>
    private bool TownHasRoomForSharedShop(string actor, string? kind)
    {
        if (kind is not ("store" or "tailor" or "restaurant" or "clinic" or "blacksmith"))
            return true;
        if (TownForResident(actor) is not { } townId)
            return false;
        var town = towns.Single(item => item.Id == townId);
        var limit = (town.ResidentIds.Count + ResidentsPerSharedShop - 1) / ResidentsPerSharedShop;
        var placed = worldSimulation.Buildings.Count(building => building.TownId == townId &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                HouseholdBuildingKind(definition) == kind));
        var planned = agents.Values.Count(person => person.AgentId != actor &&
            TownForResident(person.AgentId) == townId &&
            person.Project is { Stage: not ("completed" or "cancelled") } project &&
            TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) && selection.IsBuilding &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == selection.DefinitionId &&
                HouseholdBuildingKind(definition) == kind));
        return placed + planned < limit;
    }

    /// <summary>Whether a member already has a live plan for this kind, so the household plans at most one.</summary>
    private bool HouseholdBuildingProjectInProgress(string householdId, string? kind = null,
        string? retryingActor = null) =>
        agents.Values.Any(person =>
            person.Project is { } project && project.Stage is not ("completed" or "cancelled") &&
            TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) && selection.IsBuilding &&
            society.Checkpoint.GetAgent(person.AgentId).HouseholdId == householdId &&
            worldContent.Buildings.FirstOrDefault(definition => definition.CanonicalId == selection.DefinitionId) is { } planned &&
            HouseholdBuildingKind(planned) is { } plannedKind && (kind is null || plannedKind == kind) &&
            !(person.AgentId == retryingActor &&
                (project.RequiresFreshChoice || project.Stage == "blocked" &&
                    WorldTick - project.LastTransitionTick >= BlockedProjectRetryDelayTicks)));

    /// <summary>Usable materials the household or its members own, wherever they store them.</summary>
    private bool HouseholdHasMaterialsInHand(string householdId, IReadOnlyList<ContentQuantity> costs) =>
        costs.All(cost => HouseholdMaterialInHand(householdId, cost.ResourceId) >= cost.Amount);

    private long HouseholdMaterialInHand(string householdId, string itemKind) =>
        HoldingsTotal(agents.Keys.Where(actor => HouseholdFor(actor) == householdId).Prepend(householdId),
            GoodsKinds.One(itemKind));

    /// <summary>
    /// The first material missing for the first building the household still
    /// needs, when it can be gathered from a reachable source. One such offer
    /// at a time keeps the choice list short.
    /// </summary>
    private (BuildingDefinition Building, ContentQuantity Material, MapResource Source)? NeededBuildingMaterial(string actor, string householdId)
    {
        if (HouseholdBuildingProjectInProgress(householdId, retryingActor: actor))
            return null;
        foreach (var definition in PlannableHouseholdBuildings(householdId, actor))
        {
            var missing = definition.BuildCosts.FirstOrDefault(cost =>
                HouseholdMaterialInHand(householdId, cost.ResourceId) < cost.Amount);
            if (missing.Amount == 0 || !WorkBuildingHasInputs(actor, householdId, definition))
                continue;
            if (MaterialSource(missing.ResourceId, actor, HouseForHousehold(householdId)?.Position ?? SharedStoragePosition,
                HouseForHousehold(householdId) is null ? ResourceInteractionRange : 0, useHarvestBonus: false) is { } source)
                return (definition, missing, source);
        }
        return null;
    }

    private void AddBuildingMaterialCandidate(List<CognitionCandidate> candidates, string actor, string householdId)
    {
        var plan = PlanBuildingMaterial(actor, householdId);
        if (plan is null || !CheckBuildingMaterial(plan).Allowed) return;
        var load = plan.Harvest.Quantity > 4 ? "using its faster whole load" : "as a smaller whole load that fits your carrying space";
        candidates.Add(new CognitionCandidate(plan.CandidateId,
            $"Gather {plan.Material.ResourceId} {load} so the household has what it needs to build its own {plan.Building.DisplayName}.",
            34)
        { ActionPlan = plan });
    }

    /// <summary>Use the faster harvest only when its complete output fits the current carry space.</summary>
    private bool UseHarvestBonusForBuildingMaterial(string actor, string itemKind, MapResource source,
        GridPoint? returnTo = null, int returnRange = 0)
    {
        var plan = ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus: true);
        return plan is { ToolLotId: not null, Quantity: > 4 } &&
            FreeCarryCapacity(actor) >= checked(plan.Quantity + plan.TreeSeedQuantity) &&
            (returnTo is null || CanReturnWithHarvest(actor, itemKind, source, returnTo.Value, returnRange));
    }

    /// <summary>
    /// Store a spare generic tool at home so preparation materials have room
    /// to travel. Building work uses an available hammer or proceeds by hand.
    /// </summary>
    private InventoryLot? BuildingPreparationToolToStore(string actor, string householdId, string? sourceLotId = null)
    {
        if (agents[actor].Project is { Stage: not ("completed" or "cancelled") } ||
            HouseForHousehold(householdId) is not { } house || DestinationRoom(house.InstanceId) == 0 ||
            NeededBuildingMaterial(actor, householdId) is not { } need ||
            MaterialSource(need.Material.ResourceId, actor, HouseForHousehold(householdId)?.Position ?? SharedStoragePosition,
                HouseForHousehold(householdId) is null ? ResourceInteractionRange : 0, useHarvestBonus: false) is not { } source)
            return null;

        var free = FreeCarryCapacity(actor);
        var required = ProjectMaterialCarryUnits(actor, need.Material.ResourceId, source, useHarvestBonus: false);
        if (free >= required)
            return null;

        var tool = SpendingGoods(actor, GoodsUse.ConsumeAt, [actor, householdId], "tool").Select(match => match.Lot).Where(lot =>
                (lot.OwnerId == actor || lot.OwnerId == householdId) &&
                PersonalEquipmentRules.IsCarried(lot, actor) && lot.DeliveryBuildingId is null &&
                lot.ContainerLotId is null &&
                lot.ItemKind == "tool" && AvailableLotQuantity(lot) > 0 && (sourceLotId is null || lot.Id == sourceLotId))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
        return tool is not null && free + Math.Min(1, AvailableLotQuantity(tool)) >= required ? tool : null;
    }
    private HouseGuestActionPlan? PlanHouseGuest(string actor, HouseGuestChoice choice, string? houseId = null, bool offeredChoice = true)
    {
        if (!agents.ContainsKey(actor)) return null;
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        var house = houseId is null ? household is null ? null : HouseForHousehold(household)
            : worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == houseId);
        if (house is null) return null;
        return new(actor, choice, house, household,
            agents.ContainsKey(choice.GuestId) ? society.Checkpoint.GetAgent(choice.GuestId).Name : choice.GuestId, offeredChoice);
    }

    private ActionCheck CheckHouseGuest(HouseGuestActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseGuestCapabilities(plan.OfferedChoice));
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.House.InstanceId) is not { } house ||
            house.DefinitionId != plan.House.DefinitionId || house.PlacedTick != plan.House.PlacedTick || house.HouseholdId is null ||
            house.HouseholdId != plan.ActorHouseholdId || society.Checkpoint.GetAgent(plan.Actor).HouseholdId != plan.ActorHouseholdId ||
            !worldContent.Buildings.Any(definition => definition.CanonicalId == house.DefinitionId && definition.Tags.Contains("house", StringComparer.Ordinal)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Only an adult member of the House's household can invite or revoke a guest.");
        if (!agents.TryGetValue(plan.Choice.GuestId, out var guest))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Choose a living named guest.");
        if (society.Checkpoint.GetAgent(guest.AgentId).HouseholdId == house.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "Household members already have access to their own House.");
        if (plan.OfferedChoice)
        {
            if (HasHouseGuestInvitation(guest.AgentId, house.InstanceId) == plan.Choice.Invited)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected guest invitation already matches this choice");
            if (plan.Choice.Invited && !IsWithinInteractionRange(agents[plan.Actor].Position, guest.Position, 2))
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer close enough to invite that guest");
        }
        return new();
    }
}
