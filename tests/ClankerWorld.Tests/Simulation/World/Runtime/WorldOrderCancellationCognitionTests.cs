using OwnerInstructionReceipt = ClankerWorld.Simulation.World.OwnerInstructionReceipt;
using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldRuntimeTests
{
    [Fact]
    public async Task CancellingAnAdmittedTravelOrderRequestsOrdinaryPlanningInsteadOfContinuingItsSeekFood()
    {
        var provider = new CancellationPlanningProvider();
        using var world = CreateCancellationTravelWorld(provider);
        var before = world.ExportState();
        var order = SubmitCancellationTravelOrder(world, "admitted-cancel-travel");

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var admitted = world.ExportState();
        var intention = Assert.Single(admitted.Society.Cognition.Runtimes,
            item => item.AgentId == HarvestInstructionActor).CurrentIntention!;
        Assert.Equal("seek_food", intention.CandidateId);
        Assert.Equal(order.InstructionId.Value, intention.OperativeOrderInstructionId);
        var position = CancellationActorPosition(admitted);
        Assert.NotEqual(CancellationActorPosition(before), position);
        Assert.Equal("doing", CancellationOrder(admitted, order.InstructionId).Status);

        CancelTravelOrder(world, order.InstructionId, "cancel-admitted-travel");
        for (var tick = 0; tick < 5; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(position, CancellationActorPosition(world.ExportState()));
        }

        Assert.Single(provider.Requests, request => request.OperativeOrderInstructionId == order.InstructionId);
        var ordinary = Assert.Single(provider.Requests, request => request.OperativeOrderInstructionId is null);
        // Food seeking remains legal: the stop comes from a fresh personal idle
        // choice, rather than fullness, arrival, depletion or a blocked route.
        Assert.Contains(ordinary.Candidates, candidate => candidate.Id == "seek_food");
        Assert.Equal("cancelled", CancellationOrder(world.ExportState(), order.InstructionId).Status);
        Assert.Equal(0, CancellationOrder(world.ExportState(), order.InstructionId).CompletedUnits);
        world.Validate();
    }

    [Fact]
    public async Task ACancelledHeldTravelReplyCannotInstallSeekFoodThatMovesOnFollowingTicks()
    {
        var provider = new CancellationPlanningProvider(holdFirstOrder: true);
        using var world = CreateCancellationTravelWorld(provider);
        var order = SubmitCancellationTravelOrder(world, "held-cancel-travel");
        try
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            var position = CancellationActorPosition(world.ExportState());
            Assert.Equal("doing", CancellationOrder(world.ExportState(), order.InstructionId).Status);
            CancelTravelOrder(world, order.InstructionId, "cancel-held-travel");

            provider.Release.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitAsync(provider.Returned.Task);
            // The fresh ordinary request starts on a background task, so a slow runner
            // may need a few more ticks; the agent must stay put on every one of them.
            for (var tick = 0; tick < 8 || tick < 60 &&
                     !provider.Requests.Any(request => request.OperativeOrderInstructionId is null); tick++)
            {
                Assert.True((await world.TickModelWorkAsync()).Advanced);
                Assert.Equal(position, CancellationActorPosition(world.ExportState()));
                if (tick >= 8) await ModelWorkHarness.For(world).SettleAsync();
            }

            Assert.Single(provider.Requests, request => request.OperativeOrderInstructionId == order.InstructionId);
            Assert.Single(provider.Requests, request => request.OperativeOrderInstructionId is null);
            Assert.Equal("cancelled", CancellationOrder(world.ExportState(), order.InstructionId).Status);
            Assert.Equal(0, CancellationOrder(world.ExportState(), order.InstructionId).CompletedUnits);
            world.Validate();
        }
        finally
        {
            provider.Release.TrySetResult(true);
        }
    }

    [Fact]
    public async Task CancellingAHeldTravelOrderCanDispatchOrdinaryPlanningBeforeTheObsoleteProviderReturns()
    {
        var provider = new CancellationPlanningProvider(holdFirstOrder: true);
        using var world = CreateCancellationTravelWorld(provider);
        var order = SubmitCancellationTravelOrder(world, "unreturned-cancel-travel");
        try
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            var position = CancellationActorPosition(world.ExportState());
            CancelTravelOrder(world, order.InstructionId, "cancel-unreturned-travel");

            // The ordinary request starts on a background task, so a slow runner may
            // need a few more ticks; the agent must stay put on every one of them.
            for (var tick = 0; tick < 5 || tick < 60 &&
                     !provider.Requests.Any(request => request.OperativeOrderInstructionId is null); tick++)
            {
                Assert.True((await world.TickModelWorkAsync()).Advanced);
                Assert.Equal(position, CancellationActorPosition(world.ExportState()));
                if (tick >= 5) await ModelWorkHarness.For(world).SettleAsync();
            }

            Assert.False(provider.Returned.Task.IsCompleted);
            var ordinary = Assert.Single(provider.Requests, request => request.OperativeOrderInstructionId is null);
            Assert.Contains(ordinary.Candidates, candidate => candidate.Id == "seek_food");
            Assert.Single(provider.Requests, request => request.OperativeOrderInstructionId == order.InstructionId);
            Assert.Equal("cancelled", CancellationOrder(world.ExportState(), order.InstructionId).Status);
            world.Validate();
        }
        finally
        {
            provider.Release.TrySetResult(true);
        }
    }

    [Fact]
    public async Task PausedHeldTravelOrderRefreshesItsPersonalRequestInTheResumedEpoch()
    {
        var provider = new CancellationPlanningProvider(holdFirstOrder: true, holdFreshOrder: true);
        using var world = CreateCancellationTravelWorld(provider);
        var order = SubmitCancellationTravelOrder(world, "paused-held-travel");
        try
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            var originalRequest = Assert.Single(provider.Requests);
            world.Pause();
            var paused = world.ExportState();
            Assert.Contains(paused.Society.Cognition.Queue, entry =>
                entry.AgentId == HarvestInstructionActor &&
                entry.Observation.OperativeOrderInstructionId == order.InstructionId);
            world.Resume();
            // Dispatch does not establish admission. Hold the resumed response
            // so this boundary is exercised on both fast and loaded runners.
            for (var dispatchTick = 0; dispatchTick < 20 && !provider.FreshStarted.Task.IsCompleted; dispatchTick++)
            {
                Assert.True((await world.TickModelWorkAsync()).Advanced);
                await ModelWorkHarness.For(world).SettleAsync();
            }
            Assert.True(provider.FreshStarted.Task.IsCompleted, "The resumed order request was not dispatched.");

            // The resumed request starts in the background; wait for its actual
            // epoch while continuing ticks, without releasing the obsolete reply.
            for (var tick = 0; tick < 200 &&
                 !provider.Requests.Any(request => request.RunEpoch != originalRequest.RunEpoch); tick++)
            {
                await ModelWorkHarness.For(world).SettleAsync();
                Assert.True((await world.TickModelWorkAsync()).Advanced);
            }

            var freshRequest = Assert.Single(provider.Requests, request => request.RunEpoch != originalRequest.RunEpoch);
            Assert.Equal(world.Society.RunEpoch, freshRequest.RunEpoch);
            Assert.Equal(order.InstructionId.Value, freshRequest.OperativeOrderInstructionId);
            Assert.Contains(freshRequest.ObserverGuidance!, message => message.InstructionId == order.InstructionId);
            Assert.Null(Assert.Single(world.ExportState().Instructions!,
                instruction => instruction.InstructionId == order.InstructionId).ObservedTick);
            provider.ReleaseFresh.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitAsync(provider.FreshReturned.Task);
            for (var admissionTick = 0; admissionTick < 20 && Assert.Single(world.ExportState().Instructions!,
                     instruction => instruction.InstructionId == order.InstructionId).ObservedTick is null; admissionTick++)
            {
                await ModelWorkHarness.For(world).SettleAsync();
                Assert.True((await world.TickModelWorkAsync()).Advanced);
            }
            Assert.NotNull(Assert.Single(world.ExportState().Instructions!,
                instruction => instruction.InstructionId == order.InstructionId).ObservedTick);
            Assert.Equal(2, provider.Requests.Count);
            Assert.False(provider.Returned.Task.IsCompleted);
            world.Validate();
        }
        finally
        {
            provider.Release.TrySetResult(true);
            provider.ReleaseFresh.TrySetResult(true);
        }
    }

    private static WorldRuntime CreateCancellationTravelWorld(CancellationPlanningProvider provider)
    {
        using var setup = CreateKnownBerryOrderWorld(_ =>
            new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));
        var state = setup.ExportState();
        var source = Assert.Single(state.Map.Resources, resource => resource.Id == "berry-patch");
        var occupied = state.Agents.Where(person => person.AgentId != HarvestInstructionActor)
            .Select(person => person.Position).ToHashSet();
        occupied.UnionWith(state.WorldSimulation!.Buildings.SelectMany(building =>
            WorldContentSimulationRules.Footprint(
                setup.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId),
                building.Position)));
        var start = state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => state.Map.IsPassable(point) && !occupied.Contains(point) &&
                state.Map.FootDistance(point, source.Position) is >= 14 and <= 20 &&
                !state.Map.Resources.Any(resource => resource.Position == point))
            .OrderBy(point => state.Map.FootDistance(point, source.Position)).ThenBy(point => point.Y).ThenBy(point => point.X)
            .First(point => DeterministicRouteFinder.TryFind(state.Map, point, source.Position, out var route) &&
                route.All(step => !occupied.Contains(step)));
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == HarvestInstructionActor
                ? person with { Position = start, FullnessBasisPoints = 6_000, TravelCooldownTicks = 0 }
                : person).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = WithoutFoodLots(state.Society.Society.Inventory),
                },
            },
        };
        return WorldRuntime.Restore(state, CancellationProviderFactory(provider));
    }

    private static Func<string, IDecisionProvider> CancellationProviderFactory(CancellationPlanningProvider provider) =>
        id => id == HarvestInstructionActor ? provider :
            new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true);

    private static OwnerInstructionReceipt SubmitCancellationTravelOrder(WorldRuntime world, string key) =>
        world.SubmitInstruction(new OwnerInstructionRequest(key, "owner:test", HarvestInstructionActor,
            OwnerInstructionKind.Order, "go to the berry patch"));

    private static void CancelTravelOrder(WorldRuntime world, string orderId, string key) =>
        Assert.True(world.CancelOrder(new OwnerOrderCancelRequest(key, "owner:test", world.Society.WorldId,
            HarvestInstructionActor, orderId)).Changed);

    private static GridPoint CancellationActorPosition(WorldRuntimeState state) =>
        state.Agents.Single(person => person.AgentId == HarvestInstructionActor).Position;

    private static OrderTask CancellationOrder(WorldRuntimeState state, string orderId) =>
        Assert.Single(state.Instructions!, instruction => instruction.InstructionId == orderId).Task!;

    private sealed class CancellationPlanningProvider(bool holdFirstOrder = false, string orderCandidate = "seek_food",
        bool holdFreshOrder = false) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<AgentObservation> Requests { get; } = new();
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> FreshStarted { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> ReleaseFresh { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> FreshReturned { get; } = new(TaskCreationOptions.None);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();

            Requests.Enqueue(request.Observation);
            if (holdFirstOrder && Requests.Count == 1 && request.Observation.OperativeOrderInstructionId is not null)
            {
                Started.TrySetResult(true);
                // Simulate a remote request that does not acknowledge cancellation.
                // A new valid request must not depend on this obsolete reply.
                await Release.Task;
                Returned.TrySetResult(true);
            }
            else if (holdFreshOrder && request.Observation.OperativeOrderInstructionId is not null)
            {
                FreshStarted.TrySetResult(true);
                await ReleaseFresh.Task;
                FreshReturned.TrySetResult(true);
            }
            return ModelResponse(request, Kind, ProviderEpoch,
                request.Observation.OperativeOrderInstructionId is null ? "safe_idle" : orderCandidate);
        }
    }
}
