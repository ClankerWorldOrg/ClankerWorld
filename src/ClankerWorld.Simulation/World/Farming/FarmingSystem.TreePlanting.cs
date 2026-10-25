using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    internal static string FarmCandidate(FarmWorkKind kind, GridPoint point, string? crop = null) =>
        new FieldChoice(kind, point, crop).Encode();

    internal void AddFieldCandidates(IFarmingWorld world, List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        var householdId = world.Society.GetAgent(actor).HouseholdId;
        if (householdId is null || world.FarmhouseForHousehold(householdId) is not { } farmhouse ||
            world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state) ||
            (state.Project is { Stage: not ("completed" or "cancelled") } project && !project.RequiresFreshChoice)) return;
        var fertility = Fertility(world.Map);
        var demand = FarmFoodDemand(world, householdId);
        var hasHoe = ToolProgressionRules.PlanWork(world.Society.Inventory, actor, ToolFamily.Hoe) is not null;
        foreach (var field in fields.Where(field => field.HouseholdId == householdId && field.Work is null))
        {
            if (!world.CanReach(actor, field.Position, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain)) continue;
            if (field.Stage == FarmFieldStage.Ready)
                AddPlannedFieldCandidate(world, candidates, actor, new(FarmWorkKind.Harvest, field.Position), "Harvest the ready crop; it stays on the ground until carried.", 12);
            else if (hasHoe && field.Stage == FarmFieldStage.Growing && !field.Tended)
                AddPlannedFieldCandidate(world, candidates, actor, new(FarmWorkKind.Tend, field.Position), "Tend the growing crop with a hoe.", 13);
            else if (field.Stage is FarmFieldStage.Prepared or FarmFieldStage.Harvested && demand.MissingMeals > 0)
            {
                if (HasOtherAgentClaimedPlanting(world, field, actor)) continue;
                var priority = 14;
                foreach (var (crop, seed) in PlannedPlantingStock(world, actor, field))
                    AddPlannedFieldCandidate(world, candidates, actor, new(FarmWorkKind.Plant, field.Position, crop),
                        $"Carry {FarmFieldRules.PlantingItem(crop).Replace('_', ' ')} to the field and plant {crop.Replace('_', ' ')}.", priority++, seed);
            }
        }
        var wantedFields = WantedFields(world, householdId);
        if (!hasHoe || fields.Count(field => field.HouseholdId == householdId) >= wantedFields) return;
        var heldByOthers = world.HouseholdLandHeldByOthers(householdId);
        var site = NearbyFarmTiles(world, farmhouse.Position)
            .Where(point => !heldByOthers.Contains(point) && FarmableFreeTile(world, point, fertility))
            .OrderByDescending(point => fields.Any(field => field.HouseholdId == householdId && world.Map.FootDistance(field.Position, point) == 1))
            .ThenByDescending(point => fertility.At(point) - world.Map.FootDistance(farmhouse.Position, point) * 2)
            .ThenBy(point => point.Y).ThenBy(point => point.X)
            .Cast<GridPoint?>().FirstOrDefault(point => world.CanReach(actor, point!.Value, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain));
        if (site is { } chosen)
            AddPlannedFieldCandidate(world, candidates, actor, new(FarmWorkKind.Till, chosen),
                $"Prepare {World.LandFertility.Description(fertility.At(chosen)).ToLowerInvariant()} land for another household field.", 17);
    }

    private void AddPlannedFieldCandidate(IFarmingWorld world, List<CognitionCandidate> candidates, string actor,
        FieldChoice choice, string description, int priority, InventoryLot? selectedSeed = null)
    {
        if (PlanFieldAction(world, actor, choice, selectedSeed) is { } plan && CheckFieldPlan(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, priority) { ActionPlan = plan });
    }

    private int WantedFields(IFarmingWorld world, string householdId)
    {
        var demand = FarmFoodDemand(world, householdId);
        var farmhouse = world.FarmhouseForHousehold(householdId)!;
        var expectedYield = Math.Max(1, FarmFieldRules.HarvestQuantity(FarmFieldRules.Grain, Fertility(world.Map).At(farmhouse.Position)));
        return (int)Math.Ceiling(demand.MissingMeals / (demand.FarmingHouseholds * (double)expectedYield));
    }

    internal static IEnumerable<GridPoint> NearbyFarmTiles(IFarmingWorld world, GridPoint origin)
    {
        for (var dy = -12; dy <= 12; dy++)
            for (var dx = -12; dx <= 12; dx++)
            {
                var x = origin.X + dx;
                if (world.Map.WrapsEastWest) x = (x % world.Map.Width + world.Map.Width) % world.Map.Width;
                var point = new GridPoint(x, origin.Y + dy);
                if (world.Map.Contains(point)) yield return point;
            }
    }

    internal bool HasOtherAgentClaimedPlanting(IFarmingWorld world, FarmFieldState field, string actor)
    {
        var runtimes = world.Cognition.Runtimes;
        foreach (var runtime in runtimes.Where(item => item.AgentId != actor))
        {
            if (runtime.CurrentIntention is not { } intention) continue;
            var crop = new[] { FarmFieldRules.Greens, FarmFieldRules.Grain, FarmFieldRules.Potatoes }
                .FirstOrDefault(item => intention.CandidateId == FarmCandidate(FarmWorkKind.Plant, field.Position, item));
            if (crop is null) continue;
            var eligible = world.Agents.TryGetValue(runtime.AgentId, out var claimant) &&
                world.IsAdult(runtime.AgentId) && world.HouseholdFor(runtime.AgentId) == field.HouseholdId &&
                !world.NeedsUrgentFood(claimant) && !world.NeedsUrgentWarmth(claimant) &&
                ToolProgressionRules.PlanWork(world.Society.Inventory, runtime.AgentId, ToolFamily.Hoe) is not null &&
                (claimant.Project is not { Stage: not ("completed" or "cancelled") } project || project.RequiresFreshChoice) &&
                FarmNeedsFood(world, field.HouseholdId);
            var stock = eligible ? PlantingStock(world, runtime.AgentId, field, crop) : null;
            if (stock is not null) return true;
        }
        return false;
    }

    internal bool FarmNeedsFood(IFarmingWorld world, string householdId) => FarmFoodDemand(world, householdId).MissingMeals > 0;

    internal (int MissingMeals, int FarmingHouseholds) FarmFoodDemand(IFarmingWorld world, string householdId)
    {
        var checkpoint = world.Society;
        var townId = world.FarmhouseForHousehold(householdId)?.TownId;
        var town = world.Towns.SingleOrDefault(item => item.Id == townId);
        var residents = checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
            (town is null ? person.HouseholdId == householdId : town.ResidentIds.Contains(person.Id, StringComparer.Ordinal)))
            .ToArray();
        var foodOwners = town is null ? new HashSet<string>(StringComparer.Ordinal) { householdId }
            : residents.Select(person => person.Id).Concat(residents.Select(person => person.HouseholdId)
                .OfType<string>()).ToHashSet(StringComparer.Ordinal);
        var stock = world.EdibleFoodHoldings(foodOwners);
        // The budget changes no ownership or access: each household still works
        // its own fields and must physically carry its own planting supplies.
        var farmingHouseholds = town is null ? 1 : residents.Select(person => person.HouseholdId).OfType<string>()
            .Distinct(StringComparer.Ordinal).Count(owner => world.FarmhouseForHousehold(owner)?.TownId == town.Id);
        // Starter rations can cover today's meals without establishing crop production.
        // Keep one field's first planting eligible; reserves still govern expansion and replanting.
        var minimum = fields.Any(field => field.HouseholdId == householdId && field.Crop is not null) ? 0 : 1;
        return ((int)Math.Max(minimum, residents.Length * FarmFieldRules.MealsPerPersonPerDay * 2 - stock),
            Math.Max(1, farmingHouseholds));
    }

    // A missing unit for existing eligible field work, not an inventory buffer.
    internal bool WantsFieldPlantingStock(IFarmingWorld world, string actor, string itemKind)
    {
        var crop = new[] { FarmFieldRules.Greens, FarmFieldRules.Grain, FarmFieldRules.Potatoes }
            .FirstOrDefault(candidate => FarmFieldRules.PlantingItem(candidate) == itemKind);
        if (crop is null || !world.IsAdult(actor) ||
            world.Society.GetAgent(actor).Status != SocietyAgentStatus.Active ||
            world.Society.GetAgent(actor).HouseholdId is not { } householdId ||
            world.FarmhouseForHousehold(householdId) is null || !FarmNeedsFood(world, householdId) ||
            world.NeedsUrgentFood(world.Agents[actor]) || world.NeedsUrgentWarmth(world.Agents[actor]) ||
            (world.Agents[actor].Project is { Stage: not ("completed" or "cancelled") } project && !project.RequiresFreshChoice) ||
            ToolProgressionRules.PlanWork(world.Society.Inventory, actor, ToolFamily.Hoe) is null)
            return false;
        return fields.Any(field => field.HouseholdId == householdId && field.Work is null &&
            field.Stage is FarmFieldStage.Prepared or FarmFieldStage.Harvested &&
            world.CanReach(actor, field.Position, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain) &&
            !HasOtherAgentClaimedPlanting(world, field, actor) && PlantingStock(world, actor, field, crop) is null);
    }

    private IEnumerable<(string Crop, InventoryLot Seed)> PlannedPlantingStock(IFarmingWorld world, string actor, FarmFieldState field) =>
        new[] { FarmFieldRules.Greens, FarmFieldRules.Grain, FarmFieldRules.Potatoes }
            .OrderBy(crop => field.Crop == crop ? -1 : fields.Count(item => item.HouseholdId == field.HouseholdId &&
                (item.Work?.Crop ?? item.Crop) == crop))
            .Select(crop => (Crop: crop, Seed: PlantingStock(world, actor, field, crop)))
            .Where(item => item.Seed is not null).Select(item => (item.Crop, item.Seed!));

    // Orders need a usable pickup now; autonomous crop claims keep terrain eligibility
    // while a temporary occupant passes and their existing physical route retries.
    internal static InventoryLot? PlantingStock(IFarmingWorld world, string actor, FarmFieldState field, string crop, bool requirePickupRoute = false)
    {
        var kind = FarmFieldRules.PlantingItem(crop);
        var request = new GoodsRequest(GoodsUse.FieldPlanting, actor, new([actor, field.HouseholdId]), GoodsKinds.One(kind),
            ReservationId: field.ReplantingReservationId);
        return world.FindGoods(request).Matches.Select(match => match.Lot)
            .OrderBy(lot => lot.OwnerId == actor ? 0 : field.ReplantingReservationId is { } id &&
                world.Society.Inventory.Reservations.Any(item => item.Id == id && item.LotId == lot.Id && item.State == InventoryReservationState.Reserved) ? 1 : 2)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => lot.OwnerId == actor || world.PickupCarryCapacity(actor, lot, field.Position, 1) >= 1 &&
                (requirePickupRoute ? world.CanReach(actor, world.HouseholdStockPosition(lot), ReachPurpose.FieldWork,
                    WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(lot), 0, SwimmingPolicy.WhenCapable) :
                world.Reach(actor, world.HouseholdStockPosition(lot), ReachPurpose.FieldSeedPickup, 0, 0,
                    SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0));
    }

    internal void ApplyFieldCandidate(IFarmingWorld world, string actor, AgentState state, string candidate, string? orderInstructionId = null)
    {
        var parts = candidate.Split(':');
        if (parts.Length != 5 || !Enum.TryParse<FarmWorkKind>(parts[1], out var kind) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var y)) return;
        var point = new GridPoint(x, y);
        var crop = parts[4] == "-" ? null : parts[4];
        if (kind == FarmWorkKind.Plant)
        {
            var field = fields.SingleOrDefault(item => item.Position == point && item.HouseholdId == world.HouseholdFor(actor));
            if (field is null || crop is null || !FarmFieldRules.IsCrop(crop) ||
                PlantingStock(world, actor, field, crop, requirePickupRoute: orderInstructionId is not null) is not { } seed) return;
            if (seed.OwnerId != actor)
            {
                if (world.PickupCarryCapacity(actor, seed, point, 1) < 1) return;
                var source = world.HouseholdStockPosition(seed);
                var range = WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(seed);
                if (!world.IsWithinInteractionRange(state.Position, source, range))
                {
                    world.MoveToward(actor, state, source, "planting_stock", range);
                    return;
                }
                world.RunInventoryCommand(InventorySystem.ApplyFieldCandidate(field, seed, world, actor));
                if (field.ReplantingReservationId is { } replant && !ActiveFarmReservation(world, replant))
                    SetFarmField(world, field with { ReplantingReservationId = null });
                return;
            }
            if (state.Position != point) { world.MoveToward(actor, state, point, "field_planting"); return; }
            StartFieldWorkCore(world, actor, point, kind, crop, seed.Id, orderInstructionId);
            return;
        }
        if (state.Position != point) { world.MoveToward(actor, state, point, "field_work"); return; }
        StartFieldWorkCore(world, actor, point, kind, orderInstructionId: orderInstructionId);
    }

    internal static ActorCapabilityPolicy FieldPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private static ActionCheck CheckFieldContext(IFarmingWorld world, string actor, FieldChoice choice, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(actor), choice.Encode(), capabilityPolicy ?? FieldPolicy);
        if (!capability.Allowed) return capability;
        if (world.HouseholdFor(actor) is not { } household || world.FarmhouseForHousehold(household) is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household no longer has a Farmhouse");
        if (world.IsConversationBusy(actor) ||
            world.Agents[actor].Project is { Stage: not ("completed" or "cancelled"), RequiresFreshChoice: false })
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their other work comes first");
        return new();
    }

    internal FieldActionPlan? PlanFieldAction(IFarmingWorld world, string actor, FieldChoice choice,
        InventoryLot? selectedSeed = null, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        if (!CheckFieldContext(world, actor, choice, capabilityPolicy).Allowed) return null;
        var household = world.HouseholdFor(actor)!;
        var farmhouse = world.FarmhouseForHousehold(household)!;
        var field = fields.SingleOrDefault(item => item.Position == choice.Position);
        var inventory = world.Society.Inventory;
        var hoe = choice.Kind is FarmWorkKind.Till or FarmWorkKind.Tend
            ? ToolProgressionRules.PlanWork(inventory, actor, ToolFamily.Hoe) : null;
        var sickle = choice.Kind == FarmWorkKind.Harvest
            ? ToolProgressionRules.PlanWork(inventory, actor, ToolFamily.Sickle) : null;
        PlannedFieldSeed? seed = null;
        if (choice.Kind == FarmWorkKind.Plant)
        {
            if (field is null || choice.Crop is null || !FarmFieldRules.IsCrop(choice.Crop) ||
                (selectedSeed ?? PlantingStock(world, actor, field, choice.Crop)) is not { } stock) return null;
            var claim = field.ReplantingReservationId is { } id && inventory.Reservations.Any(item =>
                item.Id == id && item.LotId == stock.Id && item.State == InventoryReservationState.Reserved) ? id : null;
            seed = new(stock.Id, stock.OwnerId, stock.ItemKind, stock.OwnerId == actor ? world.Agents[actor].Position :
                world.HouseholdStockPosition(stock), stock.OwnerId == actor ? 0 : WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(stock), claim);
        }
        PlannedFieldTool? Capture(ToolWorkPlan? work) => work is null ? null :
            new(work, inventory.GetLot(work.ToolLotId).OwnerId, inventory.GetLot(work.ToolLotId).ItemKind);
        return new(actor, choice, household, farmhouse.InstanceId, field, seed, Capture(hoe), Capture(sickle),
            world.SkilledWorkTicks(actor, SkillKind.Farming, FarmFieldRules.WorkTicks(choice.Kind)),
            world.Reach(actor, choice.Position, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain));
    }

    internal ActionCheck CheckFieldPlan(IFarmingWorld world, FieldActionPlan plan, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        var context = CheckFieldContext(world, plan.Actor, plan.Choice, capabilityPolicy);
        if (!context.Allowed) return context;
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || world.FarmhouseForHousehold(plan.HouseholdId)?.InstanceId != plan.FarmhouseId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or Farmhouse has changed");
        if (FarmWorkFor(world, plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already have field work under way");
        var field = fields.SingleOrDefault(item => item.Position == plan.Choice.Position);
        if (plan.Choice.Kind == FarmWorkKind.Till)
        {
            if (field is not null || !FarmableFreeTile(world, plan.Choice.Position) ||
                world.HouseholdLandHeldByOthers(plan.HouseholdId).Contains(plan.Choice.Position))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected land is no longer free for a field");
            if (fields.Count(item => item.HouseholdId == plan.HouseholdId) >= WantedFields(world, plan.HouseholdId))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the household already has enough fields");
        }
        else if (field is null || field.HouseholdId != plan.HouseholdId || field.Work is not null ||
            plan.Field is null || field.Cycle != plan.Field.Cycle || field.Crop != plan.Field.Crop || field.LastWorkedTick != plan.Field.LastWorkedTick ||
            plan.Choice.Kind == FarmWorkKind.Plant && (field.Stage is not (FarmFieldStage.Prepared or FarmFieldStage.Harvested) ||
                !FarmFieldRules.IsCrop(plan.Choice.Crop) || !FarmNeedsFood(world, plan.HouseholdId) || HasOtherAgentClaimedPlanting(world, field, plan.Actor)) ||
            plan.Choice.Kind == FarmWorkKind.Tend && (field.Stage != FarmFieldStage.Growing || field.Tended) ||
            plan.Choice.Kind == FarmWorkKind.Harvest && field.Stage != FarmFieldStage.Ready)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected field no longer needs this work");
        foreach (var tool in new[] { plan.Hoe, plan.Sickle }.OfType<PlannedFieldTool>())
            if (world.RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(tool.OwnerId), GoodsKinds.One(tool.ItemKind)),
                    tool.Work.ToolLotId) is not { Lot: var lot } || !ToolProgressionRules.IsTopLevelCarriedLot(lot, plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected field tool is no longer available");
        if ((plan.Choice.Kind is FarmWorkKind.Till or FarmWorkKind.Tend) && plan.Hoe is null)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "they need a usable carried hoe");
        if (plan.Seed is { } seed)
        {
            var request = new GoodsRequest(GoodsUse.FieldPlanting, plan.Actor, GoodsOwners.One(seed.OwnerId), GoodsKinds.One(seed.ItemKind),
                ReservationId: seed.ReplantingReservationId);
            if (world.RecheckGoods(request, seed.StockId) is not { } match || match.Quantity < 1 ||
                seed.OwnerId != plan.Actor && (world.HouseholdStockPosition(match.Lot) != seed.Position ||
                    WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(match.Lot) != seed.InteractionRange ||
                    world.PickupCarryCapacity(plan.Actor, match.Lot, plan.Choice.Position, 1) < 1) ||
                seed.ReplantingReservationId is { } id && !world.Society.Inventory.Reservations.Any(item =>
                    item.Id == id && item.LotId == seed.StockId && item.State == InventoryReservationState.Reserved))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected planting item is no longer available");
            if (seed.OwnerId != plan.Actor && world.Reach(plan.Actor, seed.Position, ReachPurpose.FieldSeedPickup, 0, 0,
                SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count == 0)
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected planting item is out of reach now");
        }
        return world.Reach(plan.Actor, plan.Choice.Position, ReachPurpose.FieldWork, 0, 0,
            SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0 ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected field is out of reach now");
    }

    internal void ApplyFieldPlan(IFarmingWorld world, FieldActionPlan plan)
    {
        var state = world.Agents[plan.Actor];
        if (plan.Seed is { } seed && seed.OwnerId != plan.Actor)
        {
            if (!world.IsWithinInteractionRange(state.Position, seed.Position, seed.InteractionRange))
            {
                world.MoveToward(plan.Actor, state, seed.Position, "planting_stock", seed.InteractionRange);
                return;
            }
            world.RunInventoryCommand(InventorySystem.ApplyFieldPlan(seed, world, plan));
            if (plan.Field is { ReplantingReservationId: { } claim } field && !ActiveFarmReservation(world, claim))
                SetFarmField(world, fields.Single(item => item.Position == field.Position) with { ReplantingReservationId = null });
            return;
        }
        if (state.Position != plan.Choice.Position)
        {
            world.MoveToward(plan.Actor, state, plan.Choice.Position, plan.Choice.Kind == FarmWorkKind.Plant ? "field_planting" : "field_work");
            return;
        }
        StartFieldWorkCore(world, plan.Actor, plan.Choice.Position, plan.Choice.Kind, plan.Choice.Crop,
            plan.Seed?.StockId, plannedWork: plan);
    }

    // How far from an agent the built-in choice looks for open planting ground.
    private const int PlantingSearchRadius = 10;
    private const string OrchardReplantingPrefix = "orchard-replant:";

    internal static string OrchardReplantingReservationId(string lotId) => DerivedId.Create(OrchardReplantingPrefix + lotId);

    internal static int PlantingSeedQuantity(IFarmingWorld world, InventoryLot lot) => world.PlantingSeedQuantity(lot);

    internal static InventoryLot? CarriedPlantingSeed(IFarmingWorld world, string actor, string kind) =>
        world.FindGoods(new(GoodsUse.TreePlanting, actor, GoodsOwners.One(actor), GoodsKinds.One(kind)))
            .Matches.Select(match => match.Lot).OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static bool IsPlantedTree(MapResource resource) =>
        resource.Id.StartsWith(TreeGrowthRules.PlantedTreeIdPrefix, StringComparison.Ordinal);

    internal static void AddTreePlantingCandidates(IFarmingWorld world, List<CognitionCandidate> candidates, string actor,
        AgentState state)
    {
        var carried = world.HasCarriedOwnItem(actor, TreeGrowthRules.TreeSeedItem);
        var orchardCarried = CarriedPlantingSeed(world, actor, TreeGrowthRules.OrchardSeedItem) is not null;
        var collecting = world.FreeCarryCapacity(actor) > 0;
        var treeAvailable = carried || collecting && world.SharedItem(TreeGrowthRules.TreeSeedItem, actor) is not null;
        var orchardAvailable = orchardCarried || collecting && world.SharedItem(TreeGrowthRules.OrchardSeedItem, actor) is not null;
        // Keep agents without seeds out of the planting-site and stump scans.
        if (!treeAvailable && !orchardAvailable) return;
        var site = PlantingSite(world, actor, state.Position);
        if (orchardAvailable && site is { } orchardSite)
            AddTreeCandidate(world, candidates, actor, TreeChoice.Orchard, "Plant an orchard seed on open ground.", 31, orchardSite);
        if (!treeAvailable) return;
        if (ReplantableTree(world, actor, state.Position) is { } tree)
            AddTreeCandidate(world, candidates, actor, TreeChoice.Replant, "Carry a tree seed to a stump and replant it.", 32, tree.Position, tree);
        if (site is { } plantingSite)
            AddTreeCandidate(world, candidates, actor, TreeChoice.Plant, "Plant a tree seed on open ground outside the Town.", 34, plantingSite);
    }

    private static void AddTreeCandidate(IFarmingWorld world, List<CognitionCandidate> candidates, string actor,
        TreeChoice choice, string description, int priority, GridPoint site, MapResource? stump = null)
    {
        if (PlanTreeAction(world, actor, choice, site, stump) is { } plan && CheckTreePlan(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, priority, stump?.Id) { ActionPlan = plan });
    }

    internal static MapResource? ReplantableTree(IFarmingWorld world, string actor, GridPoint origin)
    {
        var depleted = world.Environment.Ecology.Resources.Where(ecology =>
                ecology.Quantity == 0 && !ecology.IsPlanted)
            .Select(ecology => ecology.Id).ToHashSet(StringComparer.Ordinal);
        return world.Map.Resources
            .Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) && resource.IsRenewable &&
                depleted.Contains(resource.Id) && world.Reach(actor, resource.Position, ReachPurpose.TreePlanting, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0)
            .OrderBy(resource => world.Map.FootDistance(origin, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(resource => world.IsWithinInteractionRange(origin, resource.Position, 1) ||
                world.Reach(actor, resource.Position, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }

    private static void CompleteTreeReplanting(IFarmingWorld world, string actor, MapResource tree, InventoryLot seed)
    {
        var ecology = world.Environment.Ecology.GetResource(tree.Id);
        if (ecology.Quantity != 0 || ecology.IsPlanted) return;
        world.ApplySocietyTransition(checkpoint => SocietyRules.ConsumeInventory(checkpoint,
            actor, seed.Id, 1, "tree_replanting"));
        var day = WorldCalendarRules.FromTick(world.WorldTick, world.Environment.Config).DayIndex;
        var planted = ecology with
        {
            State = EcologyResourceState.Regenerating,
            IsPlanted = true,
            NextRegenerationDay = day + TreeGrowthRules.SaplingGrowthDays,
        };
        world.ReplaceEcologyResource(tree.Id, planted);
        world.SyncEcologyResourceStates();
        world.AppendEvent(new TreeReplantedEventPayload(actor, tree.Id, tree.TreeKind));
    }

    internal static TreePlantingResult PlantTreeCore(IFarmingWorld world, string planterId, string species, string seedLotId, GridPoint destination)
    {
        if (!TreeGrowthRules.IsPlantableSpecies(species))
            return TreePlantingResult.Refused(TreePlantingRefusal.UnsupportedSpecies);
        if (!world.Agents.TryGetValue(planterId, out var planter) || !world.IsAdult(planterId))
            return TreePlantingResult.Refused(TreePlantingRefusal.PlanterUnavailable);
        var seed = world.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == seedLotId);
        if (seed is null)
            return TreePlantingResult.Refused(TreePlantingRefusal.NoSeedLeft);
        if (seed.ItemKind != TreeGrowthRules.SeedItem(species))
            return TreePlantingResult.Refused(TreePlantingRefusal.NotATreeSeed);
        if (seed.OwnerId != planterId || seed.GroundPosition is not null || seed.StorageBuildingId is not null || seed.DeliveryBuildingId is not null)
            return TreePlantingResult.Refused(TreePlantingRefusal.SeedNotOwned);
        if (PlantingSeedQuantity(world, seed) < 1)
            return TreePlantingResult.Refused(TreePlantingRefusal.NoSeedLeft);
        var obstacles = PlantingObstacles(world);
        if (PlantingSiteRefusal(world, destination, obstacles) is { } site)
            return TreePlantingResult.Refused(site);
        if (!world.IsWithinInteractionRange(planter.Position, destination, 1))
            return TreePlantingResult.Refused(TreePlantingRefusal.TooFar);
        if (TreeAreaFull(world, destination))
            return TreePlantingResult.Refused(TreePlantingRefusal.AreaFull);

        if (species == TreeGrowthRules.Orchard)
            foreach (var reservation in world.Society.Inventory.Reservations.Where(item => item.LotId == seed.Id &&
                         item.State is (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) && item.Id == OrchardReplantingReservationId(seed.Id)).ToArray())
                world.RunInventoryCommand(InventorySystem.PlantTreeCore(reservation));
        world.ApplySocietyTransition(checkpoint => SocietyRules.ConsumeInventory(checkpoint,
            planterId, seed.Id, 1, "tree_planting"));
        var tree = new MapResource(TreeGrowthRules.PlantedTreeId(destination), TreeGrowthRules.ResourceKind(species), destination, true,
            species);
        var plantedMap = world.Map with
        {
            Resources = world.Map.Resources.Append(tree).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
        };
        world.SetMap(plantedMap with { ManifestDigest = MapManifestCodec.Digest(plantedMap) });
        var day = WorldCalendarRules.FromTick(world.WorldTick, world.Environment.Config).DayIndex;
        var chunk = world.Environment.Chunks.Single(item =>
            item.Coordinate == ChunkRules.ToChunkCoordinate(destination, item.ChunkSize));
        world.PlantTree(tree, day, chunk);
        world.SyncEcologyResourceStates();
        world.RecordKnowledgeFact(planterId, destination);
        world.AppendEvent(new TreePlantedEventPayload(planterId, tree.Id, species));
        return TreePlantingResult.Success(tree.Id, species);
    }

    /// <summary>
    /// The built-in planting choice: the nearest reachable open tile outside
    /// every Town border, so new trees do not block Town building sites.
    /// </summary>
    internal static GridPoint? PlantingSite(IFarmingWorld world, string actor, GridPoint origin)
    {
        var obstacles = PlantingObstacles(world);
        var townTiles = world.Towns.SelectMany(town => town.BorderTiles ?? []).ToHashSet();
        var sites = new List<GridPoint>();
        for (var dy = -PlantingSearchRadius; dy <= PlantingSearchRadius; dy++)
            for (var dx = -PlantingSearchRadius; dx <= PlantingSearchRadius; dx++)
            {
                var x = origin.X + dx;
                if (world.Map.WrapsEastWest) x = (x % world.Map.Width + world.Map.Width) % world.Map.Width;
                var point = new GridPoint(x, origin.Y + dy);
                if (!world.Map.Contains(point) || townTiles.Contains(point) ||
                    PlantingSiteRefusal(world, point, obstacles) is not null || world.Reach(actor, point, ReachPurpose.TreePlanting, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count == 0)
                    continue;
                sites.Add(point);
            }
        return sites
            .OrderBy(point => world.Map.FootDistance(origin, point))
            .ThenBy(point => point.Y)
            .ThenBy(point => point.X)
            .Where(point => !TreeAreaFull(world, point))
            .Select(point => (GridPoint?)point)
            .FirstOrDefault(point => world.IsWithinInteractionRange(origin, point!.Value, 1) ||
                world.Reach(actor, point.Value, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }

    /// <summary>A tree seed grows into the species of the nearest wood tree, or by climate when none is near.</summary>
    internal static string PlantingSpecies(IFarmingWorld world, GridPoint site) =>
        world.Map.Resources.Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) &&
                world.Map.FootDistance(site, resource.Position) <= PlantingSearchRadius)
            .OrderBy(resource => world.Map.FootDistance(site, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .Select(resource => resource.TreeKind!)
            .FirstOrDefault() ??
        (world.Map.ClimateAt(site) is ClimateZone.Cold or ClimateZone.Polar
            ? TreeGrowthRules.Conifer : TreeGrowthRules.Broadleaf);

    internal static (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) PlantingObstacles(IFarmingWorld world)
    {
        var ground = world.Ground;
        var buildings = GroundOccupancy.Combine(ground.PlantingBuildingFootprints,
            ground.ExpansionSites, ground.ProtectedProjectSites(), ground.MarketSites);
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources, ground.Fields);
        return (buildings, occupied);
    }

    internal static TreePlantingRefusal? PlantingSiteRefusal(IFarmingWorld world, GridPoint point,
        (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) obstacles)
    {
        if (TreeGrowthRules.GroundRefusal(world.Map, point) is { } ground) return ground;
        if (obstacles.Buildings.Contains(point)) return TreePlantingRefusal.Building;
        if (world.RoadAndBridgeTiles().Contains(point)) return TreePlantingRefusal.Road;
        if (obstacles.Occupied.Contains(point)) return TreePlantingRefusal.Occupied;
        return null;
    }

    // Planted trees share the bounded per-chunk resource budget with
    // generated sites, so a crowded area cannot grow without limit.
    internal static bool TreeAreaFull(IFarmingWorld world, GridPoint point)
    {
        var config = world.Environment.Config;
        var chunk = world.Environment.Chunks.FirstOrDefault(item =>
            item.Coordinate == ChunkRules.ToChunkCoordinate(point, item.ChunkSize));
        return chunk is null || chunk.Resources.Count >= config.MaxResourcesPerChunk ||
            world.Environment.Ecology.Resources.Count >= config.MaxResourcesPerChunk * config.MaxChunkCount;
    }

    internal static void ValidatePlantedTrees(IFarmingWorld world)
    {
        var obstacles = PlantingObstacles(world);
        var ecology = world.Environment.Ecology.Resources.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var tree in world.Map.Resources.Where(IsPlantedTree))
        {
            if (!TreeGrowthRules.IsValidPlantedTree(world.Map, tree) || obstacles.Buildings.Contains(tree.Position) ||
                world.RoadAndBridgeTiles().Contains(tree.Position) ||
                world.Map.MapObjects.Any(item => item.Position == tree.Position) ||
                !ecology.TryGetValue(tree.Id, out var growth) || growth.Kind != tree.Kind ||
                growth.Position != tree.Position || !growth.IsRenewable)
                throw new InvalidDataException($"The planted tree '{tree.Id}' is invalid.");
        }
    }

    internal static ActorCapabilityPolicy TreePolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal static TreeActionPlan? PlanTreeAction(IFarmingWorld world, string actor, TreeChoice choice,
        GridPoint? selectedSite = null, MapResource? selectedStump = null, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        if (!ActorCapabilities.Check(world.CapabilitiesFor(actor), TreeActionPlan.Candidate(choice), capabilityPolicy ?? TreePolicy).Allowed) return null;
        var origin = world.Agents[actor].Position;
        var stump = choice == TreeChoice.Replant ? selectedStump ?? ReplantableTree(world, actor, origin) : null;
        var position = choice == TreeChoice.Replant ? stump?.Position : selectedSite ?? PlantingSite(world, actor, origin);
        if (position is not { } site) return null;
        var kind = choice == TreeChoice.Orchard ? TreeGrowthRules.OrchardSeedItem : TreeGrowthRules.TreeSeedItem;
        var seed = CarriedPlantingSeed(world, actor, kind) ?? world.SharedItem(kind, actor, site, 1);
        if (seed is null) return null;
        var claim = seed.OwnerId == actor ? OrchardClaim(world, seed) : null;
        return new(actor, choice, site, stump?.TreeKind ?? (choice == TreeChoice.Orchard ? TreeGrowthRules.Orchard : PlantingSpecies(world, site)),
            stump?.Id, new(seed.Id, seed.OwnerId, kind, seed.OwnerId == actor ? origin : world.HouseholdStockPosition(seed),
                seed.OwnerId == actor ? 0 : WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(seed)), claim,
            world.Reach(actor, site, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable));
    }

    private static PlannedOrchardClaim? OrchardClaim(IFarmingWorld world, InventoryLot seed)
    {
        var claim = WorldRuntime.OrchardPlantingClaim(InventoryIndex.For(world.Society.Inventory), seed, world.WorldTick);
        return claim is null ? null : new(claim.Id, claim.Quantity);
    }

    internal static ActionCheck CheckTreePlan(IFarmingWorld world, TreeActionPlan plan, ActorCapabilityPolicy? capabilityPolicy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, capabilityPolicy ?? TreePolicy);
        if (!capability.Allowed) return capability;
        var seed = plan.Seed;
        var use = seed.OwnerId == plan.Actor ? GoodsUse.TreePlanting : GoodsUse.Collect;
        if (world.RecheckGoods(new(use, plan.Actor, GoodsOwners.One(seed.OwnerId), GoodsKinds.One(seed.ItemKind)), seed.StockId) is not { Lot: var lot } ||
            seed.OwnerId == plan.Actor && OrchardClaim(world, lot) != plan.Claim ||
            seed.OwnerId != plan.Actor && (lot.ContainerLotId is not null || lot.CarrierId is not null ||
                world.HouseholdStockPosition(lot) != seed.Position || WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(lot) != seed.InteractionRange ||
                world.PickupCarryCapacity(plan.Actor, lot, plan.Position, 1) < 1))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected planting seed is no longer available");
        if (plan.Choice == TreeChoice.Replant)
        {
            var stump = world.Map.Resources.SingleOrDefault(item => item.Id == plan.StumpId);
            var growth = world.Environment.Ecology.Resources.SingleOrDefault(item => item.Id == plan.StumpId);
            if (stump is null || stump.Position != plan.Position || stump.TreeKind != plan.Species || !stump.IsRenewable ||
                growth is null || growth.Quantity != 0 || growth.IsPlanted)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected stump no longer needs replanting");
        }
        else if (world.Towns.Any(town => (town.BorderTiles ?? []).Contains(plan.Position)) ||
            PlantingSiteRefusal(world, plan.Position, PlantingObstacles(world)) is not null || TreeAreaFull(world, plan.Position))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected planting ground is no longer available");
        return world.Reach(plan.Actor, plan.Position, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected planting site is out of reach now");
    }

    internal static void ApplyTreePlan(IFarmingWorld world, TreeActionPlan plan)
    {
        if (plan.Seed.OwnerId != plan.Actor)
        {
            world.PickUpEquipment(plan.Actor, plan.Seed);
            return;
        }
        var state = world.Agents[plan.Actor];
        if (!world.IsWithinInteractionRange(state.Position, plan.Position, 1))
        {
            world.MoveToward(plan.Actor, state, plan.Position, plan.Choice == TreeChoice.Replant ? "replant_tree" : "plant_tree", 1);
            return;
        }
        if (plan.Choice == TreeChoice.Replant)
            CompleteTreeReplanting(world, plan.Actor, world.Map.GetResource(plan.StumpId!), world.Society.Inventory.GetLot(plan.Seed.StockId));
        else PlantTreeCore(world, plan.Actor, plan.Species, plan.Seed.StockId, plan.Position);
    }
}
