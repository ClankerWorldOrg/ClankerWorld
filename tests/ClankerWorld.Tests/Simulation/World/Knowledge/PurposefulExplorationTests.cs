using System.Text;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class PurposefulExplorationTests
{
    [Fact]
    public async Task UntargetedMaterialOrderKeepsItsPurposePastEightStepsAndHandsOffOnlyAfterDiscoveryAcrossReplay()
    {
        var (state, actor, kind, source, index) = await MaterialCourse();
        using var world = Restore(state);
        var receipt = world.SubmitInstruction(new("purposeful-material", "owner:test", actor,
            OwnerInstructionKind.Order, "gather " + kind.Replace('_', ' ')));
        Assert.Equal("waiting", Order(world, receipt.InstructionId).Status);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var goal = Person(world, actor).Exploration!.Goal;
        Assert.Equal(new ExplorationGoal("resource", kind, receipt.InstructionId), goal);
        Assert.Equal(0, Order(world, receipt.InstructionId).CompletedUnits);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
        var reached = new HashSet<GridPoint> { Person(world, actor).Position };
        var longOuting = false;
        var discovered = false;
        for (var tick = 0; tick < 100 && Order(world, receipt.InstructionId).Status != "finished"; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            reached.Add(Person(world, actor).Position);
            var facts = world.ExportState().Knowledge!.Facts.Where(fact => fact.OwnerId == actor).ToArray();
            Assert.All(facts, fact => Assert.Contains(reached, point => state.Map.FootDistance(fact.Position, point) <= 1));
            discovered |= facts.Any(fact => fact.Position == source.Position) ||
                state.Map.FootDistance(Person(world, actor).Position, source.Position) <= 1;
            var outing = Person(world, actor).Exploration!;
            longOuting |= outing.OutingPath.Count > 9;
            if (!discovered)
            {
                Assert.Equal(goal, outing.Goal);
                Assert.Equal(0, Order(world, receipt.InstructionId).CompletedUnits);
                Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == kind);
            }
        }
        Assert.True(index > 8);
        Assert.True(longOuting);
        Assert.True(discovered);
        Assert.Equal("finished", Order(world, receipt.InstructionId).Status);
        Assert.Contains(world.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == kind && lot.Quantity > 0);
        Assert.Contains(world.ExportState().Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == source.Position);
        Assert.Null(Person(world, actor).Exploration!.Goal);
        Assert.Empty(Person(world, actor).Exploration!.OutingPath);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "exploration_goal_found" && item.Detail == actor + ":resource:" + kind);
        world.Validate();
    }

    [Theory]
    [InlineData("resource", "wood")]
    [InlineData("terrain", "Forest")]
    public async Task OrdinaryProjectNeedsOfferResourceAndTerrainPurposesWithoutRevealingUnseenSites(string kind, string target)
    {
        var (state, actor, _, source, _) = await MaterialCourse();
        state = UnpaidWoodProject(state, actor);
        var chooser = new ScoutProvider($"explore_for:{kind}:{target}");
        using var world = WorldRuntime.Restore(state, id => id == actor ? chooser : new ScoutProvider("safe_idle"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(chooser.Offered.Contains("explore_for:resource:wood"),
            "Missing needed-wood choice: " + string.Join(',', chooser.Offered.Where(id => id.StartsWith("explore", StringComparison.Ordinal))) +
            "; actual project=" + Person(world, actor).Project);
        Assert.Contains("explore_for:terrain:Forest", chooser.Offered);
        Assert.Equal(new ExplorationGoal(kind, target), Person(world, actor).Exploration!.Goal);
        var visible = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == actor);
        Assert.Equal("looking for " + target.ToLowerInvariant() + (kind == "terrain" ? " terrain" : ""), visible.PublicIntention!.Summary);
        Assert.Equal(visible.PublicIntention.Summary, ClankerWorld.Contracts.GameUiText.ActivityPhrase(visible.PublicIntention.CandidateId, null));
        Assert.DoesNotContain(world.ExportState().Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == source.Position);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.False((await loaded.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.ExportState()));
        // Alter the stock of the unseen target only. Candidate IDs, chosen step
        // and saved purpose must not reveal that change.
        var withoutSource = state with
        {
            Resources = state.Resources.Select(item => item.ResourceId == source.Id ? item with { State = ResourceState.Depleted } : item).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(item => item.Id == source.Id
                        ? item with { Quantity = 0, State = EcologyResourceState.Depleted } : item).ToArray(),
                },
            },
        };
        var otherChooser = new ScoutProvider($"explore_for:{kind}:{target}");
        using var hidden = WorldRuntime.Restore(withoutSource, id => id == actor ? otherChooser : new ScoutProvider("safe_idle"));
        Assert.True((await hidden.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(chooser.Offered.Where(id => id.StartsWith("explore", StringComparison.Ordinal)),
            otherChooser.Offered.Where(id => id.StartsWith("explore", StringComparison.Ordinal)));
        Assert.Equal(Person(world, actor).Position, Person(hidden, actor).Position);
        Assert.Equal(Person(world, actor).Exploration!.Goal, Person(hidden, actor).Exploration!.Goal);
        for (var tick = 0; tick < 85 && Person(world, actor).Exploration!.Goal is not null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Null(Person(world, actor).Exploration!.Goal);
        Assert.Empty(Person(world, actor).Exploration!.OutingPath);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "exploration_goal_found" &&
            item.Detail == $"{actor}:{kind}:{target}");
        if (kind == "terrain")
            Assert.Contains(world.ExportState().Knowledge!.Facts, fact => fact.OwnerId == actor &&
                fact.DiscovererId == actor && fact.Acquisition == "firsthand" && fact.Terrain == target &&
                fact.Position == Person(world, actor).Position);
        else
        {
            Assert.True(state.Map.FootDistance(Person(world, actor).Position, source.Position) <= 1 ||
                world.ExportState().Knowledge!.Facts.Any(fact => fact.OwnerId == actor && fact.Position == source.Position));
            Assert.Equal(1, world.WorldSystems.Ecology.GetResource(source.Id).Quantity);
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == "wood");
        }
        var completed = WorldRuntimeCodec.Encode(world.ExportState());
        using var completedReload = Restore(WorldRuntimeCodec.Decode(completed));
        Assert.Equal(completed, WorldRuntimeCodec.Encode(completedReload.ExportState()));
    }

    [Fact]
    public async Task FoodOrderRetainsItsKindThroughNonmatchingDiscoveriesAndCancellationReturnsWithoutCredit()
    {
        var (state, actor, _, _, _) = await MaterialCourse();
        var chooser = new ScoutProvider("safe_idle");
        using var world = WorldRuntime.Restore(state, id => id == actor ? chooser : new ScoutProvider("safe_idle"));
        var receipt = world.SubmitInstruction(new("find-fruit", "owner:test", actor, OwnerInstructionKind.Order, "harvest fruit"));
        for (var tick = 0; tick < 40 && (Person(world, actor).Exploration?.OutingPath.Count ?? 0) < 12; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(new ExplorationGoal("resource", "fruit", receipt.InstructionId), Person(world, actor).Exploration!.Goal);
        Assert.True(Person(world, actor).Exploration!.OutingPath.Count > 8);
        Assert.Contains(world.ExportState().Knowledge!.Facts, fact => fact.OwnerId == actor && fact.ResourceKinds.Contains("construction"));
        Assert.Equal(0, Order(world, receipt.InstructionId).CompletedUnits);
        var origin = Person(world, actor).Exploration!.OutingPath[0];
        world.CancelOrder(new("stop-fruit", "owner:test", world.Society.WorldId, actor, receipt.InstructionId));
        chooser.Preferred = "explore_return";
        world.SubmitInstruction(new("return-after-fruit", "owner:test", actor, OwnerInstructionKind.Suggestion, "Please return to where the trip started."));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved),
            id => new ScoutProvider(id == actor ? "explore_return" : "safe_idle"));
        for (var tick = 0; tick < 50 && Person(world, actor).Exploration!.OutingPath.Count > 0; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal(origin, Person(world, actor).Position);
        Assert.Empty(Person(world, actor).Exploration!.OutingPath);
        Assert.Null(Person(world, actor).Exploration!.Goal);
        Assert.Equal(("cancelled", 0), (Order(world, receipt.InstructionId).Status, Order(world, receipt.InstructionId).CompletedUnits));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "exploration_goal_found" && item.Detail.EndsWith(":fruit", StringComparison.Ordinal));
        world.Validate();
    }

    [Fact]
    public async Task ExplicitFoodTargetReplacesAnUntargetedPurposeWithoutCreditingAnUnrelatedDiscovery()
    {
        var (state, actor, _, source, _) = await MaterialCourse();
        using var world = Restore(state);
        var original = world.SubmitInstruction(new("fruit-before-target", "owner:test", actor, OwnerInstructionKind.Order, "harvest fruit"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("fruit", Person(world, actor).Exploration!.Goal!.Target);
        var targeted = world.SubmitInstruction(new("exact-fruit-site", "owner:test", actor, OwnerInstructionKind.Order,
            $"harvest fruit at ({source.Position.X}, {source.Position.Y})"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Null(Person(world, actor).Exploration!.Goal);
        Assert.Equal(source.Position, Order(world, targeted.InstructionId).TargetPosition);
        Assert.Equal(0, Order(world, targeted.InstructionId).CompletedUnits);
        Assert.Equal("cancelled", Order(world, original.InstructionId).Status);
        Assert.Equal(1, world.WorldSystems.Ecology.GetResource(source.Id).Quantity);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    [Fact]
    public async Task FoodNeedOffersAPurposeAlongsideCuriosityWithoutAnOrder()
    {
        var (state, actor, _, _, _) = await MaterialCourse();
        var household = state.Society.Society.GetAgent(actor).HouseholdId;
        state = FarmFieldTests.WithInventory(state, state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != household &&
                (lot.OwnerId != actor || lot.ItemKind is "basket" or "wooden_axe" or "wooden_pickaxe")).ToArray(),
        }) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { FullnessBasisPoints = 6_000 } : person).ToArray(),
        };
        var chooser = new ScoutProvider("explore_for:resource:food");
        using var world = WorldRuntime.Restore(state, id => id == actor ? chooser : new ScoutProvider("safe_idle"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains("explore", chooser.Offered);
        Assert.Contains("explore_for:resource:food", chooser.Offered);
        Assert.Equal(new ExplorationGoal("resource", "food"), Person(world, actor).Exploration!.Goal);
        Assert.Empty(world.ExportState().Instructions!);
        world.Validate();
    }

    [Fact]
    public async Task StrictReloadRejectsForgedPurposesAndOrderLinksAndPreservesEarlierAlphaBytes()
    {
        var (state, actor, _, _, _) = await MaterialCourse();
        using var world = Restore(state);
        world.SubmitInstruction(new("validate-goal", "owner:test", actor, OwnerInstructionKind.Order, "gather wood"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var peer = world.Agents.First(person => person.AgentId != actor).AgentId;
        var peerOrder = world.SubmitInstruction(new("peer-goal", "owner:test", peer, OwnerInstructionKind.Order, "gather wood"));
        state = world.ExportState();
        foreach (var goal in new ExplorationGoal[]
        {
            new("resource", "invented"), new("terrain", "Ocean"), new("resource", "wood", "missing-order"),
            new("resource", "clay", Person(world, actor).Exploration!.Goal!.OrderInstructionId),
            new("resource", "wood", peerOrder.InstructionId),
        })
        {
            var invalid = state with
            {
                Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Exploration = person.Exploration! with { Goal = goal } } : person).ToArray()
            };
            Assert.Throws<InvalidDataException>(() => Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(invalid))));
        }
        var missingGoal = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        var actorNode = missingGoal["state"]!["agents"]!.AsArray()
            .Single(person => person!["agentId"]!.GetValue<string>() == actor)!;
        actorNode["exploration"]!.AsObject().Remove("goal");
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(Encoding.UTF8.GetBytes(missingGoal.ToJsonString())));
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        document["state"]!["schemaVersion"] = WorldRuntime.StateSchemaVersion - 1;
        var previous = Encoding.UTF8.GetBytes(document.ToJsonString());
        var preserved = previous.ToArray();
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(previous));
        Assert.Equal(preserved, previous);
    }

    private static readonly Lazy<Task<(byte[] State, string Actor, string Kind, MapResource Source, int Index)>> MaterialCheckpoint = new(CreateMaterialCheckpoint);

    private static async Task<(WorldRuntimeState State, string Actor, string Kind, MapResource Source, int Index)> MaterialCourse()
    {
        var saved = await MaterialCheckpoint.Value;
        return (WorldRuntimeCodec.Decode(saved.State), saved.Actor, saved.Kind, saved.Source, saved.Index);
    }

    private static async Task<(byte[] State, string Actor, string Kind, MapResource Source, int Index)> CreateMaterialCheckpoint()
    {
        using var setup = NormalPathWorld.CreateGenerated("purposeful-scouting", _ => new ScoutProvider("safe_idle"));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = WeatherTestFixture.WithWeather(setup.ExportState(), WeatherKind.Clear);
        var actor = state.Agents[0].AgentId;
        var inventory = state.Society.Society.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, "scout-basket", "basket", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "scout-axe", "wooden_axe", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "scout-pick", "wooden_pickaxe", actor, 1);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 10_000,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
                TravelCooldownTicks = 0,
                Exploration = null,
                Equipment = person.AgentId == actor ? new(CarryAidLotId: "scout-basket") : person.Equipment,
            }).ToArray(),
            Knowledge = state.Knowledge! with { Facts = state.Knowledge.Facts.Where(fact => fact.OwnerId != actor).ToArray() },
        };
        // Use a real curiosity outing to locate the fixture's future observation.
        // Neither the implementation nor its requested purpose receives that position.
        using var scouting = WorldRuntime.Restore(state, id => new ScoutProvider(id == actor ? "explore" : "safe_idle"));
        for (var tick = 0; tick < 65 && (Person(scouting, actor).Exploration?.OutingPath.Count ?? 0) < 35; tick++)
            Assert.True((await scouting.AdvanceOneTickAsync()).Advanced);
        var path = Person(scouting, actor).Exploration!.OutingPath;
        var candidates = state.Map.Resources.Where(source => source.Kind is "wood" or "construction" && path.Contains(source.Position))
            .Select(source => (Kind: "wood", Source: source, Index: path.ToList().IndexOf(source.Position)))
            .Where(item => item.Index > 12).OrderBy(item => item.Index).ToArray();
        Assert.True(candidates.Length > 0, "The generated real scouting path must first discover a supported material after eight steps: " +
            string.Join(',', path.Select((point, at) => $"{at}:{string.Join('/', state.Map.Resources.Where(source => source.Position == point).Select(source => source.Kind))}")));
        var target = candidates[0];
        // Controlled stock boundary: only this existing generated tree has wood
        // left. Earlier observed empty trees must not finish the search.
        var exhausted = state.Map.Resources.Where(source => source.Kind is "wood" or "construction" && source.Id != target.Source.Id)
            .Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        state = state with
        {
            Resources = state.Resources.Select(resource => exhausted.Contains(resource.ResourceId)
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource => exhausted.Contains(resource.Id)
                        ? resource with { Quantity = 0, State = EcologyResourceState.Depleted } : resource).ToArray(),
                },
            },
        };
        return (WorldRuntimeCodec.Encode(state), actor, target.Kind, target.Source, target.Index);
    }

    private static AgentState Person(WorldRuntime world, string actor) => world.Agents.Single(person => person.AgentId == actor);
    private static OrderTask Order(WorldRuntime world, string id) => world.ExportState().Instructions!.Single(item => item.InstructionId == id).Task!;
    private static WorldRuntime Restore(WorldRuntimeState state) => WorldRuntime.Restore(state, _ => new ScoutProvider("safe_idle"));

    private sealed class ScoutProvider(string preferred) : IDecisionProvider
    {
        public string Preferred { get; set; } = preferred;
        public IReadOnlyList<string> Offered { get; private set; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Offered = request.Observation.Candidates.Select(item => item.Id).ToArray();
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id == Preferred)
                ?? request.Observation.Candidates.FirstOrDefault(item => item.Id == "safe_idle")
                ?? request.Observation.Candidates[0];
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [candidate] } }, cancellationToken);
        }
    }
}
