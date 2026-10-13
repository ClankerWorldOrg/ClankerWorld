using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ExplorationHandcartTests
{
    private static readonly GridPoint Start = new(234, 55);
    private static readonly GridPoint Mountain = new(233, 54);
    private static readonly Lazy<Task<WorldRuntimeState>> Prepared = new(PrepareCart);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativeScoutUsesLegalCartExitsAndRecordsOnlyActualMovementAcrossReload(bool attached)
    {
        var state = await Prepared.Value;
        var cart = Assert.Single(state.Society.Society.Inventory.Lots, lot => lot.ItemKind == "handcart");
        var actor = cart.OwnerId;
        var inventory = InventoryAuthority.Relocate(state.Society.Society.Inventory, "scout-position", cart.Id, actor, 1,
            groundPosition: new(Start.X, Start.Y));
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? Start : person.Position,
                FullnessBasisPoints = 9_500,
                Survival = new(),
                LastDecisionContext = null,
                Project = null,
                TravelCooldownTicks = 0,
                MoveWaitTicks = 0,
                Exploration = null,
            }).ToArray(),
        };
        Assert.Equal(TerrainKind.Mountain, state.Map.Tiles.Single(tile => tile.Position == Mountain).Terrain);
        Assert.True(state.Map.CanFootStep(Start, new(234, 54)));
        Assert.DoesNotContain(state.RoadTiles!, point => point == Mountain);
        Assert.DoesNotContain(state.Agents.Where(person => person.AgentId != actor),
            person => state.Map.FootDistance(Start, person.Position) <= 1);
        var chooser = new ScoutChooser(attached ? "attach_handcart:" + cart.Id : "safe_idle");
        using var initial = WorldRuntime.Restore(state, id => id == actor ? chooser : new ScoutChooser("safe_idle"));
        if (attached)
            await Until(initial, () => initial.ExportState().HandcartHitches!.Count == 1, 20);
        chooser.Preferred = "explore";
        var bytes = WorldRuntimeCodec.Encode(initial.ExportState());
        var scout = new ScoutChooser("explore");
        var replayScout = new ScoutChooser("explore");
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? scout : new ScoutChooser("safe_idle"));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? replayScout : new ScoutChooser("safe_idle"));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        var moves = 0;
        for (var tick = 0; tick < 40; tick++)
        {
            var before = world.Agents.Single(person => person.AgentId == actor);
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            var after = world.Agents.Single(person => person.AgentId == actor);
            var previousVisited = before.Exploration?.VisitedTiles ?? [];
            Assert.All(after.Exploration!.VisitedTiles, point => Assert.True(previousVisited.Contains(point) ||
                point == before.Position || point == after.Position));
            if (after.Position != before.Position)
            {
                moves++;
                Assert.True(state.Map.CanFootStep(before.Position, after.Position));
                Assert.Contains(after.Position, after.Exploration.VisitedTiles);
                if (attached)
                {
                    Assert.False(state.Map.IsDiagonalFootStep(before.Position, after.Position));
                    Assert.NotEqual(TerrainKind.Mountain, state.Map.Tiles.Single(tile => tile.Position == after.Position).Terrain);
                    Assert.True(state.Map.ElevationAt(after.Position) < WorldMap.MountainElevationThreshold);
                }
            }
            if (attached)
                Assert.Equal(new InventoryGroundPosition(after.Position.X, after.Position.Y), world.Society.Inventory.GetLot(cart.Id).GroundPosition);
            else
                Assert.Equal(new InventoryGroundPosition(Start.X, Start.Y), world.Society.Inventory.GetLot(cart.Id).GroundPosition);
            // Two outward steps exercise legal exits and a multi-step return;
            // a longer outing repeats the same movement and memory checks.
            if (moves >= 2 && scout.Preferred == "explore")
            {
                scout.Preferred = replayScout.Preferred = "explore_return";
                var suggestion = new OwnerInstructionRequest("return-with-cart", "owner:test", actor,
                    OwnerInstructionKind.Suggestion, "Please return to where you started scouting.");
                world.SubmitInstruction(suggestion);
                replay.SubmitInstruction(suggestion);
            }
            world.Validate();
            if (world.ExportState().Events.Any(item => item.Kind == "exploration_completed" &&
                item.Detail.StartsWith(actor + ":", StringComparison.Ordinal)))
                break;
        }
        var result = world.Agents.Single(person => person.AgentId == actor);
        Assert.True(moves > 0 || result.Exploration!.OutingPath.Count == 0,
            "A scout must take the available legal cart exit or end the outing within 40 native ticks.");
        Assert.True(moves >= 4, "The scout must make two outward steps and return along the actual path.");
        Assert.Contains(world.ExportState().Events, item => item.Kind == "exploration_completed" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        Assert.Equal(Start, result.Position);
        Assert.Equal(actor, world.Society.Inventory.GetLot(cart.Id).OwnerId);
        Assert.Equal(1, world.Society.Inventory.GetLot(cart.Id).Quantity);
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), _ => new ScoutChooser("safe_idle"));
        Assert.Equal(final, WorldRuntimeCodec.Encode(loaded.ExportState()));
        loaded.Validate();
    }

    private static async Task<WorldRuntimeState> PrepareCart()
    {
        using var setup = NormalPathWorld.CreateGenerated("normal-visible-handcart", _ => new ScoutChooser("safe_idle"));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        // Crafting/payment is covered by NormalCraftLoadPullSaveParkAndUnloadConservePhysicalMaterials.
        // This fixture starts at the independent attachment/scouting boundary on unchanged generated terrain.
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "scout-cart", "handcart", actor, 1,
            groundPosition: new(Start.X, Start.Y));
        return state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
    }

    private static async Task Until(WorldRuntime world, Func<bool> done, int limit)
    {
        for (var tick = 0; tick < limit && !done(); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(done());
    }

    private sealed class ScoutChooser(string preferred) : IDecisionProvider
    {
        public string Preferred { get; set; } = preferred;
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var choice = request.Observation.Candidates.FirstOrDefault(item => item.Id == Preferred) ??
                request.Observation.Candidates.FirstOrDefault(item => item.Id == "continue_project") ??
                request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
