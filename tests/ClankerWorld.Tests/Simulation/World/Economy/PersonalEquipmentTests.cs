using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class PersonalEquipmentTests
{
    private const string Alpha = "household:first";

    [Fact]
    public void CapacityCountsDeliveredCargoOnceAndDoesNotUseAStoredOrBrokenSack()
    {
        var inventory = InventoryAuthority.CreateGenesis([
            new("sack", "sack", "person", 1, 10_000, 10_000, 0), new("coat", "padded_coat", "person", 1, 10_000, 10_000, 0),
            new("wood", "wood", "person", 8, 10_000, 10_000, 0, DeliveryBuildingId: "house"),
            new("pot", "jug", "person", 1, 10_000, 10_000, 0), new("water", "water", "person", 3, 10_000, 10_000, 0),
            new("ground-stock", "grain", "person", 30, 10_000, 10_000, 0, GroundPosition: new(2, 3)),
        ]);
        var equipment = new PersonalEquipment("coat", "sack");
        Assert.Equal(12, PersonalEquipmentRules.CarriedQuantity(inventory, "person", equipment));
        Assert.Equal(12, PersonalEquipmentRules.FreeCapacity(inventory, "person", equipment));
        var broken = InventoryAuthority.WearSingleUnit(inventory, "sack", 10_000);
        Assert.Equal(8, PersonalEquipmentRules.Capacity(broken, "person", equipment));
        Assert.Equal(12, PersonalEquipmentRules.CarriedQuantity(broken, "person", equipment));
        Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(broken, "person", equipment));
        var stored = InventoryAuthority.Transfer(inventory, "store", "person", Alpha, "sack", 1, "store", "house");
        Assert.Equal(8, PersonalEquipmentRules.Capacity(stored, "person", equipment));
        Assert.Equal(12, PersonalEquipmentRules.CarriedQuantity(stored, "person", equipment));
    }

    [Fact]
    public void CoatsAndCloaksProtectAgainstTheirWeatherAndWearReducesProtection()
    {
        var basic = new InventoryLot("basic", "clothing", "person", 1, 10_000, 10_000, 0);
        var coat = new InventoryLot("coat", "padded_coat", "person", 1, 10_000, 10_000, 0);
        var cloak = new InventoryLot("cloak", "rain_cloak", "person", 1, 10_000, 10_000, 0);
        Assert.True(PersonalEquipmentRules.Protection(coat, WeatherKind.Snow) > PersonalEquipmentRules.Protection(basic, WeatherKind.Snow));
        Assert.True(PersonalEquipmentRules.Protection(cloak, WeatherKind.Storm) > PersonalEquipmentRules.Protection(basic, WeatherKind.Storm));
        Assert.True(PersonalEquipmentRules.Protection(coat, WeatherKind.Snow) > PersonalEquipmentRules.Protection(coat with { ConditionBasisPoints = 2_000 }, WeatherKind.Snow));
        Assert.Equal(0, PersonalEquipmentRules.Protection(cloak with { ConditionBasisPoints = 0 }, WeatherKind.Rain));
    }

    [Fact]
    public async Task FiberBecomesRopeAndAnEquippedBasketThroughHouseWorkAcrossReload()
    {
        using var generated = NormalPathWorld.CreateGenerated("equipment-basket-loop", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var house = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "basket-fiber", "fiber", Alpha, 6, storageBuildingId: house.InstanceId);
        state = WithInventory(state, inventory);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == actor
            ? item with { FullnessBasisPoints = 9_000 } : item).ToArray()
        };
        var rope = state.WorldContent!.Recipes.Single(item => item.LocalId == "twist-rope");
        var basket = state.WorldContent.Recipes.Single(item => item.LocalId == "weave-basket");
        string[] choices = ["equip_carry_aid", "build:recipe:" + basket.CanonicalId, "build:recipe:" + rope.CanonicalId];
        var actorProvider = new Choices(choices);
        IDecisionProvider Provider(string id) => id == actor ? actorProvider : new Choices([]);
        var world = WorldRuntime.Restore(state, Provider);
        try
        {
            var reopened = false;
            for (var tick = 0; tick < 300 && world.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.CarryAidLotId is null; tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                if (!reopened && world.WorldSimulation.ProductionJobs.Any(job => job.RecipeId == basket.CanonicalId && job.State == WorldProductionJobState.Running))
                {
                    var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
                    world.Dispose();
                    world = WorldRuntime.Restore(saved, Provider);
                    reopened = true;
                }
            }
            Assert.True(reopened, "Jobs: " + string.Join(", ", world.WorldSimulation.ProductionJobs.Select(job => job.RecipeId + ":" + job.State)) +
                " Offers: " + string.Join("; ", actorProvider.Offers.TakeLast(4)));
            var person = world.ExportState().Agents.Single(item => item.AgentId == actor);
            var equipped = world.Society.Inventory.GetLot(Assert.IsType<string>(person.Equipment?.CarryAidLotId));
            Assert.Equal(("basket", actor, 1), (equipped.ItemKind, equipped.OwnerId, equipped.Quantity));
            Assert.Null(equipped.StorageBuildingId);
            Assert.Equal(16, PersonalEquipmentRules.Capacity(world.Society.Inventory, actor, person.Equipment));
            Assert.Equal(0, world.Society.Inventory.Lots.Where(lot => lot.Id == "basket-fiber").Sum(lot => lot.Quantity));
            Assert.Equal(2, world.WorldSimulation.ProductionJobs.Count(job => job.State == WorldProductionJobState.Completed));
            world.Validate();
        }
        finally { world.Dispose(); }
    }

    [Theory]
    [InlineData("sew-padded-coat", "padded_coat", 12, 4, WeatherKind.Snow)]
    [InlineData("sew-rain-cloak", "rain_cloak", 7, 2, WeatherKind.Rain)]
    [InlineData("sew-sack", "sack", 6, 2, WeatherKind.Clear)]
    public async Task HouseholdSuppliesWeavesSewsAndEquipsTailorGoodsAcrossReload(string recipeId,
        string kind, int fiber, int clothCost, WeatherKind weather)
    {
        // On some maps the tailor also gathers nearby wild fiber and weaves
        // more cloth than the garment needs; these seeds' maps do not.
        var (state, shopId) = TailorTestWorld.Create("equipment-tailor-" + recipeId, fiber);
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        // Exhaust the starting garments so the selected garment must come
        // from the actual Tailor production chain below.
        var inventory = state.Society.Society.Inventory;
        state = WithInventory(state, inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.OwnerId == Alpha && PersonalEquipmentRules.IsGarment(lot.ItemKind)
                ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
        });
        if (kind == "sack")
            state = WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory,
                "sack-rope", "rope", Alpha, 1, storageBuildingId: "first-town-house-a"));
        // The House keeps two real rope/basket batches. The original inputs
        // above remain the surplus that this scenario carries into the Tailor.
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "zz-tailor-house-fiber-reserve", "fiber", Alpha, 6, storageBuildingId: "first-town-house-a");
        var sourceReserves = new List<InventoryLot> { inventory.GetLot("zz-tailor-house-fiber-reserve") };
        if (kind == "sack")
        {
            inventory = InventoryAuthority.AddLot(inventory,
                "zz-tailor-house-rope-reserve", "rope", Alpha, 2, storageBuildingId: "first-town-house-a");
            sourceReserves.Add(inventory.GetLot("zz-tailor-house-rope-reserve"));
        }
        state = WithInventory(state, inventory);
        var systems = state.WorldSystems!;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { FullnessBasisPoints = 9_000 } : person).ToArray(),
            WorldSystems = systems with
            {
                RegionalWeather = null,
                Config = systems.Config with
                {
                    WeatherProfiles = Enum.GetValues<SeasonKind>()
                    .Select(season => new WeatherProfile(season,
                        weather == WeatherKind.Clear ? 1 : 0, 0, weather == WeatherKind.Rain ? 1 : 0, 0,
                        weather == WeatherKind.Snow ? 1 : 0)).ToArray()
                },
                Climate = systems.Climate with { Weather = weather },
            },
        };
        var target = state.WorldContent!.Recipes.Single(item => item.LocalId == recipeId);
        var weave = state.WorldContent.Recipes.Single(item => item.LocalId == "weave-cloth");
        string[] choices = ["wear_clothing", "equip_carry_aid", "build:recipe:" + target.CanonicalId,
            "haul_household_stock", "supply_workstation:rope", "build:recipe:" + weave.CanonicalId, "supply_workstation:fiber",
            "seek_warmth", "tend_fire", "consume_food", "collect_shared_food"];
        var actorChoices = new Choices(choices);
        IDecisionProvider Provider(string id) => id == actor ? actorChoices : new Choices([]);
        var world = WorldRuntime.Restore(state, Provider);
        try
        {
            var reopened = false;
            string? selected = null;
            WorldProductionJob? sewn = null;
            var tickLimit = kind == "padded_coat" ? 340 : 300;
            for (var tick = 0; tick < tickLimit; tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                AssertSourceReserves(world);
                if (!reopened && world.WorldSimulation.ProductionJobs.Any(job =>
                        job.RecipeId == target.CanonicalId && job.State == WorldProductionJobState.Running))
                {
                    var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
                    world.Dispose();
                    world = WorldRuntime.Restore(saved, Provider);
                    reopened = true;
                }
                sewn = world.WorldSimulation.ProductionJobs.SingleOrDefault(job =>
                    job.RecipeId == target.CanonicalId && job.State == WorldProductionJobState.Completed);
                var equipment = world.Agents.Single(person => person.AgentId == actor).Equipment;
                selected = kind == "sack" ? equipment?.CarryAidLotId : equipment?.ClothingLotId;
                if (sewn is not null && selected == sewn.JobId + ":output:00") break;
            }
            Assert.True(reopened, "Jobs: " + string.Join(",", world.WorldSimulation.ProductionJobs.Select(job => job.RecipeId + ":" + job.State)) +
                " Project: " + world.Agents.Single(person => person.AgentId == actor).Project +
                " Stock: " + string.Join(",", world.Society.Inventory.Lots.Where(lot => lot.OwnerId == Alpha || lot.OwnerId == actor)
                    .Select(lot => $"{lot.ItemKind}:{lot.Quantity}@{lot.StorageBuildingId}")) +
                " Offers: " + string.Join(";", actorChoices.Offers.TakeLast(3)));
            Assert.NotNull(sewn);
            Assert.Equal(sewn.JobId + ":output:00", selected);
            var unit = world.Society.Inventory.GetLot(selected!);
            Assert.Equal((kind, actor, 1), (unit.ItemKind, unit.OwnerId, unit.Quantity));
            Assert.Null(unit.StorageBuildingId);
            Assert.Equal(clothCost, world.WorldSimulation.ProductionJobs.Count(job =>
                job.RecipeId == weave.CanonicalId && job.State == WorldProductionJobState.Completed));
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot =>
                lot.OwnerId == Alpha && lot.ItemKind == "cloth" && lot.Quantity > 0);
            Assert.Contains(world.ExportState().Events, item => item.Kind == "workstation_input_picked_up");
            if (kind == "sack") Assert.Equal(0, world.Society.Inventory.Lots.Where(lot => lot.Id == "sack-rope").Sum(lot => lot.Quantity));
            Assert.All(world.WorldSimulation.ProductionJobs, job => Assert.Equal(shopId, job.BuildingInstanceId));
            using var savedWorld = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
                WorldRuntimeCodec.Encode(world.ExportState())), Provider);
            Assert.Equal(world.Agents.Single(person => person.AgentId == actor).Equipment,
                savedWorld.Agents.Single(person => person.AgentId == actor).Equipment);
            AssertSourceReserves(savedWorld);
            savedWorld.Validate();
        }
        finally { world.Dispose(); }

        void AssertSourceReserves(WorldRuntime current) => Assert.All(sourceReserves, expected =>
        {
            var actual = current.Society.Inventory.GetLot(expected.Id);
            Assert.Equal(expected with { LastProcessedTick = actual.LastProcessedTick }, actual);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimedRepairResumesAfterCodecReloadAndSpendsOnlyItsReservedCloth(bool skilled)
    {
        var (state, shop) = TailorTestWorld.Create("equipment-repair", 0);
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var position = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == shop).Position;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "repair-coat", "padded_coat", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, "repair-coat", 8_000);
        inventory = InventoryAuthority.AddLot(inventory, "repair-cloth", "cloth", actor, 2);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(item => item.AgentId == actor
                ? item with { Position = position, FullnessBasisPoints = 9_000, Survival = new(), Equipment = new("repair-coat") } : item).ToArray(),
        };
        state = SkillWorkTests.WithSkill(state, actor, skilled ? SkillKind.Crafting : null);
        IDecisionProvider Provider(string id) => new Choices(id == actor ? ["repair_equipment"] : []);
        using var first = WorldRuntime.Restore(state, Provider);
        for (var tick = 0; tick < 30 && first.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.Repair is null; tick++)
            Assert.True((await first.AdvanceOneTickAsync()).Advanced);
        var working = first.ExportState();
        var repair = Assert.IsType<EquipmentRepairWork>(working.Agents.Single(item => item.AgentId == actor).Equipment?.Repair);
        Assert.Equal(2, first.Society.Inventory.GetLot("repair-cloth").Quantity);
        var originalMaterial = repair.MaterialReservationIds[0];
        WorldRuntimeState WithSavedMaterialKey(string value)
        {
            var key = Identity.ParseStored(value);
            var changedInventory = working.Society.Society.Inventory with
            {
                Reservations = working.Society.Society.Inventory.Reservations.Select(item => item.Id == originalMaterial
                    ? item with { Id = key } : item).OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray(),
            };
            return WithInventory(working, changedInventory) with
            {
                Agents = working.Agents.Select(item => item.AgentId == actor ? item with
                {
                    Equipment = item.Equipment! with
                    { Repair = repair with { MaterialReservationIds = repair.MaterialReservationIds.Select(id => id == originalMaterial ? key : id).ToArray() } },
                } : item).ToArray(),
            };
        }
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithSavedMaterialKey("control\0material"), Provider));
        // The skilled row resumes and cancels with an existing legal long input
        // reference. The ordinary row keeps the native generated key.
        if (skilled)
        {
            working = WithSavedMaterialKey(new string('x', 129));
            repair = working.Agents.Single(item => item.AgentId == actor).Equipment!.Repair!;
        }
        using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(working)), Provider);
        for (var tick = 0; tick < 12 && resumed.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.Repair is not null; tick++)
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        Assert.Null(resumed.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.Repair);
        Assert.Equal(skilled ? 6 : 8, resumed.WorldTick - working.Society.Society.WorldTick);
        Assert.Equal(1, resumed.Society.Inventory.GetLot("repair-cloth").Quantity);
        Assert.InRange(resumed.Society.Inventory.GetLot("repair-coat").ConditionBasisPoints, 7_800, 8_000);
        Assert.All(repair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Completed, resumed.Society.Inventory.GetReservation(id).State));
        resumed.Validate();

        var interrupted = working with { Agents = working.Agents.Select(item => item.AgentId == actor ? item with { FullnessBasisPoints = 1_000 } : item).ToArray() };
        using var cancelled = WorldRuntime.Restore(interrupted, Provider);
        Assert.True((await cancelled.AdvanceOneTickAsync()).Advanced);
        Assert.Null(cancelled.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.Repair);
        Assert.Equal(2, cancelled.Society.Inventory.GetLot("repair-cloth").Quantity);
        Assert.All(repair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Released, cancelled.Society.Inventory.GetReservation(id).State));
        cancelled.Validate();
    }

    [Fact]
    public async Task AnOwnerOrderStepInterruptsTimedRepairAndReleasesItsCloth()
    {
        var (state, shop) = TailorTestWorld.Create("order-interrupts-repair", 0);
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var position = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == shop).Position;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "order-coat", "padded_coat", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, "order-coat", 8_000);
        inventory = InventoryAuthority.AddLot(inventory, "order-cloth", "cloth", actor, 2);
        inventory = InventoryAuthority.AddLot(inventory, "order-berries", "berries", actor, 1);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(item => item.AgentId == actor
                ? item with { Position = position, FullnessBasisPoints = 9_000, Survival = new(), Equipment = new("order-coat") } : item).ToArray(),
        };
        IDecisionProvider Provider(string id) => new Choices(id == actor ? ["repair_equipment"] : []);
        using var world = WorldRuntime.Restore(state, Provider);
        for (var tick = 0; tick < 30 && world.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.Repair is null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var repair = Assert.IsType<EquipmentRepairWork>(world.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.Repair);

        var working = world.ExportState();
        var readyToEat = working with
        {
            Agents = working.Agents.Select(item => item.AgentId == actor
                ? item with { FullnessBasisPoints = 3_000 } : item).ToArray(),
        };
        using var ordered = WorldRuntime.Restore(readyToEat, Provider);
        var order = ordered.SubmitInstruction(new OwnerInstructionRequest("eat-during-repair", "owner:test", actor,
            OwnerInstructionKind.Order, "eat berries"));
        for (var tick = 0; tick < 3 && !(ordered.ExportState().CompletedInstructionIds ?? []).Contains(order.InstructionId); tick++)
            Assert.True((await ordered.AdvanceOneTickAsync()).Advanced);

        var after = ordered.ExportState();
        Assert.Contains(order.InstructionId.Value, after.CompletedInstructionIds ?? []);
        Assert.DoesNotContain(after.Society.Society.Inventory.Lots, lot => lot.Id == "order-berries");
        Assert.Null(after.Agents.Single(item => item.AgentId == actor).Equipment?.Repair);
        Assert.Equal(2, ordered.Society.Inventory.GetLot("order-cloth").Quantity);
        Assert.All(repair.MaterialReservationIds, id =>
            Assert.Equal(InventoryReservationState.Released, ordered.Society.Inventory.GetReservation(id).State));
        Assert.Contains(after.Events, item => item.Kind == "equipment_repair_interrupted");
        ordered.Validate();
    }

    [Fact]
    public async Task WaitingForModelInstructionCannotKeepAnExpiredRepairInTheCheckpoint()
    {
        var (state, shopId) = TailorTestWorld.Create("held-planning-repair-probe", 0);
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == Alpha).Id;
        var position = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == shopId).Position;
        var started = state.Society.Society.WorldTick;
        const string coatId = "planning-probe-coat";
        const string clothId = "planning-probe-cloth";
        const string reservationId = "planning-probe-reservation";
        var recipe = state.WorldContent!.Recipes.Single(item => item.LocalId == "wooden-axe");
        var pausedPlan = new AgentProject("build:recipe:" + recipe.CanonicalId, recipe.DisplayName, started,
            "paused", 4, "Materials for this work are unavailable. Choose another task for now.",
            LastTransitionTick: started, RequiresFreshChoice: true);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, coatId, "padded_coat", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, coatId, 8_000);
        inventory = InventoryAuthority.AddLot(inventory, clothId, "cloth", actor, 2);
        inventory = InventoryAuthority.Reserve(inventory, reservationId, actor, clothId, 1,
            "equipment_repair", started + 120);
        state = WithWeather(WithInventory(state, inventory), WeatherKind.Clear) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = position,
                FullnessBasisPoints = 10_000,
                Survival = new(),
                Project = pausedPlan,
                LastDecisionContext = null,
                Equipment = new(ClothingLotId: coatId,
                    Repair: new(coatId, shopId, started, 0, [Identity.Parse(reservationId)])),
            } : person).ToArray(),
        };
        var provider = new HeldRepairPlanningProvider();
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), id => id == actor ? provider : new Choices([]));
        // Keep this expiry check stationary: an untargeted harvest can now
        // scout into cold terrain and legitimately wear the coat. This order
        // has no carried input and cannot progress while its reply is held.
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == "stone");
        var receipt = world.SubmitInstruction(new OwnerInstructionRequest("planning-probe-order", "owner:test",
            actor, OwnerInstructionKind.Order, "store stone"));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
        for (var tick = 0; tick < 120; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);

        Assert.Equal(1, provider.Calls);
        Assert.True(world.WorldTick - started > 120);
        var final = world.ExportState();
        Assert.DoesNotContain(receipt.InstructionId.Value, final.CompletedInstructionIds ?? []);
        var saved = WorldRuntimeCodec.Encode(final);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new Choices([]));
        Assert.Null(restored.Agents.Single(person => person.AgentId == actor).Equipment?.Repair);
        Assert.Equal(coatId, restored.Agents.Single(person => person.AgentId == actor).Equipment?.ClothingLotId);
        var savedProject = Assert.IsType<AgentProject>(
            restored.Agents.Single(person => person.AgentId == actor).Project);
        Assert.Equal(pausedPlan.CandidateId, savedProject.CandidateId);
        Assert.Equal(pausedPlan.WorkDone, savedProject.WorkDone);
        Assert.Equal("paused", savedProject.Stage);
        Assert.Null(savedProject.JobId);
        Assert.True(savedProject.RequiresFreshChoice);
        Assert.False(string.IsNullOrWhiteSpace(savedProject.Blocker));
        Assert.Equal(InventoryReservationState.Released, restored.Society.Inventory.GetReservation(reservationId).State);
        Assert.Equal(2, restored.Society.Inventory.GetLot(clothId).Quantity);
        Assert.Equal(2_000, restored.Society.Inventory.GetLot(coatId).ConditionBasisPoints);
        Assert.Contains(final.Events, item => item.Kind == "equipment_repair_interrupted" &&
            item.Detail == actor + "|" + coatId);
        restored.Validate();
    }

    [Fact]
    public async Task AcceptingConversationReleasesRepairMaterialsAndKeepsTheBusyWorldSaveable()
    {
        var (state, shopId) = TailorTestWorld.Create("repair-conversation-expiry", 0);
        var people = state.Society.Society.Agents.Where(person => person.HouseholdId == Alpha).ToArray();
        var actor = people[0].Id;
        var other = people[1].Id;
        var position = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == shopId).Position;
        var started = state.Society.Society.WorldTick;
        const string coatId = "conversation-repair-coat";
        const string clothId = "conversation-repair-cloth";
        const string reservationId = "conversation-repair-input";
        const string conversationId = "conversation:repair-interruption";
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, coatId, "padded_coat", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, coatId, 8_000);
        inventory = InventoryAuthority.AddLot(inventory, clothId, "cloth", actor, 2);
        inventory = InventoryAuthority.Reserve(inventory, reservationId, actor, clothId, 1,
            "equipment_repair", started + 120);
        var repair = new EquipmentRepairWork(coatId, shopId, started, 0, [Identity.Parse(reservationId)]);
        state = WithWeather(WithInventory(state, inventory), WeatherKind.Clear) with
        {
            Conversations = [AgentConversationRules.Propose(conversationId, other, actor,
                started, state.Society.Society.RunEpoch)],
            ConversationBudgets = [new(other, started / state.Society.Society.Config.TicksPerWorldDay, 1)],
            Agents = state.Agents.Select(person => person.AgentId == actor || person.AgentId == other
                ? person with
                {
                    Position = position,
                    FullnessBasisPoints = 10_000,
                    Survival = new(),
                    Project = null,
                    LastDecisionContext = null,
                    Equipment = person.AgentId == actor ? new(ClothingLotId: coatId, Repair: repair) : person.Equipment,
                } : person).ToArray(),
        };
        var provider = new HeldRepairConversationProvider(new HashSet<string>([actor, other], StringComparer.Ordinal));
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), _ => provider);
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "conversation_accepted" &&
            item.Detail.StartsWith(conversationId, StringComparison.Ordinal));
        const int pendingProviderTicks = 12;
        for (var tick = 0; tick < pendingProviderTicks; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            Assert.Equal(AgentConversationStatus.AwaitingSpeaker, Assert.Single(world.Conversations).Status);
        }
        Assert.True(world.WorldTick - started > pendingProviderTicks);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        var decoded = WorldRuntimeCodec.Decode(saved);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(decoded));
        using var restored = WorldRuntime.Restore(decoded, _ => provider);
        Assert.Null(restored.Agents.Single(person => person.AgentId == actor).Equipment?.Repair);
        Assert.Equal(InventoryReservationState.Released, restored.Society.Inventory.GetReservation(reservationId).State);
        Assert.Equal(2, restored.Society.Inventory.GetLot(clothId).Quantity);
        Assert.Null(world.Agents.Single(person => person.AgentId == actor).Equipment?.Repair);
        Assert.Equal(InventoryReservationState.Released, world.Society.Inventory.GetReservation(reservationId).State);
        Assert.Equal(2, world.Society.Inventory.GetLot(clothId).Quantity);
        Assert.Equal(2_000, world.Society.Inventory.GetLot(coatId).ConditionBasisPoints);

        provider.ReleaseHeldTurn();
        using var completionDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Assert.Single(world.Conversations).Status != AgentConversationStatus.Closed)
        {
            // Held responses resume asynchronously; give them time to reach a later tick's admission.
            await ModelWorkHarness.For(world).SettleAsync();
            Assert.True((await world.TickModelWorkAsync(cancellationToken: completionDeadline.Token)).Advanced);
        }
        var completedConversation = Assert.Single(world.Conversations);
        Assert.Equal(AgentConversationStatus.Closed, completedConversation.Status);
        Assert.Equal("agreed", completedConversation.Outcome);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "conversation_turn_admitted" &&
            item.Detail.StartsWith(conversationId + ":", StringComparison.Ordinal));
        var completedSave = WorldRuntimeCodec.Encode(world.ExportState());
        using var completedReload = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(completedSave), _ => new Choices([]));
        Assert.Equal(completedSave, WorldRuntimeCodec.Encode(completedReload.ExportState()));
        Assert.Equal(AgentConversationStatus.Closed, Assert.Single(completedReload.Conversations).Status);
        Assert.Equal(2_000, completedReload.Society.Inventory.GetLot(coatId).ConditionBasisPoints);
    }

    [Fact]
    public async Task ReplacingABrokenBasketKeepsItsOverloadedCargoAndTheOldBasket()
    {
        using var generated = NormalPathWorld.CreateGenerated("equipment-replacement", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "old-basket", "basket", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, "old-basket", 10_000);
        inventory = InventoryAuthority.AddLot(inventory, "overloaded-wood", "wood", actor, 12);
        inventory = InventoryAuthority.AddLot(inventory, "replacement-sack", "sack", Alpha, 1, storageBuildingId: "first-town-house-a");
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(item => item.AgentId == actor
            ? item with { Equipment = new(CarryAidLotId: "old-basket") } : item).ToArray()
        };
        using var world = WorldRuntime.Restore(state, id => new Choices(id == actor ? ["equip_carry_aid"] : []));
        for (var tick = 0; tick < 120 && world.ExportState().Agents.Single(item => item.AgentId == actor).Equipment?.CarryAidLotId != "replacement-sack"; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var person = world.ExportState().Agents.Single(item => item.AgentId == actor);
        Assert.Equal("replacement-sack", person.Equipment?.CarryAidLotId);
        Assert.Equal((actor, 1, 0), (world.Society.Inventory.GetLot("old-basket").OwnerId, world.Society.Inventory.GetLot("old-basket").Quantity, world.Society.Inventory.GetLot("old-basket").ConditionBasisPoints));
        Assert.Equal(12, world.Society.Inventory.GetLot("overloaded-wood").Quantity);
        var reloaded = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(person.Equipment, reloaded.Agents.Single(item => item.AgentId == actor).Equipment);
        world.Validate();
    }

    [Fact]
    public void CurrentSaveRejectsAnEquippedStackOrAnotherPersonsItem()
    {
        using var generated = NormalPathWorld.CreateGenerated("equipment-invalid", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        foreach (var (owner, quantity) in new[] { (actor, 2), (state.Agents[1].AgentId, 1) })
        {
            var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "invalid-sack", "sack", owner, quantity);
            var invalid = WithInventory(state, inventory) with
            {
                Agents = state.Agents.Select(item => item.AgentId == actor
                ? item with { Equipment = new(CarryAidLotId: "invalid-sack") } : item).ToArray()
            };
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(invalid)));
        }
    }

    [Fact]
    public async Task AFullCarrierDeclinesGearAndRepairThenCanDeliverAndSwapWithoutLosingGoods()
    {
        var (state, _) = TailorTestWorld.Create("equipment-full-carrier", 0);
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var house = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "full-garment", "clothing", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, "full-garment", 8_000);
        inventory = InventoryAuthority.AddLot(inventory, "full-cargo", "wood", Alpha, 8,
            storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.Transfer(inventory, "full-cargo-collected", Alpha, actor, "full-cargo", 8,
            "household_stock_picked_up", destinationDeliveryBuildingId: house.InstanceId);
        foreach (var (id, kind) in new[] { ("full-coat", "padded_coat"), ("full-cloth", "cloth"), ("full-pickaxe", "wooden_pickaxe") })
            inventory = InventoryAuthority.AddLot(inventory, id, kind, Alpha, 1, storageBuildingId: house.InstanceId);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = house.Position, FullnessBasisPoints = 9_000, Equipment = new("full-garment") } : person).ToArray(),
        };
        state = WithWeather(state, WeatherKind.Snow);
        var blocked = new Choices(["wear_clothing", "repair_equipment", "collect_wooden_pickaxe"]);
        using var full = WorldRuntime.Restore(state, id => id == actor ? blocked : new Choices([]));
        for (var tick = 0; tick < 5; tick++) Assert.True((await full.AdvanceOneTickAsync()).Advanced);
        Assert.All(blocked.Offers, offer =>
        {
            Assert.DoesNotContain("wear_clothing", offer);
            Assert.DoesNotContain("repair_equipment", offer);
            Assert.DoesNotContain("collect_wooden_pickaxe", offer);
        });
        Assert.Equal("full-garment", full.Agents.Single(person => person.AgentId == actor).Equipment?.ClothingLotId);
        Assert.Equal((actor, 8), (full.Society.Inventory.GetLot("full-cargo").OwnerId, full.Society.Inventory.GetLot("full-cargo").Quantity));
        // This controlled provider changes its choice after the declined
        // actions, so ask for a fresh decision without changing physical stock.
        var deliveryState = full.ExportState();
        deliveryState = deliveryState with
        {
            Agents = deliveryState.Agents.Select(person =>
            person.AgentId == actor ? person with { LastDecisionContext = null } : person).ToArray()
        };
        using var delivery = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(deliveryState)), id => new Choices(id == actor ? ["haul_household_stock"] : []));
        Assert.True((await delivery.AdvanceOneTickAsync()).Advanced);
        var delivered = delivery.Society.Inventory.GetLot("full-cargo");
        Assert.Equal((Alpha, 8, house.InstanceId), (delivered.OwnerId, delivered.Quantity, delivered.StorageBuildingId));
        using var swap = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(delivery.ExportState())), id => new Choices(id == actor ? ["wear_clothing"] : []));
        for (var tick = 0; tick < 60 && swap.Agents.Single(person => person.AgentId == actor).Equipment?.ClothingLotId == "full-garment"; tick++)
            Assert.True((await swap.AdvanceOneTickAsync()).Advanced);
        var replacementId = Assert.IsType<string>(swap.Agents.Single(person => person.AgentId == actor).Equipment?.ClothingLotId);
        Assert.NotEqual("full-garment", replacementId);
        var replacement = swap.Society.Inventory.GetLot(replacementId);
        Assert.True(PersonalEquipmentRules.IsGarment(replacement.ItemKind));
        Assert.Equal((actor, 1), (replacement.OwnerId, replacement.Quantity));
        Assert.Null(replacement.StorageBuildingId);
        Assert.True(replacement.ConditionBasisPoints > swap.Society.Inventory.GetLot("full-garment").ConditionBasisPoints);
        Assert.Equal((actor, 1), (swap.Society.Inventory.GetLot("full-garment").OwnerId, swap.Society.Inventory.GetLot("full-garment").Quantity));
        Assert.Equal((Alpha, 1), (swap.Society.Inventory.GetLot("full-cloth").OwnerId, swap.Society.Inventory.GetLot("full-cloth").Quantity));
        swap.Validate();
    }

    [Fact]
    public async Task AnOverloadedCarrierKeepsTheBrokenBasketWhenAReplacementWouldStillExceedCapacity()
    {
        using var generated = NormalPathWorld.CreateGenerated("equipment-too-small-replacement", _ => new Choices([]));
        var state = generated.ExportState();
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "broken-basket", "basket", actor, 1);
        inventory = InventoryAuthority.WearSingleUnit(inventory, "broken-basket", 10_000);
        inventory = InventoryAuthority.AddLot(inventory, "heavy-cargo", "wood", actor, 20);
        inventory = InventoryAuthority.AddLot(inventory, "small-replacement", "basket", Alpha, 1, storageBuildingId: "first-town-house-a");
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Equipment = new(CarryAidLotId: "broken-basket") } : person).ToArray(),
        };
        var choices = new Choices(["equip_carry_aid"]);
        using var world = WorldRuntime.Restore(state, id => id == actor ? choices : new Choices([]));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.All(choices.Offers, offer => Assert.DoesNotContain("equip_carry_aid", offer));
        Assert.Equal("broken-basket", world.Agents.Single(person => person.AgentId == actor).Equipment?.CarryAidLotId);
        Assert.Equal((actor, 20), (world.Society.Inventory.GetLot("heavy-cargo").OwnerId, world.Society.Inventory.GetLot("heavy-cargo").Quantity));
        Assert.Equal((Alpha, 1), (world.Society.Inventory.GetLot("small-replacement").OwnerId, world.Society.Inventory.GetLot("small-replacement").Quantity));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), _ => new Choices([]));
        restored.Validate();
    }

    [Theory]
    [InlineData(false, 4, 0)]
    [InlineData(true, 4, 0)]
    [InlineData(false, 5, 4)]
    public async Task HungryCarriersMoveWholeJugFamiliesAndLeaveReservedContentsInPlace(
        bool reserved, int houseRoom, int incomingQuantity)
    {
        using var initial = NormalPathWorld.CreateGenerated("food-capacity-boundary", _ => new Choices([]));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var house = initial.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => lot.OwnerId != actor &&
                !(lot.OwnerId == Alpha && InventoryContainerRules.IsFood(lot.ItemKind))).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "z-spare-jug", InventoryContainerRules.WaterJug, actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "a-contained-water", InventoryContainerRules.FreshWater,
            actor, 4, containerLotId: "z-spare-jug");
        if (reserved)
            inventory = InventoryAuthority.Reserve(inventory, "water-reserved", actor, "a-contained-water", 1,
                "keep water for a job", inventory.WorldTick + 100);
        else
            inventory = InventoryAuthority.AddLot(inventory, "protected-map", "field_map", actor, 3);
        // The complete family needs five units, without taking room from supplies already on their way.
        var stored = inventory.Lots.Where(lot => lot.StorageBuildingId == house.InstanceId).Sum(lot => lot.Quantity);
        inventory = InventoryAuthority.AddLot(inventory, "house-filler", "wood", Alpha,
            BuildingStorageRules.UnitsPerTile - houseRoom - stored, storageBuildingId: house.InstanceId);
        if (incomingQuantity > 0)
        {
            var carrier = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha && item.Id != actor).Id;
            inventory = InventoryAuthority.AddLot(inventory, "incoming-house-wood", "wood", carrier, incomingQuantity);
            inventory = inventory with
            {
                Lots = inventory.Lots.Select(lot => lot.Id == "incoming-house-wood"
                    ? lot with { DeliveryBuildingId = house.InstanceId } : lot).ToArray(),
            };
        }
        var startingSite = initial.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-warehouse").Position;
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = startingSite, FullnessBasisPoints = 6_000, LastDecisionContext = null, Equipment = null }
                : person).ToArray(),
        };
        var provider = new Choices(["make_room_for_food"]);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), id => id == actor ? provider : new Choices([]));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var reloaded = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        var lots = reloaded.Society.Society.Inventory;
        var jug = lots.GetLot("z-spare-jug");
        var water = lots.GetLot("a-contained-water");
        Assert.Equal(1, jug.Quantity);
        Assert.Equal(4, water.Quantity);
        Assert.Equal(jug.Id, water.ContainerLotId);
        Assert.Equal(jug.OwnerId, water.OwnerId);
        Assert.Null(jug.StorageBuildingId);
        Assert.Null(water.StorageBuildingId);
        if (reserved)
        {
            Assert.All(provider.Offers, offer => Assert.DoesNotContain("make_room_for_food", offer.Split(',')));
            Assert.Equal(actor, jug.OwnerId);
            Assert.Null(jug.GroundPosition);
            Assert.Equal(InventoryReservationState.Reserved, lots.GetReservation("water-reserved").State);
            Assert.Equal(5, PersonalEquipmentRules.CarriedQuantity(lots, actor, null));
        }
        else
        {
            Assert.Contains(provider.Offers, offer => offer.Split(',').Contains("make_room_for_food", StringComparer.Ordinal));
            Assert.Equal(Alpha, jug.OwnerId);
            Assert.Equal(new InventoryGroundPosition(startingSite.X, startingSite.Y), jug.GroundPosition);
            Assert.Null(water.GroundPosition);
            Assert.Equal(3, PersonalEquipmentRules.CarriedQuantity(lots, actor, null));
            Assert.Equal(actor, lots.GetLot("protected-map").OwnerId);
            Assert.Single(reloaded.Events, item => item.Kind == "spare_cargo_stored" &&
                item.Detail.StartsWith(actor + ":water_jug:1:", StringComparison.Ordinal));
        }
        if (incomingQuantity > 0)
        {
            var incoming = lots.GetLot("incoming-house-wood");
            Assert.Equal(inventory.GetLot(incoming.Id) with { LastProcessedTick = incoming.LastProcessedTick }, incoming);
        }
        world.Validate();
    }

    [Fact]
    public async Task AFullCarrierMakesRoomForTheHouseholdsOnlyPotFoodAcrossReload()
    {
        using var initial = NormalPathWorld.CreateGenerated("pot-only-food-capacity", _ => new Choices([]));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == Alpha).Id;
        var house = initial.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                !(lot.OwnerId == Alpha && InventoryContainerRules.IsFood(lot.ItemKind))).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "only-food-pot", InventoryContainerRules.StoragePot,
            Alpha, 1, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "only-pot-food", "food", Alpha, 2,
            storageBuildingId: house.InstanceId, containerLotId: "only-food-pot");
        inventory = InventoryAuthority.AddLot(inventory, "full-carried-wood", "wood", actor, 8);
        var woodBefore = inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var foodIds = state.Map.Resources.Where(resource => resource.Kind is "food" or "fruit")
            .Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = house.Position, FullnessBasisPoints = 6_000, LastDecisionContext = null, Equipment = null }
                : person).ToArray(),
            Resources = state.Resources.Select(resource => foodIds.Contains(resource.ResourceId)
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource => foodIds.Contains(resource.Id)
                        ? resource with { Quantity = 0, State = EcologyResourceState.Depleted, NextRegenerationDay = 100 }
                        : resource).ToArray(),
                },
            },
        };
        var provider = new Choices(["make_room_for_food", "take_food_from_pot"]);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), id => id == actor ? provider : new Choices([]));
        for (var tick = 0; tick < 30 && !world.ExportState().Events.Any(item => item.Kind == "spare_cargo_stored"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "spare_cargo_stored" &&
            item.Detail == $"{actor}:wood:1:{house.InstanceId}");
        Assert.Equal(7, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
        var roomBytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(roomBytes),
            id => id == actor ? provider : new Choices([]));
        Assert.Equal(roomBytes, WorldRuntimeCodec.Encode(resumed.ExportState()));
        for (var tick = 0; tick < 30 && !resumed.ExportState().Events.Any(item => item.Kind == "food_taken_from_pot"); tick++)
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        Assert.Single(resumed.ExportState().Events, item => item.Kind == "food_taken_from_pot" &&
            item.Detail == $"{actor}:only-food-pot:only-pot-food:1");
        var lots = resumed.Society.Inventory;
        Assert.Equal(woodBefore, lots.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Equal(1, lots.GetLot("only-food-pot").Quantity);
        Assert.Equal((Alpha, house.InstanceId, "only-food-pot", 1),
            (lots.GetLot("only-pot-food").OwnerId, lots.GetLot("only-pot-food").StorageBuildingId,
                lots.GetLot("only-pot-food").ContainerLotId, lots.GetLot("only-pot-food").Quantity));
        Assert.Equal(1, lots.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == "food" && lot.ContainerLotId is null)
            .Sum(lot => lot.Quantity));
        Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(lots, actor, null));
        using var finalReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(resumed.ExportState())), _ => new Choices([]));
        finalReload.Validate();
    }

    [Theory]
    [InlineData("clothing", 0, 10_000)]
    [InlineData("food", 10_000, 0)]
    public async Task AHungryCarrierSetsDownBrokenOrSpoiledUnselectedCargoWithoutLosingIt(
        string kind, int condition, int freshness)
    {
        using var initial = new WorldRuntime("food-capacity-boundary", _ => new Choices([]));
        initial.StageStarterContent();
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var actor = state.Agents[0].AgentId;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var source = state.Map.Resources.First(item => item.Kind == "food" && item.TreeKind is null);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                !(lot.OwnerId == household && lot.ItemKind == "food")).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "unusable-spare-cargo", kind, actor, 8,
            conditionBasisPoints: condition, freshnessBasisPoints: freshness);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with
                {
                    Position = source.Position,
                    FullnessBasisPoints = 6_000,
                    Equipment = null,
                    LastDecisionContext = null
                } : person).ToArray(),
        };
        var provider = new Choices(["make_room_for_food", "harvest_food", "seek_food"]);
        var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), id => id == actor ? provider : new Choices([]));
        try
        {
            for (var tick = 0; tick < 60 && !world.ExportState().Events.Any(item => item.Kind == "food_harvested" &&
                     item.Detail.StartsWith(actor + ":", StringComparison.Ordinal)); tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                if (tick == 1)
                {
                    var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
                    world.Dispose();
                    world = WorldRuntime.Restore(saved, id => id == actor ? provider : new Choices([]));
                }
            }

            Assert.Contains(provider.Offers, offer => offer.Split(',').Contains("make_room_for_food", StringComparer.Ordinal));
            Assert.Single(world.ExportState().Events, item => item.Kind == "spare_cargo_stored" &&
                item.Detail.StartsWith(actor + ":" + kind + ":4:", StringComparison.Ordinal));
            Assert.Single(world.ExportState().Events, item => item.Kind == "food_harvested" &&
                item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
            var unchangedCargo = world.Society.Inventory.Lots.Where(lot => lot.ItemKind == kind &&
                lot.ConditionBasisPoints == condition && lot.FreshnessBasisPoints == freshness &&
                (lot.OwnerId == actor || lot.OwnerId == household)).ToArray();
            Assert.Equal(8, unchangedCargo.Sum(lot => lot.Quantity));
            Assert.Equal(4, unchangedCargo.Where(lot => lot.OwnerId == household).Sum(lot => lot.Quantity));
            Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
            world.Validate();
        }
        finally { world.Dispose(); }
    }

    [Fact]
    public async Task AHungryCarrierSetsDownSpareSuppliesThenGathersFoodAcrossReload()
    {
        using var initial = new WorldRuntime("food-capacity-boundary", _ => new Choices([]));
        initial.StageStarterContent();
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var actor = state.Agents[0].AgentId;
        var household = state.Society.Society.Agents.Single(item => item.Id == actor).HouseholdId!;
        var source = state.Map.Resources.First(item => item.Kind == "food" && item.TreeKind is null);
        var inventory = state.Society.Society.Inventory;
        // With no household food to collect, the bush is the only food.
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => lot.OwnerId != actor && !(lot.OwnerId == household && lot.ItemKind == "food")).ToArray(),
        };
        // Five carried units leave room for 3; a food pick needs 4.
        inventory = InventoryAuthority.AddLot(inventory, "spare-wood", "wood", actor, 4);
        inventory = InventoryAuthority.AddLot(inventory, "spare-tool", "tool", actor, 1);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = source.Position, FullnessBasisPoints = 6_000, LastDecisionContext = null } : person).ToArray(),
        };
        var householdWood = inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var provider = new Choices(["make_room_for_food", "harvest_food", "seek_food"]);
        var world = WorldRuntime.Restore(state, id => id == actor ? provider : new Choices([]));
        try
        {
            for (var tick = 0; tick < 60 && !world.ExportState().Events.Any(item => item.Kind == "food_harvested" &&
                     item.Detail.StartsWith(actor + ":", StringComparison.Ordinal)); tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                Assert.InRange(PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null), 0, 8);
                if (tick == 1)
                {
                    world.Pause();
                    var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
                        WorldRuntimeCodec.Encode(world.ExportState())), id => id == actor ? provider : new Choices([]));
                    world.Dispose();
                    world = reloaded;
                    world.Resume();
                }
            }

            var events = world.ExportState().Events;
            Assert.Contains(provider.Offers, offer => offer.Split(',').Contains("make_room_for_food", StringComparer.Ordinal));
            Assert.Single(events, item => item.Kind == "spare_cargo_stored" && item.Detail.StartsWith(actor + ":wood:1:", StringComparison.Ordinal));
            Assert.Single(events, item => item.Kind == "food_harvested" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
            var lots = world.Society.Inventory.Lots;
            // Wood is set down before the tool, and nothing is lost on the way.
            Assert.Equal(3, lots.Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
            Assert.Equal(1, lots.Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) && lot.ItemKind == "tool").Sum(lot => lot.Quantity));
            Assert.Equal(householdWood + 1, lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
            Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
            world.Validate();
        }
        finally
        {
            world.Dispose();
        }
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public async Task HelpingAProjectOffersGatheringOnlyWhenTheWholeLoadFits(int cargo, bool fits)
    {
        using var initial = new WorldRuntime("settlement-acquisition", _ => new Choices([]));
        initial.StageStarterContent();
        for (var tick = 0; tick < 6; tick++) Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var requester = state.Agents[0].AgentId;
        var helper = state.Agents[1].AgentId;
        var source = state.Map.Resources.First(item => item.Kind == "construction" && item.TreeKind is null);
        var house = initial.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point));
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != helper && lot.ItemKind != "wood").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "helper-unrelated-cargo", "fiber", helper, cargo);
        inventory = InventoryAuthority.AddLot(inventory, "helper-gathering-axe", "wooden_axe", helper, 1);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == helper
                ? person with { Position = source.Position, FullnessBasisPoints = 9_500, Equipment = null, LastDecisionContext = null }
                : person.AgentId == requester ? person with
                {
                    FullnessBasisPoints = 9_500,
                    Project = new AgentProject(TownConstructionCandidateIds.Building(house.CanonicalId, site),
                        house.DisplayName, initial.WorldTick, "acquiring", LastTransitionTick: initial.WorldTick),
                } : person).ToArray(),
        };
        var provider = new Choices(["assist:wood"]);
        using var world = WorldRuntime.Restore(state, id => id == helper ? provider : new Choices([]));
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.InRange(PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, helper, null), 0, 8);
            if (world.ExportState().Events.Any(item => item.Kind == "material_gathered" &&
                item.Detail.StartsWith(helper + ":wood:", StringComparison.Ordinal))) break;
        }
        if (fits) Assert.Contains(provider.Offers, offer => offer.Split(',').Contains("assist:wood", StringComparer.Ordinal));
        else Assert.All(provider.Offers, offer => Assert.DoesNotContain("assist:wood", offer.Split(',')));
        Assert.Equal(fits ? 1 : 0, world.ExportState().Events.Count(item => item.Kind == "material_gathered" &&
            item.Detail.StartsWith(helper + ":wood:6", StringComparison.Ordinal)));
        Assert.Equal(cargo, world.Society.Inventory.GetLot("helper-unrelated-cargo").Quantity);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "carrying_full" && item.Detail == helper);
        world.Validate();
    }

    [Fact]
    public async Task NoHouseHelperWaitsForRequesterCapacityThenTransfersOnlyWhatFitsAcrossReload()
    {
        using var initial = new WorldRuntime("settlement-acquisition", _ => new Choices([]));
        initial.StageStarterContent();
        for (var tick = 0; tick < 6; tick++) Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var requester = state.Agents[0].AgentId;
        var helper = state.Agents[1].AgentId;
        var household = state.Society.Society.GetAgent(requester).HouseholdId!;
        var startingSite = state.Map.GetObject("storage").Position;
        var house = initial.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        Assert.DoesNotContain(state.WorldSimulation!.Buildings,
            building => building.HouseholdId == household && building.DefinitionId == house.CanonicalId);
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point));
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != helper &&
                lot.OwnerId != requester && lot.ItemKind != "wood").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "full-builders-stone", "stone", requester, 8);
        inventory = InventoryAuthority.AddLot(inventory, "helper-house-wood", "wood", helper, 4);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == helper
                ? person with { FullnessBasisPoints = 9_500, Equipment = null, LastDecisionContext = null }
                : person.AgentId == requester ? person with
                {
                    FullnessBasisPoints = 9_500,
                    Equipment = null,
                    Project = new AgentProject(TownConstructionCandidateIds.Building(house.CanonicalId, site),
                        house.DisplayName, initial.WorldTick, "acquiring", LastTransitionTick: initial.WorldTick),
                } : person).ToArray(),
        };
        var provider = new Choices(["assist:wood"]);
        using var world = WorldRuntime.Restore(state, id => id == helper ? provider : new Choices([]));
        for (var tick = 0; tick < 30 && provider.Offers.Count == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.NotEmpty(provider.Offers);
        Assert.All(provider.Offers, offer => Assert.DoesNotContain("assist:wood", offer.Split(',')));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "project_request_fulfilled");
        Assert.Equal(helper, world.Society.Inventory.GetLot("helper-house-wood").OwnerId);
        Assert.Equal(4, world.Society.Inventory.GetLot("helper-house-wood").Quantity);
        Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, requester, null));

        var fullRequesterSave = WorldRuntimeCodec.Encode(world.ExportState());
        var fullRequesterState = WorldRuntimeCodec.Decode(fullRequesterSave);
        using (var fullRequesterReload = WorldRuntime.Restore(fullRequesterState, _ => new Choices([])))
        {
            Assert.Equal(fullRequesterSave, WorldRuntimeCodec.Encode(fullRequesterReload.ExportState()));
            Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(fullRequesterReload.Society.Inventory, requester, null));
        }

        var capacityInventory = InventoryAuthority.Transfer(fullRequesterState.Society.Society.Inventory,
            "requester-unloads-stone", requester, household, "full-builders-stone", 1, "test_setup_capacity",
            destinationGroundPosition: new InventoryGroundPosition(startingSite.X, startingSite.Y));
        var capacityState = WithInventory(fullRequesterState, capacityInventory) with
        {
            Agents = fullRequesterState.Agents.Select(person => person.AgentId == helper
                ? person with { LastDecisionContext = null } : person).ToArray(),
        };
        var capacityEventFloor = capacityState.Events.Select(item => item.EventId).DefaultIfEmpty(0).Max();
        var woodBeforeCapacityResume = capacityState.Society.Society.Inventory.Lots
            .Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var capacitySave = WorldRuntimeCodec.Encode(capacityState);
        var capacityProvider = new Choices(["assist:wood"]);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(capacitySave),
            id => id == helper ? capacityProvider : new Choices([]));
        Assert.Equal(capacitySave, WorldRuntimeCodec.Encode(restored.ExportState()));
        for (var tick = 0; tick < 30 && !restored.ExportState().Events.Any(item => item.Kind == "project_request_fulfilled"); tick++)
        {
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.InRange(PersonalEquipmentRules.CarriedQuantity(restored.Society.Inventory, requester, null), 0, 8);
        }
        Assert.Contains(capacityProvider.Offers, offer => offer.Split(',').Contains("assist:wood", StringComparer.Ordinal));
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "project_request_fulfilled" &&
            item.Detail == helper + ":" + requester + ":wood:1");

        var directHandoff = Assert.Single(restored.Society.Inventory.Events, item => item.Kind == "inventory_transferred" &&
            item.Detail.Contains($":{helper}:{requester}:helper-house-wood:1:project_request_fulfilled", StringComparison.Ordinal));
        var stagedContribution = Assert.Single(restored.Society.Inventory.Events, item => item.Kind == "inventory_transferred" &&
            item.Detail.Contains($":{requester}:{household}:helper-house-wood#transfer:project-share:", StringComparison.Ordinal) &&
            item.Detail.EndsWith(":1:project_contribution", StringComparison.Ordinal));
        Assert.Equal(directHandoff.WorldTick, stagedContribution.WorldTick);
        var helperRemainder = restored.Society.Inventory.GetLot("helper-house-wood");
        Assert.Equal(helper, helperRemainder.OwnerId);
        Assert.Equal(3, helperRemainder.Quantity);
        var delivered = Assert.Single(restored.Society.Inventory.Lots,
            lot => lot.ProvenanceLotId == "helper-house-wood");
        // The requester receives the helper's single unit, then stages it at the storage pile
        // for the active project during the same tick; prove both transfers above.
        Assert.Equal(household, delivered.OwnerId);
        Assert.Equal(new InventoryGroundPosition(startingSite.X, startingSite.Y), delivered.GroundPosition);
        Assert.Null(delivered.StorageBuildingId);
        Assert.Null(delivered.DeliveryBuildingId);
        Assert.Equal(1, delivered.Quantity);
        var stagedStone = restored.Society.Inventory.GetLot("full-builders-stone#transfer:requester-unloads-stone");
        Assert.Equal(household, stagedStone.OwnerId);
        Assert.Equal(new InventoryGroundPosition(startingSite.X, startingSite.Y), stagedStone.GroundPosition);
        Assert.Equal(1, stagedStone.Quantity);
        Assert.Equal(7, restored.Society.Inventory.GetLot("full-builders-stone").Quantity);
        Assert.Equal(7, PersonalEquipmentRules.CarriedQuantity(restored.Society.Inventory, requester, null));
        var resumedWoodGathered = restored.ExportState().Events
            .Where(item => item.EventId > capacityEventFloor && item.Kind == "material_gathered")
            .Select(item => item.Detail.Split(':'))
            .Where(parts => parts.Length == 3 && parts[1] == "wood")
            .Sum(parts => int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(woodBeforeCapacityResume + resumedWoodGathered,
            restored.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        var saved = WorldRuntimeCodec.Encode(restored.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new Choices([]));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        Assert.Equal(delivered, reloaded.Society.Inventory.GetLot(delivered.Id));
        Assert.Equal(stagedStone, reloaded.Society.Inventory.GetLot(stagedStone.Id));
        Assert.DoesNotContain(reloaded.WorldSimulation.Buildings,
            building => building.HouseholdId == household && building.DefinitionId == house.CanonicalId);
        reloaded.Validate();
    }

    [Fact]
    public async Task NoHouseAssistanceSkipsFullFirstRequesterForEligibleSecondRequester()
    {
        using var initial = new WorldRuntime("settlement-acquisition", _ => new Choices([]));
        initial.StageStarterContent();
        for (var tick = 0; tick < 6; tick++) Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        const string helper = "founder-mira";
        const string fullRequester = "founder-ilya";
        const string availableRequester = "founder-rowan";
        var household = state.Society.Society.GetAgent(fullRequester).HouseholdId!;
        Assert.Equal(household, state.Society.Society.GetAgent(availableRequester).HouseholdId);
        var house = initial.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var sites = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point)).Take(2).ToArray();
        Assert.Equal(2, sites.Length);
        Assert.DoesNotContain(state.WorldSimulation!.Buildings,
            building => building.HouseholdId == household && building.DefinitionId == house.CanonicalId);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != helper &&
                lot.OwnerId != fullRequester && lot.OwnerId != availableRequester && lot.ItemKind != "wood").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "full-first-requester-stone", "stone", fullRequester, 8);
        inventory = InventoryAuthority.AddLot(inventory, "eligible-requester-wood", "wood", helper, 4);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId switch
            {
                var id when id == helper => person with { FullnessBasisPoints = 9_500, Equipment = null, LastDecisionContext = null },
                var id when id == fullRequester => person with
                {
                    FullnessBasisPoints = 9_500,
                    Equipment = null,
                    LastDecisionContext = null,
                    Project = new AgentProject(TownConstructionCandidateIds.Building(house.CanonicalId, sites[0]),
                        house.DisplayName, initial.WorldTick, "paused",
                        Blocker: "Materials for this work are unavailable. Choose another task for now.",
                        LastTransitionTick: initial.WorldTick, RequiresFreshChoice: true),
                },
                var id when id == availableRequester => person with
                {
                    FullnessBasisPoints = 9_500,
                    Equipment = null,
                    LastDecisionContext = null,
                    Project = new AgentProject(TownConstructionCandidateIds.Building(house.CanonicalId, sites[1]),
                        house.DisplayName, initial.WorldTick, "paused",
                        Blocker: "Materials for this work are unavailable. Choose another task for now.",
                        LastTransitionTick: initial.WorldTick, RequiresFreshChoice: true),
                },
                _ => person,
            }).ToArray(),
        };
        var provider = new Choices(["assist:wood"]);
        using var world = WorldRuntime.Restore(state, id => id == helper ? provider : new Choices([]));
        for (var tick = 0; tick < 30 && !world.ExportState().Events.Any(item => item.Kind == "project_request_fulfilled"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(provider.Offers, offer => offer.Split(',').Contains("assist:wood", StringComparer.Ordinal));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "project_request_fulfilled" &&
            item.Detail == helper + ":" + availableRequester + ":wood:4");
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "project_request_fulfilled" &&
            item.Detail.StartsWith(helper + ":" + fullRequester + ":", StringComparison.Ordinal));
        Assert.Contains(world.Society.Inventory.Events, item => item.Kind == "inventory_transferred" &&
            item.Detail.Contains($":{helper}:{availableRequester}:eligible-requester-wood:4:project_request_fulfilled", StringComparison.Ordinal));
        Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, fullRequester, null));
        Assert.InRange(PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, availableRequester, null), 0, 8);
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new Choices([]));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(restored.Society.Inventory, fullRequester, null));
        Assert.Equal(4, restored.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        restored.Validate();
    }

    private static WorldRuntimeState WithWeather(WorldRuntimeState state, WeatherKind weather)
    {
        var systems = state.WorldSystems!;
        return state with
        {
            WorldSystems = systems with
            {
                RegionalWeather = null,
                Config = systems.Config with
                {
                    WeatherProfiles = Enum.GetValues<SeasonKind>()
                    .Select(season => new WeatherProfile(season, ClearWeight: weather == WeatherKind.Clear ? 1 : 0,
                        CloudyWeight: 0, RainWeight: weather == WeatherKind.Rain ? 1 : 0,
                        StormWeight: 0, SnowWeight: weather == WeatherKind.Snow ? 1 : 0)).ToArray()
                },
                Climate = systems.Climate with { Weather = weather },
            },
        };
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private sealed class HeldRepairPlanningProvider : IDecisionProvider
    {
        private readonly InlineCompletionSource<CognitionDecisionResponse> held = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public int Calls;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult(true);
            return new(held.Task.WaitAsync(cancellationToken));
        }
    }

    private sealed class HeldRepairConversationProvider(IReadOnlySet<string> participants)
        : IDecisionProvider, IAgentConversationProvider
    {
        private readonly Choices planner = new(["conversation_accept:", "conversation_wrapup_accept:"]);
        private readonly InlineCompletionSource<AgentConversationTurnResponse> held = new(TaskCreationOptions.None);
        private AgentConversationTurnRequest? heldRequest;
        private int released;
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public DecisionProviderKind Kind => planner.Kind;
        public long ProviderEpoch => planner.ProviderEpoch;
        public bool CanSpeakAs(string agentId) => participants.Contains(agentId);
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default) => planner.DecideAsync(request, cancellationToken);
        public ValueTask<AgentConversationTurnResponse> SpeakAsync(AgentConversationTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();
            if (Volatile.Read(ref released) != 0)
                return ValueTask.FromResult(Response(request));
            heldRequest = request;
            Started.TrySetResult(true);
            return new(held.Task.WaitAsync(cancellationToken));
        }

        public void ReleaseHeldTurn()
        {
            var request = heldRequest ?? throw new InvalidOperationException("The held conversation turn has not started.");
            Interlocked.Exchange(ref released, 1);
            held.TrySetResult(Response(request));
        }

        private static AgentConversationTurnResponse Response(AgentConversationTurnRequest request) => new(
            request.RequestId, request.ConversationId, request.Revision, request.RunEpoch, request.SpeakerId,
            "We can finish this talk together.", AgentConversationDisposition.Continue);
    }

    private sealed class Choices(IReadOnlyList<string> allowed) : IDecisionProvider
    {
        public List<string> Offers { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Offers.Add(string.Join(",", request.Observation.Candidates.Select(item => item.Id)));
            var permitted = request.Observation.Candidates.Where(item => allowed.Any(prefix => item.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(item => item with
                {
                    DeterministicPriority = allowed.Select((prefix, index) => (prefix, index))
                    .First(entry => item.Id.StartsWith(entry.prefix, StringComparison.Ordinal)).index
                }).ToArray();
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                { Candidates = permitted.Length > 0 ? permitted : [request.Observation.Candidates.Single(item => item.Id == "safe_idle")] }
            }, cancellationToken);
        }
    }
}
