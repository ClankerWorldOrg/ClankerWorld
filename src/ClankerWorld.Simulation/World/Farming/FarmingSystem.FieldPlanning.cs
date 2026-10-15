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

    internal static bool HasOtherAgentClaimedPlanting(IFarmingWorld world, FarmFieldState field, string actor)
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

    internal static bool FarmNeedsFood(IFarmingWorld world, string householdId) => FarmFoodDemand(world, householdId).MissingMeals > 0;

    internal static (int MissingMeals, int FarmingHouseholds) FarmFoodDemand(IFarmingWorld world, string householdId)
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
        return ((int)Math.Max(0, residents.Length * FarmFieldRules.MealsPerPersonPerDay * 2 - stock),
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

    internal IEnumerable<string> PlantableCrops(IFarmingWorld world, string actor, FarmFieldState field) =>
        PlannedPlantingStock(world, actor, field).Select(item => item.Crop);

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
                world.ApplyInventoryTransition(inventory =>
                {
                    if (field.ReplantingReservationId is { } id && inventory.Reservations.Any(item =>
                        item.Id == id && item.LotId == seed.Id && item.State == InventoryReservationState.Reserved))
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "planting_stock_collected");
                    return InventoryAuthority.Transfer(inventory, $"field-seed-pickup:{world.WorldTick}:{actor}",
                        field.HouseholdId, actor, seed.Id, 1, "field_seed_picked_up", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                });
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
}
