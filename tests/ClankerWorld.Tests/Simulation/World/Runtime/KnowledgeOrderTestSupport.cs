using OwnerInstructionReceipt = ClankerWorld.Simulation.World.OwnerInstructionReceipt;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class KnowledgeOrderTestSupport
{
    public static async Task<WorldRuntimeState> PrepareCore(byte[] baseline, bool learn,
        Func<WorldRuntimeState, WorldRuntime> restore,
        Func<WorldRuntime, string, string, bool, OwnerInstructionReceipt> submit)
    {
        var state = WorldRuntimeCodec.Decode(baseline);
        using (var initial = WorldRuntime.Restore(state, _ => new Idle()))
        {
            Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
            state = initial.ExportState();
        }
        var actor = state.Agents[0].AgentId;
        using var geometry = WorldRuntime.Restore(state, _ => new Idle());
        var occupied = state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building))
            .Concat(geometry.RoadTiles).Concat(state.Map.Resources.Select(resource => resource.Position))
            .Concat(state.Map.MapObjects.Select(item => item.Position)).ToHashSet();
        var target = state.Map.Tiles.Select(tile => tile.Position).Where(point => !occupied.Contains(point) &&
                !state.Towns!.Any(town => town.BorderTiles.Contains(point)) &&
                TreeGrowthRules.GroundRefusal(state.Map, point) is null &&
                state.Map.FootNeighbors(point).Any(next => state.Agents.All(person => person.Position != next)) &&
                state.Map.IsReachableOnFoot(state.Agents[0].Position, point))
            .OrderBy(point => state.Map.FootDistance(state.Agents[0].Position, point)).First();
        var stand = state.Map.FootNeighbors(target).First(point => state.Agents.All(person => person.Position != point));
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = stand,
                FullnessBasisPoints = 9500,
                TravelCooldownTicks = 0,
                LastDecisionContext = null,
                Project = null,
            } : person).ToArray(),
        };
        if (!learn) return state;
        using var world = restore(state);
        foreach (var destination in new[] { target, stand })
        {
            var receipt = submit(world, "learn-site-" + destination, $"move to {destination.X},{destination.Y}", false);
            for (var tick = 0; tick < 12 && Order(world, receipt).Status != "finished"; tick++)
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal("finished", Order(world, receipt).Status);
        }
        Assert.Equal(2, world.ExportState().Knowledge!.Facts.Count);
        return world.ExportState();
    }

    public static WorldRuntimeState Supplies(WorldRuntimeState state, string owner, int paper, int cloth)
    {
        var inventory = state.Society.Society.Inventory;
        if (paper > 0) inventory = InventoryAuthority.AddLot(inventory, "ordered-paper", "paper", owner, paper, inventory.WorldTick);
        if (cloth > 0) inventory = InventoryAuthority.AddLot(inventory, "ordered-cloth", "cloth", owner, cloth, inventory.WorldTick);
        return FarmFieldTests.WithInventory(state, inventory);
    }

    public static OrderTask Order(WorldRuntime world, OwnerInstructionReceipt receipt) =>
        world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Task!;

    public static int Quantity(WorldRuntimeState state, string owner, string kind) =>
        state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == owner && lot.ItemKind == kind).Sum(lot => lot.Quantity);

    public static void AssertUnmovedLot(InventoryLot before, InventoryLot after) =>
        Assert.Equal(before with { LastProcessedTick = after.LastProcessedTick }, after);

    public static void AssertReload(WorldRuntimeState state)
    {
        var bytes = WorldRuntimeCodec.Encode(state);
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        loaded.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    public static async Task FinishTogether(WorldRuntime world, OwnerInstructionReceipt receipt,
        Func<WorldRuntimeState, WorldRuntime> restore)
    {
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = restore(WorldRuntimeCodec.Decode(bytes));
        for (var tick = 0; tick < 32 && Order(world, receipt).Status != "finished"; tick++)
        {
            if (world.ExportState().Knowledge!.WritingProjects.FirstOrDefault(project =>
                    project.OrderInstructionId == receipt.InstructionId) is { } finishing &&
                finishing.WorkDone + 1 == finishing.WorkRequired)
            {
                var beforeCompletion = WorldRuntimeCodec.Encode(world.ExportState());
                Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
                Assert.Equal(beforeCompletion, WorldRuntimeCodec.Encode(world.ExportState()));
            }
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal("finished", Order(world, receipt).Status);
    }

    public sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [request.Observation.Candidates.Single(item => item.Id == "safe_idle")] },
            }, cancellationToken);
    }

    public sealed class Choose(string choice) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                {
                    Candidates = [request.Observation.Candidates.FirstOrDefault(item => item.Id == choice) ??
                        request.Observation.Candidates.Single(item => item.Id == "safe_idle")],
                },
            }, cancellationToken);
    }
}
