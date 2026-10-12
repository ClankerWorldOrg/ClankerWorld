using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MarketWritingPaperTests
{
    private const string SalePaper = "writer-market-paper";
    private const string Fiber = "writer-paper-fiber";
    private const string Jug = "writer-paper-jug";
    private const string Water = "writer-paper-water";
    private static readonly Lazy<Task<byte[]>> Prepared = new(PrepareAsync);

    [Theory]
    [InlineData("active", 1, 4)]
    [InlineData("missing", 1, 0)]
    [InlineData("left", 0, 3)]
    public async Task HouseholdMakesReplacementPaperWithoutTakingItsMarketStock(
        string boundary, int expectedJobs, int expectedSalePaper)
    {
        var state = WorldRuntimeCodec.Decode(await Prepared.Value);
        var seller = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, seller);
        var writer = Writer(state, seller, household);
        var receipt = Assert.Single(PaidMarketWorld.Market(state).StockReceipts);
        if (boundary == "missing")
            state = PaidMarketWorld.WithInventory(state, state.Society.Society.Inventory with
            {
                Lots = state.Society.Society.Inventory.Lots.Where(lot => !MarketTradeRules.IsReceiptLot(receipt, lot)).ToArray(),
            });
        if (boundary == "left")
        {
            using var leaving = WorldRuntime.Restore(state, Policy(seller, "market_leave:").CreateProvider);
            for (var tick = 0; tick < 12 && PaidMarketWorld.Market(leaving).Occupancies.Any(item => item.EndedTick is null); tick++)
                Assert.True((await leaving.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(PaidMarketWorld.Market(leaving).Occupancies, item => item.EndedTick is null);
            state = FreshDecisions(leaving.ExportState());
        }
        var recipe = state.WorldContent!.Recipes.Single(item => item.LocalId == "house-paper");
        var recipeCandidate = TownConstructionCandidateIds.Recipe(recipe.CanonicalId);
        var choices = Policy(writer, recipeCandidate, "knowledge_write:field_record", "knowledge_continue");
        using var world = WorldRuntime.Restore(state, choices.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            Policy(writer, recipeCandidate, "knowledge_write:field_record", "knowledge_continue").CreateProvider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 70 && world.ExportState().Knowledge!.Artifacts.Count == 0; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var final = world.ExportState();
        var record = Assert.Single(final.Knowledge!.Artifacts);
        Assert.Equal((writer, "field_record"), (record.CreatorId, record.Kind));
        Assert.Contains(choices.OfferedTo(writer), item => item.Id == "knowledge_write:field_record");
        Assert.All(record.Facts, fact => Assert.Contains(final.Knowledge.Facts,
            learned => learned.OwnerId == writer && AgentKnowledgeRules.SameDiscovery(learned, fact)));
        var material = Assert.Single(record.Materials);
        Assert.Equal(("paper", 1), (material.ItemKind, material.Quantity));
        Assert.Equal(InventoryReservationState.Completed, final.Society.Society.Inventory.GetReservation(material.ReservationId).State);
        Assert.Equal(1, final.Society.Society.Inventory.GetLot(record.LotId).Quantity);
        var jobs = final.WorldSimulation!.ProductionJobs.Where(item => item.RecipeId == recipe.CanonicalId).ToArray();
        Assert.Equal(expectedJobs, jobs.Length);
        Assert.All(jobs, job => Assert.Equal(WorldProductionJobState.Completed, job.State));
        Assert.Equal(expectedSalePaper, final.Society.Society.Inventory.Lots.Where(lot => MarketTradeRules.IsReceiptLot(receipt, lot)).Sum(lot => lot.Quantity));
        Assert.Equal(boundary == "left" ? 0 : 1, PaidMarketWorld.Market(final).Occupancies.Count(item => item.EndedTick is null));
        Assert.Equal(1, final.Society.Society.Inventory.GetLot(Jug).Quantity);
        Assert.Equal(expectedJobs == 0 ? 2 : 0, final.Society.Society.Inventory.Lots.Where(lot => lot.Id == Fiber).Sum(lot => lot.Quantity));
        Assert.Equal(expectedJobs == 0 ? 1 : 0, final.Society.Society.Inventory.Lots.Where(lot => lot.Id == Water).Sum(lot => lot.Quantity));
        Assert.Equal(expectedSalePaper + expectedJobs, final.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == "paper").Sum(lot => lot.Quantity));
        Assert.Equal(boundary != "left", choices.OfferedTo(writer).Any(item => item.Id == recipeCandidate));
        AssertReload(world);
    }

    [Theory]
    [InlineData("house")]
    [InlineData("collected")]
    [InlineData("reserved")]
    public async Task AvailableCollectedAndReservedSheetsStillPreventReplacementProduction(string reserve)
    {
        var state = WorldRuntimeCodec.Decode(await Prepared.Value);
        var seller = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, seller);
        var writer = Writer(state, seller, household);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "writer-reserve-paper", "paper",
            reserve == "house" ? household : writer, 2,
            storageBuildingId: reserve == "house" ? PaidMarketWorld.HouseOf(state, household).InstanceId : null);
        if (reserve == "reserved")
            inventory = InventoryAuthority.Reserve(inventory, "writer-reserve-commitment", writer, "writer-reserve-paper", 2,
                AgentKnowledgeRules.MaterialPurpose("writer-reserve-book", "paper"), state.Society.Society.WorldTick + 200);
        state = PaidMarketWorld.WithInventory(state, inventory);
        var recipe = state.WorldContent!.Recipes.Single(item => item.LocalId == "house-paper");
        var candidate = TownConstructionCandidateIds.Recipe(recipe.CanonicalId);
        var choices = Policy(writer, candidate);
        using var world = WorldRuntime.Restore(state, choices.CreateProvider);
        for (var tick = 0; tick < 8; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(choices.OfferedTo(writer), item => item.Id == candidate);
        Assert.DoesNotContain(world.WorldSimulation.ProductionJobs, item => item.RecipeId == recipe.CanonicalId);
        Assert.Equal(2, world.Society.Inventory.GetLot("writer-reserve-paper").Quantity);
        Assert.Equal(2, world.Society.Inventory.GetLot(Fiber).Quantity);
        Assert.Equal(1, world.Society.Inventory.GetLot(Water).Quantity);
        Assert.Empty(world.Knowledge.Artifacts);
        if (reserve == "reserved")
            Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("writer-reserve-commitment").State);
        AssertReload(world);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritingDemandOnlyCountsCommittedSheetsInAUsableVessel(bool broken)
    {
        var state = WorldRuntimeCodec.Decode(await Prepared.Value);
        var seller = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, seller);
        var writer = Writer(state, seller, household);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "writer-covered-cart", "handcart", writer, 1,
            groundPosition: new(PaidMarketWorld.HouseOf(state, household).Position.X, PaidMarketWorld.HouseOf(state, household).Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "writer-covered-sheets", "paper", writer, 2,
            containerLotId: "writer-covered-cart");
        inventory = InventoryAuthority.Reserve(inventory, "writer-covered-book", writer, "writer-covered-sheets", 2,
            AgentKnowledgeRules.MaterialPurpose("writer-covered-book", "paper"), long.MaxValue);
        if (broken)
            inventory = inventory with
            {
                Lots = inventory.Lots.Select(lot => lot.Id == "writer-covered-cart"
                    ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
            };
        state = FreshDecisions(PaidMarketWorld.WithInventory(state, inventory));
        var recipe = state.WorldContent!.Recipes.Single(item => item.LocalId == "house-paper");
        var candidate = TownConstructionCandidateIds.Recipe(recipe.CanonicalId);
        var choices = Policy(writer, candidate);
        var bytes = WorldRuntimeCodec.Encode(state);
        var replayChoices = Policy(writer, candidate);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id =>
            id == writer ? new WritingIdentityProvider(choices.CreateProvider(id)) : choices.CreateProvider(id));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id =>
            id == writer ? new WritingIdentityProvider(replayChoices.CreateProvider(id)) : replayChoices.CreateProvider(id));
        var guidance = new OwnerInstructionRequest("writer-covered-guidance", "owner:test", writer,
            OwnerInstructionKind.Suggestion, "Consider making paper if the household needs usable sheets for writing.");
        Assert.Equal(world.SubmitInstruction(guidance), replay.SubmitInstruction(guidance));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickNonBlockingAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 32; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForWritingReplies(world);
            Assert.True((await replay.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForWritingReplies(replay);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.NotEmpty(choices.OfferedTo(writer));
        Assert.False(world.ExportState().Agents.Single(person => person.AgentId == writer).IdentityChoicePending);
        Assert.Equal(broken, choices.OfferedTo(writer).Any(item => item.Id == candidate));
        Assert.Equal(broken, world.WorldSimulation.ProductionJobs.Any(item => item.RecipeId == recipe.CanonicalId));
        Assert.Equal(2, world.Society.Inventory.GetLot("writer-covered-sheets").Quantity);
        Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("writer-covered-book").State);
        Assert.Equal(broken ? 0 : 10_000, world.Society.Inventory.GetLot("writer-covered-cart").ConditionBasisPoints);
        Assert.Empty(world.Knowledge.Artifacts);
        AssertReload(world);
    }

    private static async Task WaitForWritingReplies(WorldRuntime world)
    {
        var tasks = new List<Task>();
        foreach (var name in new[] { "pendingModelCalls", "pendingIdentityMoments" })
        {
            var pending = (System.Collections.IDictionary)typeof(WorldRuntime)
                .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
            foreach (var item in pending.Values)
                tasks.Add((Task)item!.GetType().GetProperty("Task")!.GetValue(item)!);
        }
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class WritingIdentityProvider(IDecisionProvider inner) : IDecisionProvider
    {
        public DecisionProviderKind Kind => inner.Kind;
        public long ProviderEpoch => inner.ProviderEpoch;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Observation.IdentityMoment is not null)
            {
                var candidate = request.Observation.Candidates.Single(item => item.Id == "identity_optional");
                return new(request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                    request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                    candidate.Id, 1, new Dictionary<string, double> { [candidate.Id] = 1 });
            }
            var response = await inner.DecideAsync(request, cancellationToken);
            return response with
            {
                ChosenName = request.Observation.NeedsName ? "Fieldwriter" : null,
                ChosenPersonality = request.Observation.NeedsPersonality ? "Careful and curious" : null,
                ChosenAspiration = request.Observation.NeedsAspiration ? "Write useful field notes" : null,
            };
        }
    }

    private static async Task<byte[]> PrepareAsync()
    {
        var state = await PaidMarketWorld.StateAsync();
        var seller = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, seller);
        var writer = Writer(state, seller, household);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, SalePaper, "paper", household, 4);
        inventory = InventoryAuthority.Relocate(inventory, "writer-market-load", SalePaper, household, 4, carrierId: seller);
        state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), seller,
            MarketContent.StallEntrance(PaidMarketWorld.Market(state).Site, 0)) with
        { JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off };
        using (var stocking = WorldRuntime.Restore(state, Policy(seller, "market_borrow:", "market_deposit:").CreateProvider))
        {
            for (var tick = 0; tick < 30 && PaidMarketWorld.Market(stocking).StockReceipts.Count == 0; tick++)
                Assert.True((await stocking.AdvanceOneTickAsync()).Advanced);
            var receipt = Assert.Single(PaidMarketWorld.Market(stocking).StockReceipts);
            Assert.Equal((household, "paper", 4), (receipt.OwnerId, receipt.ItemKind, receipt.Quantity));
            state = FreshDecisions(stocking.ExportState());
        }
        using (var exploring = WorldRuntime.Restore(state, Policy(writer, "explore").CreateProvider))
        {
            for (var tick = 0; tick < 80 && !exploring.ExportState().Knowledge!.Facts.Any(fact => fact.OwnerId == writer); tick++)
                Assert.True((await exploring.AdvanceOneTickAsync()).Advanced);
            state = FreshDecisions(exploring.ExportState());
            Assert.Contains(state.Knowledge!.Facts, fact => fact.OwnerId == writer && fact.Acquisition == "firsthand");
        }
        var house = PaidMarketWorld.HouseOf(state, household);
        inventory = state.Society.Society.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, Fiber, "fiber", household, 2, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, Jug, "water_jug", household, 1, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, Water, "fresh_water", household, 1,
            storageBuildingId: house.InstanceId, containerLotId: Jug);
        state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), writer, house.Position);
        using var validated = WorldRuntime.Restore(state);
        validated.Validate();
        return WorldRuntimeCodec.Encode(validated.ExportState());
    }

    private static string Writer(WorldRuntimeState state, string seller, string household) =>
        state.Society.Society.Agents.First(person => person.HouseholdId == household && person.Id != seller).Id;

    private static WorldRuntimeState FreshDecisions(WorldRuntimeState state) => state with
    {
        Agents = state.Agents.Select(person => person with
        { FullnessBasisPoints = 9_500, Survival = new SurvivalCondition(), LastDecisionContext = null, Project = null }).ToArray(),
    };

    private static MarketRulesPolicy Policy(string selectedActor, params string[] actions) => new()
    {
        Choose = (actor, candidates) => actor == selectedActor
            ? actions.Select(prefix => candidates.FirstOrDefault(candidate => candidate.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .FirstOrDefault(candidate => candidate is not null) ?? candidates.Single(candidate => candidate.Id == "safe_idle")
            : candidates.Single(candidate => candidate.Id == "safe_idle"),
    };

    private static void AssertReload(WorldRuntime world)
    {
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
