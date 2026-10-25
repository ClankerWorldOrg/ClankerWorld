using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    /// <summary>Start physical work at one tile. Refusal leaves the field and inventory untouched.</summary>
    internal FarmWorkResult StartFieldWorkCore(IFarmingWorld world, string workerId, GridPoint position, FarmWorkKind kind,
        string? crop = null, string? seedLotId = null, string? orderInstructionId = null, FieldActionPlan? plannedWork = null)
    {
        if (!Enum.IsDefined(kind) || !world.Agents.TryGetValue(workerId, out var worker) || !world.IsAdult(workerId) ||
            world.Society.GetAgent(workerId).HouseholdId is not { } householdId ||
            world.FarmhouseForHousehold(householdId) is null)
            return new(false, "An adult from a household with a Farmhouse must do the work.");
        if (worker.Position != position) return new(false, "The worker must stand on the field tile.");
        var hoe = plannedWork is not null ? plannedWork.Hoe?.Work : kind is FarmWorkKind.Till or FarmWorkKind.Tend
            ? ToolProgressionRules.PlanWork(world.Society.Inventory, workerId, ToolFamily.Hoe) : null;
        if ((kind is FarmWorkKind.Till or FarmWorkKind.Tend) && hoe is null)
            return new(false, "The worker needs a usable carried hoe.");
        var sickle = plannedWork is not null ? plannedWork.Sickle?.Work : kind == FarmWorkKind.Harvest
            ? ToolProgressionRules.PlanWork(world.Society.Inventory, workerId, ToolFamily.Sickle) : null;
        if (world.NeedsUrgentFood(worker) || world.NeedsUrgentWarmth(worker) || world.IsConversationBusy(workerId) ||
            (orderInstructionId is null && worker.Project is { Stage: not ("completed" or "cancelled") } project && !project.RequiresFreshChoice) ||
            FarmWorkFor(world, workerId) is not null)
            return new(false, "The worker must finish other work or meet urgent needs first.");
        var field = fields.SingleOrDefault(item => item.Position == position);
        if (kind == FarmWorkKind.Till)
        {
            if (field is not null || !FarmableFreeTile(world, position))
                return new(false, "This land cannot be tilled: choose free farmable land.");
            if (world.HouseholdLandHeldByOthers(householdId).Contains(position))
                return new(false, "This land is held or requested by another household.");
            field = new(position, householdId, FarmFieldStage.Preparing);
        }
        else if (field is null || field.HouseholdId != householdId || field.Work is not null)
            return new(false, "This household does not have a free field here.");
        if (kind == FarmWorkKind.Plant &&
            (field.Stage is not (FarmFieldStage.Prepared or FarmFieldStage.Harvested) || !FarmFieldRules.IsCrop(crop)))
            return new(false, "Plant a supported crop on prepared or harvested soil.");
        if (kind == FarmWorkKind.Tend && (field.Stage != FarmFieldStage.Growing || field.Tended))
            return new(false, "This crop does not need tending now.");
        if (kind == FarmWorkKind.Harvest && field.Stage != FarmFieldStage.Ready)
            return new(false, "The crop is not ready to harvest.");
        string? reservationId = null;
        if (kind == FarmWorkKind.Plant)
        {
            var seed = world.Society.Inventory.Lots.SingleOrDefault(lot => lot.Id == seedLotId);
            if (seed is null || seed.OwnerId != workerId || seed.GroundPosition is not null ||
                seed.StorageBuildingId is not null || seed.DeliveryBuildingId is not null ||
                seed.ItemKind != FarmFieldRules.PlantingItem(crop!) || world.AvailableLotQuantity(seed) < 1)
                return new(false, "Carry one of your own usable planting items to the field.");
            reservationId = WorldRuntime.FarmingSharedRules.FieldPlantingReservationId(position, field.Cycle, world.WorldTick, workerId);
            // This seed belongs to active work, including illness and conversation
            // pauses. Completion consumes it; every work cancellation releases it.
            world.RunInventoryCommand(InventorySystem.StartFieldWorkCore(reservationId, workerId, seed));
        }
        var hoeLotId = hoe is null ? null : IsolateFieldToolForWork(world, workerId, position, hoe.ToolLotId);
        var sickleLotId = sickle is null ? null : IsolateFieldToolForWork(world, workerId, position, sickle.ToolLotId);
        SetFarmField(world, field with
        {
            Work = new(workerId, kind, plannedWork?.WorkTicks ?? world.SkilledWorkTicks(workerId, SkillKind.Farming, FarmFieldRules.WorkTicks(kind)), world.WorldTick, Identity.ParseOptional(reservationId), crop,
                hoeLotId, sickleLotId, orderInstructionId),
            LastWorkedTick = world.WorldTick,
        });
        world.AppendEvent(new FieldWorkStartedEventPayload(workerId, FarmFieldRules.FieldId(position), Convert.ToString(kind, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
        return new(true, "Field work started.");
    }

    internal FarmFieldState? FarmWorkFor(IFarmingWorld world, string workerId) => fields.SingleOrDefault(field => field.Work?.WorkerId == workerId);

    internal static string IsolateFieldToolForWork(IFarmingWorld world, string workerId, GridPoint position, string lotId)
    {
        var inventory = world.Society.Inventory;
        var lot = inventory.GetLot(lotId);
        if (lot.Quantity == 1) return lot.Id;
        var isolatedId = WorldRuntime.FarmingSharedRules.FieldToolLotId(lot.Id, position, world.WorldTick, workerId);
        world.RunInventoryCommand(InventorySystem.IsolateFieldToolForWork(lot, isolatedId, workerId));
        return isolatedId;
    }

    internal bool FarmableFreeTile(IFarmingWorld world, GridPoint position, LandFertility? queryFertility = null)
    {
        if (!(queryFertility ?? Fertility(world.Map)).CanFarm(position) || fields.Any(field => field.Position == position) || world.TownProjectProtectedSites().Contains(position) ||
            world.MarketSiteTiles().Contains(position) ||
            world.RoadAndBridgeTiles().Contains(position) || world.Map.MapObjects.Any(item => item.Position == position) ||
            (world.Simulation.BuildingExpansions ?? []).Any(job => (job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused) && WorldRuntime.FarmingSharedRules.ExpansionTiles(job).Contains(position)) ||
            world.Map.Resources.Any(item => item.Position == position)) return false;
        return !world.Simulation.Buildings.Any(building => WorldContentSimulationRules.Footprint(
            world.Content.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building).Contains(position));
    }

    internal void SetFarmField(IFarmingWorld world, FarmFieldState field)
    {
        Commit(new(fields.Where(item => item.Position != field.Position).Append(field)
            .OrderBy(item => item.Position.Y).ThenBy(item => item.Position.X)));
    }

    internal void MaintainFarmFields(IFarmingWorld world)
    {
        foreach (var field in fields.ToArray())
        {
            if (field.Work is { } work && (!world.Agents.TryGetValue(work.WorkerId, out var worker) ||
                !world.IsAdult(work.WorkerId) || world.HouseholdFor(work.WorkerId) != field.HouseholdId || world.FarmhouseForHousehold(field.HouseholdId) is null ||
                worker.Position != field.Position || !FieldWorkToolsAvailable(world, work.WorkerId, work) ||
                world.NeedsUrgentFood(worker) || world.NeedsUrgentWarmth(worker) ||
                work.SeedReservationId is { } id && !ActiveFarmReservation(world, id)))
            {
                CancelFarmWork(world, field);
                continue;
            }
            if (field.Stage == FarmFieldStage.Planted && world.WorldTick > field.PlantedTick)
                SetFarmField(world, field with { Stage = FarmFieldStage.Growing });
            else if (field.Stage == FarmFieldStage.Growing && field.Tended && world.WorldTick >= field.ReadyTick)
            {
                SetFarmField(world, field with { Stage = FarmFieldStage.Ready });
                world.AppendEvent(new FieldReadyEventPayload(null, FarmFieldRules.FieldId(field.Position)));
            }
            if (field.ReplantingReservationId is { } reserve && !ActiveFarmReservation(world, reserve))
                SetFarmField(world, fields.Single(item => item.Position == field.Position) with { ReplantingReservationId = null });
            var current = fields.Single(item => item.Position == field.Position);
            if (current.Work is null && world.WorldTick - current.LastWorkedTick >= FarmFieldRules.IdleTicksBeforeGrass(world.Environment.Config))
                ReturnFieldToGrass(world, current);
        }
    }

    /// <summary>
    /// Removes a field nobody has worked for a full season. Any crop still on
    /// it is lost and the seed kept back for replanting is freed; harvested
    /// goods already on the ground and the land's fertility stay.
    /// </summary>
    internal void ReturnFieldToGrass(IFarmingWorld world, FarmFieldState field)
    {
        if (field.ReplantingReservationId is { } reserve && ActiveFarmReservation(world, reserve))
            world.RunInventoryCommand(InventorySystem.ReturnFieldToGrass(reserve));
        RemoveField(field.Position);
        world.AppendEvent(new FieldReturnedToGrassEventPayload(null, FarmFieldRules.FieldId(field.Position), field.HouseholdId));
    }

    internal static bool ActiveFarmReservation(IFarmingWorld world, string id) => world.Society.Inventory.Reservations.Any(reservation =>
        reservation.Id == id && reservation.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed);

    internal void CancelFarmWork(IFarmingWorld world, FarmFieldState field)
    {
        if (field.Work?.SeedReservationId is { } reservationId && ActiveFarmReservation(world, reservationId))
            world.RunInventoryCommand(InventorySystem.CancelFarmWork(reservationId));
        if (field.Stage == FarmFieldStage.Preparing) RemoveField(field.Position);
        else SetFarmField(world, field with { Work = null });
        world.AppendEvent(new FieldWorkInterruptedEventPayload(null, FarmFieldRules.FieldId(field.Position)));
    }

    internal void CancelFieldWorkForUnavailableWorkers(IFarmingWorld world)
    {
        var active = world.Society.Agents
            .Where(person => person.Status == SocietyAgentStatus.Active)
            .Select(person => person.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var field in fields.Where(field => field.Work is { } work &&
                     (!world.Agents.ContainsKey(work.WorkerId) || !active.Contains(work.WorkerId))).ToArray())
            CancelFarmWork(world, field);
    }

    internal bool ContinueFarmWork(IFarmingWorld world, string workerId) => ContinueFarmWork(world, workerId, out _);

    internal bool ContinueFarmWork(IFarmingWorld world, string workerId, out FarmFieldState? completed)
    {
        completed = null;
        if (FarmWorkFor(world, workerId) is not { Work: { } work } field) return false;
        world.RecordAttemptedAction(workerId, FarmCandidate(work.Kind, field.Position, work.Crop));
        if (work.LastWorkedTick == world.WorldTick) return true;
        if (!world.Agents.TryGetValue(workerId, out var worker) || !world.IsAdult(workerId) || world.HouseholdFor(workerId) != field.HouseholdId ||
            world.FarmhouseForHousehold(field.HouseholdId) is null || worker.Position != field.Position ||
            world.NeedsUrgentFood(worker) || world.NeedsUrgentWarmth(worker) || !FieldWorkToolsAvailable(world, workerId, work))
        {
            CancelFarmWork(world, field);
            return false;
        }
        if (world.IsConversationBusy(workerId)) return true;
        if (!IllnessRules.AllowsWork(workerId, world.WorldTick,
                worker.Survival?.IllnessBasisPoints ?? 0)) return true;
        var toolPlans = FieldWorkToolPlans(world, workerId, work);
        var hoe = toolPlans.FirstOrDefault(plan => ToolProgressionRules.Find(
            world.Society.Inventory.GetLot(plan.ToolLotId).ItemKind)?.Family == ToolFamily.Hoe);
        var sickle = toolPlans.FirstOrDefault(plan => ToolProgressionRules.Find(
            world.Society.Inventory.GetLot(plan.ToolLotId).ItemKind)?.Family == ToolFamily.Sickle);
        var progress = work.Kind switch
        {
            FarmWorkKind.Till or FarmWorkKind.Tend => hoe?.WorkUnits ?? 1,
            FarmWorkKind.Harvest => sickle?.WorkUnits ?? 1,
            _ => 1,
        };
        work = work with { RemainingTicks = Math.Max(0, work.RemainingTicks - progress), LastWorkedTick = world.WorldTick };
        if (work.RemainingTicks > 0)
        {
            world.ApplyToolWork(workerId, toolPlans.ToArray());
            if (FieldWorkToolsAvailable(world, workerId, work))
                SetFarmField(world, field with { Work = work, LastWorkedTick = world.WorldTick });
            else
                CancelFarmWork(world, field with { Work = work, LastWorkedTick = world.WorldTick });
            return true;
        }
        switch (work.Kind)
        {
            case FarmWorkKind.Till:
                world.ApplyToolWork(workerId, toolPlans.ToArray());
                SetFarmField(world, field with { Stage = FarmFieldStage.Prepared, Work = null });
                break;
            case FarmWorkKind.Plant:
                world.RunInventoryCommand(InventorySystem.ContinueFarmWork(field, world, work));
                SetFarmField(world, field with
                {
                    Stage = FarmFieldStage.Planted,
                    Crop = work.Crop,
                    PlantedTick = world.WorldTick,
                    ReadyTick = checked(world.WorldTick + FarmFieldRules.GrowthTicks(world.Environment.Config.TicksPerDay, Fertility(world.Map).At(field.Position))),
                    Tended = false,
                    Work = null,
                    ReplantingReservationId = null
                });
                break;
            case FarmWorkKind.Tend:
                world.ApplyToolWork(workerId, toolPlans.ToArray());
                SetFarmField(world, field with { Tended = true, Work = null });
                break;
            case FarmWorkKind.Harvest:
                CompleteFieldHarvest(world, field, toolPlans);
                break;
        }
        if (fields.SingleOrDefault(item => item.Position == field.Position) is { } finished)
            SetFarmField(world, finished with { LastWorkedTick = world.WorldTick });
        completed = field with { Work = work };
        world.CreditCompletedWork(workerId, "farming");
        world.AppendEvent(WorldEvents.FieldWork(work.Kind, workerId, FarmFieldRules.FieldId(field.Position), work.Kind == FarmWorkKind.Plant ? work.Crop : field.Crop));
        return true;
    }

    internal static bool FieldWorkToolsAvailable(IFarmingWorld world, string workerId, FarmFieldWork work)
    {
        if (work.Kind is FarmWorkKind.Till or FarmWorkKind.Tend)
            return ToolProgressionRules.PlanWorkForLot(world.Society.Inventory, workerId,
                ToolFamily.Hoe, work.HoeLotId) is not null;
        if (work.HoeLotId is not null) return false;
        if (work.Kind == FarmWorkKind.Harvest)
            return work.SickleLotId is null || ToolProgressionRules.PlanWorkForLot(world.Society.Inventory,
                workerId, ToolFamily.Sickle, work.SickleLotId) is not null;
        return work.SickleLotId is null;
    }

    internal static List<ToolWorkPlan> FieldWorkToolPlans(IFarmingWorld world, string workerId, FarmFieldWork work)
    {
        var inventory = world.Society.Inventory;
        var plans = new List<ToolWorkPlan>(2);
        if (work.HoeLotId is { } hoeId)
            plans.Add(ToolProgressionRules.PlanWorkForLot(inventory, workerId, ToolFamily.Hoe, hoeId)!);
        if (work.SickleLotId is { } sickleId)
            plans.Add(ToolProgressionRules.PlanWorkForLot(inventory, workerId, ToolFamily.Sickle, sickleId)!);
        return plans;
    }

    internal void CompleteFieldHarvest(IFarmingWorld world, FarmFieldState field, IReadOnlyList<ToolWorkPlan> toolPlans)
    {
        var cycle = checked(field.Cycle + 1);
        // Retilling starts a new field, but goods and released reservations
        // from the old field remain. Never reuse one of their identities.
        var previousInventory = world.Society.Inventory;
        while (previousInventory.Lots.Any(lot => lot.Id == WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, "crop") || lot.Id == WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, "seed")) ||
            previousInventory.Reservations.Any(reservation => reservation.Id == WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, "replant")))
        {
            cycle = checked(cycle + 1);
        }
        var crop = field.Crop!;
        var prefix = $"{FarmFieldRules.FieldId(field.Position)}:harvest:{cycle}";
        var plantingItem = FarmFieldRules.PlantingItem(crop);
        var plantingLotId = WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, plantingItem == crop ? "crop" : "seed");
        var replantId = WorldRuntime.FarmingSharedRules.FieldHarvestId(field.Position, cycle, "replant");
        var quantity = FarmFieldRules.HarvestQuantity(crop, Fertility(world.Map).At(field.Position));
        var weather = world.WeatherAt(field.Position);
        var moisture = WeatherRules.SoilMoistureAt(world.Environment, field.Position, world.Map.Height,
            WeatherRules.RegionClimate(world.Map, field.Position));
        if (weather == WeatherKind.Snow) quantity = Math.Max(1, quantity / 2);
        else if (weather == WeatherKind.Storm || moisture < 15) quantity = Math.Max(1, quantity * 3 / 4);
        else if (moisture >= 50) quantity += Math.Max(1, quantity / 4);
        world.RunInventoryCommand(InventorySystem.CompleteFieldHarvest(field, cycle, crop, quantity, world, plantingItem, plantingLotId, replantId, toolPlans));
        SetFarmField(world, field with { Stage = FarmFieldStage.Harvested, Cycle = cycle, Work = null, ReplantingReservationId = Identity.Parse(replantId) });
        if (weather is WeatherKind.Snow or WeatherKind.Storm)
            world.AppendEvent(new CropWeatherLossEventPayload(null, FarmFieldRules.FieldId(field.Position), cycle, weather.ToString().ToLowerInvariant()));
        else if (moisture < 15 || moisture >= 50)
            world.AppendEvent(new CropMoistureEffectEventPayload(null, FarmFieldRules.FieldId(field.Position), cycle, moisture < 15 ? "dry" : "wet", moisture));
    }

    internal static void ValidateFarmFields(FarmFieldState[] fields, WorldMap map, string seed,
        SocietyCheckpoint society, WorldContentSimulationState simulation, DeclarativeWorldContentState content,
        IReadOnlyList<GridPoint> roads)
    {
        ValidateLocalFields(fields);
        if (fields.Length == 0) return;
        var fertility = new LandFertility(map, seed);
        var ground = new GroundOccupancy(map, content, simulation, roads: roads);
        var occupied = GroundOccupancy.Combine(ground.Resources, ground.MapObjects,
            ground.Roads, ground.BuildingFootprints, ground.ExpansionSites);
        foreach (var field in fields)
        {
            if (!fertility.CanFarm(field.Position) || occupied.Contains(field.Position) ||
                !society.Households.Any(household => household.Id == field.HouseholdId) || field.LastWorkedTick > society.WorldTick ||
                field.Stage is not (FarmFieldStage.Preparing or FarmFieldStage.Prepared) && field.PlantedTick > society.WorldTick ||
                field.Stage is FarmFieldStage.Ready or FarmFieldStage.Harvested && field.ReadyTick > society.WorldTick)
                throw new InvalidDataException("A field has invalid land, ownership or crop state.");
            if (field.Work is { } work)
            {
                var worker = society.Agents.SingleOrDefault(person => person.Id == work.WorkerId);
                var workToolsValid = worker is not null && (work.Kind is FarmWorkKind.Till or FarmWorkKind.Tend
                    ? ToolProgressionRules.PlanWorkForLot(society.Inventory, work.WorkerId, ToolFamily.Hoe, work.HoeLotId) is not null
                    : work.Kind != FarmWorkKind.Harvest || work.SickleLotId is null ||
                        ToolProgressionRules.PlanWorkForLot(society.Inventory, work.WorkerId, ToolFamily.Sickle, work.SickleLotId) is not null);
                if (worker is null || worker.Status != SocietyAgentStatus.Active || !LifeStageRules.IsAdult(worker.AgeBand) ||
                    worker.HouseholdId != field.HouseholdId || work.LastWorkedTick > society.WorldTick || !workToolsValid)
                    throw new InvalidDataException("A field has invalid work in progress.");
                if (work.Kind == FarmWorkKind.Plant)
                {
                    var reservation = society.Inventory.Reservations.SingleOrDefault(item => item.Id == work.SeedReservationId);
                    var lot = society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation?.LotId);
                    if (reservation is not { State: InventoryReservationState.Reserved, Quantity: 1, Purpose: "field_planting" } ||
                        reservation.OwnerId != work.WorkerId || reservation.ExpiryTick < society.WorldTick ||
                        lot is null || lot.OwnerId != work.WorkerId || lot.ItemKind != FarmFieldRules.PlantingItem(work.Crop!) ||
                        lot.GroundPosition is not null || lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null)
                        throw new InvalidDataException("Field planting needs its worker's actual reserved seed.");
                }
            }
            if (field.ReplantingReservationId is { } replantId)
            {
                var reservation = society.Inventory.Reservations.SingleOrDefault(item => item.Id == replantId);
                var lot = society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation?.LotId);
                if (reservation is not { State: InventoryReservationState.Reserved, Quantity: 1, Purpose: "field_replanting" } ||
                    reservation.OwnerId != field.HouseholdId || lot is null || lot.OwnerId != field.HouseholdId ||
                    lot.ItemKind != FarmFieldRules.PlantingItem(field.Crop!))
                    throw new InvalidDataException("A field needs its own usable replanting reserve.");
            }
        }
    }

    private void Commit(FarmingState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalFields(next.Fields);
        State = next;
    }

    internal static void ValidateLocalFields(IReadOnlyList<FarmFieldState> fields)
    {
        if (fields.Any(field => field is null))
            throw new InvalidDataException("A saved field record is missing.");
        if (fields.Select(field => field.Position).Distinct().Count() != fields.Count ||
            !fields.SequenceEqual(fields.OrderBy(field => field.Position.Y).ThenBy(field => field.Position.X)) ||
            fields.Where(field => field.Work is not null).GroupBy(field => field.Work!.WorkerId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Fields must have distinct ordered tiles and one work site per worker.");
        foreach (var field in fields)
        {
            if (!Enum.IsDefined(field.Stage) || field.Cycle < 0 || field.LastWorkedTick < 0 ||
                field.Stage is FarmFieldStage.Preparing or FarmFieldStage.Prepared && field.Crop is not null ||
                field.Stage is not (FarmFieldStage.Preparing or FarmFieldStage.Prepared) &&
                    (!FarmFieldRules.IsCrop(field.Crop) || field.PlantedTick < 0 || field.ReadyTick <= field.PlantedTick) ||
                field.Stage is FarmFieldStage.Ready or FarmFieldStage.Harvested && !field.Tended ||
                field.Stage == FarmFieldStage.Preparing && field.Work?.Kind != FarmWorkKind.Till)
                throw new InvalidDataException("A field has invalid land, ownership or crop state.");
            if (field.Work is { } work && (work.OrderInstructionId is { } orderId &&
                !IdentityRules.Reference(orderId, DerivedId.MaximumLength) ||
                !Enum.IsDefined(work.Kind) || work.LastWorkedTick < 0 || work.LastWorkedTick != field.LastWorkedTick ||
                work.RemainingTicks < 1 || work.RemainingTicks > FarmFieldRules.WorkTicks(work.Kind) ||
                work.Kind == FarmWorkKind.Till && field.Stage != FarmFieldStage.Preparing ||
                work.Kind == FarmWorkKind.Tend && (field.Stage != FarmFieldStage.Growing || field.Tended) ||
                work.Kind == FarmWorkKind.Harvest && field.Stage != FarmFieldStage.Ready ||
                work.Kind == FarmWorkKind.Plant && (field.Stage is not (FarmFieldStage.Prepared or FarmFieldStage.Harvested) || !FarmFieldRules.IsCrop(work.Crop)) ||
                work.Kind != FarmWorkKind.Plant && (work.Crop is not null || work.SeedReservationId is not null) ||
                work.Kind is not (FarmWorkKind.Till or FarmWorkKind.Tend) && work.HoeLotId is not null ||
                work.Kind != FarmWorkKind.Harvest && work.SickleLotId is not null))
                throw new InvalidDataException("A field has invalid work in progress.");
            if (field.ReplantingReservationId is not null && field.Crop is null)
                throw new InvalidDataException("A field needs its own usable replanting reserve.");
        }
    }

    private readonly Derived<WorldMap, LandFertility> fertility;

    internal FarmingSystem(FarmingState state, string seed)
        : this(state, new Derived<WorldMap, LandFertility>(snapshot => new LandFertility(snapshot, seed), (left, right) => left.HasSameValues(right)))
    { }

    private FarmingSystem(FarmingState state, Derived<WorldMap, LandFertility> fertility)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalFields(state.Fields);
        State = state;
        this.fertility = fertility;
    }

    internal FarmingState State { get; private set; }
    private IReadOnlyList<FarmFieldState> fields => State.Fields;

    internal FarmingSystem Fork() => new(State, fertility);
    internal LandFertility Fertility(WorldMap map) => fertility.Get(map);

    private void RemoveField(GridPoint position) => Commit(new(fields.Where(field => field.Position != position)));
}
