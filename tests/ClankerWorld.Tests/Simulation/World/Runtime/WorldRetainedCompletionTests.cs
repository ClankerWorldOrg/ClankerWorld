using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldRuntimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AnAcceptedReplyWithNewerQueuedWorkTakesNoSafeRoutineTheSameTick(bool delayedStartup, bool deferReturn)
    {
        var provider = new HeldFirstReplyProvider("seek_food", deferReturn: deferReturn);
        var preparation = new InlineCompletionSource<bool>(TaskCreationOptions.None);
        if (delayedStartup) provider.PreparationGate = preparation.Task;
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var farStand = FarFromBerries(state, 15, 60);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { Position = farStand, FullnessBasisPoints = 0 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = WithoutFoodLots(state.Society.Society.Inventory) },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new ActionCoverageRecorder(chooseIdle: true));
        GridPoint Position() => world.Agents.Single(item => item.AgentId == HarvestInstructionActor).Position;
        try
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            var frame = WorldRuntimeCodec.Encode(world.ExportState());
            var entered = ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            if (delayedStartup)
            {
                Assert.False(entered.IsCompleted);
                Assert.Equal(0, provider.CallCount);
                Assert.Equal(frame, WorldRuntimeCodec.Encode(world.ExportState()));
                preparation.TrySetResult(true);
            }
            await entered;
            Assert.Equal(1, provider.CallCount);
            Assert.Equal(frame, WorldRuntimeCodec.Encode(world.ExportState()));
            // Newer guidance keeps the actor queued after its held reply is accepted.
            world.SubmitInstruction(new OwnerInstructionRequest("retained-suggestion", "owner:test",
                HarvestInstructionActor, OwnerInstructionKind.Suggestion, "Find something to eat."));
            for (var tick = 0; tick < 3; tick++)
                Assert.True((await world.TickModelWorkAsync()).Advanced);
            var heldFrame = WorldRuntimeCodec.Encode(world.ExportState());
            var returned = HeldHarvestWork(world).Task;
            Assert.False(returned.IsCompleted);
            provider.Release.TrySetResult(true);
            if (deferReturn)
            {
                await ModelWorkHarness.For(world).WaitAsync(provider.Returned.Task);
                Assert.False(returned.IsCompleted);
                Assert.Equal(heldFrame, WorldRuntimeCodec.Encode(world.ExportState()));
                provider.ReturnAllowed.TrySetResult(true);
            }
            await ModelWorkHarness.For(world).WaitAsync(returned);
            Assert.True(provider.Returned.Task.IsCompletedSuccessfully);
            Assert.Equal(heldFrame, WorldRuntimeCodec.Encode(world.ExportState()));

            var before = Position();
            var completion = await world.TickModelWorkAsync();
            Assert.Contains(completion.Decisions, item => item.AgentId == HarvestInstructionActor &&
                item.Admission.Intention?.CandidateId == "seek_food");
            Assert.Contains(world.ExportState().Society.Cognition.Queue, entry => entry.AgentId == HarvestInstructionActor);
            Assert.True(world.ExportState().Map.FootDistance(before, Position()) <= 1);
            Assert.True(completion.Events.Count(item => item.Kind == "agent_moved" &&
                item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal)) <= 1);
        }
        finally
        {
            preparation.TrySetResult(true);
            provider.Release.TrySetResult(true);
            provider.ReturnAllowed.TrySetResult(true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ASuggestionQueuedBehindAFinishedOrderReachesAFreshRequest(bool delayedStartup, bool deferReturn)
    {
        var provider = new HeldFirstReplyProvider("seek_food", replyToSuggestions: true, deferReturn);
        var preparation = new InlineCompletionSource<bool>(TaskCreationOptions.None);
        if (delayedStartup) provider.PreparationGate = preparation.Task;
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var farStand = FarFromBerries(state, 7, 40);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { Position = farStand, FullnessBasisPoints = 10_000 }
                : item).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("held-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather 2 berries from berry-patch"));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        var call = HeldHarvestWork(world);
        Assert.Equal(order.InstructionId, call.Request.Observation.OperativeOrderInstructionId);
        Assert.False(call.Task.IsCompleted);
        var frame = WorldRuntimeCodec.Encode(world.ExportState());
        var entered = ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
        if (delayedStartup)
        {
            Assert.False(provider.Started.Task.IsCompleted);
            Assert.False(entered.IsCompleted);
            Assert.Equal(0, provider.CallCount);
            Assert.Equal(frame, WorldRuntimeCodec.Encode(world.ExportState()));
            preparation.TrySetResult(true);
        }
        await entered;
        Assert.Equal(call.Request.Observation.ObservationDigest, Assert.Single(provider.Requests).ObservationDigest);
        Assert.Equal(frame, WorldRuntimeCodec.Encode(world.ExportState()));
        var suggestion = world.SubmitInstruction(new OwnerInstructionRequest("behind-finished-order", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Suggestion, "Please rest a little afterwards."));
        for (var tick = 0; tick < 80 && Assert.Single(world.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Task!.Status != "finished"; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal("finished", Assert.Single(world.ExportState().Instructions!,
            item => item.InstructionId == order.InstructionId).Task!.Status);
        Assert.Equal(1, provider.CallCount);

        var finishedFrame = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False(call.Task.IsCompleted);
        provider.Release.TrySetResult(true);
        var returned = ModelWorkHarness.For(world).WaitAsync(call.Task);
        if (deferReturn)
        {
            await ModelWorkHarness.For(world).WaitAsync(provider.Returned.Task);
            Assert.False(call.Task.IsCompleted);
            Assert.False(returned.IsCompleted);
            Assert.Equal(finishedFrame, WorldRuntimeCodec.Encode(world.ExportState()));
            provider.ReturnAllowed.TrySetResult(true);
        }
        await returned;
        Assert.True(provider.Returned.Task.IsCompletedSuccessfully);
        Assert.Equal(finishedFrame, WorldRuntimeCodec.Encode(world.ExportState()));

        for (var tick = 0; tick < 10 && provider.CallCount < 2; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await AwaitRetainedHarvestCall(world);
        }
        for (var tick = 0; tick < 5; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await AwaitRetainedHarvestCall(world);
        }

        var second = provider.Requests.ToArray()[1];
        Assert.Null(second.OperativeOrderInstructionId);
        Assert.Contains(second.ObserverGuidance!, item => item.InstructionId == suggestion.InstructionId);
        var final = world.ExportState();
        // Only the held reply for the order that has since finished is set aside.
        Assert.Single(final.Events, item => item.Kind == "instruction_order_stale_decision" &&
            item.Detail == HarvestInstructionActor);
        Assert.Contains(suggestion.InstructionId, final.CompletedInstructionIds ?? []);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task AnOrderThatFinishesBeforeItsRequestIsSentStillReachesTheModel()
    {
        var actorProvider = new ImmediateOrderReplyProvider();
        var blocker = new HoldingProvider();
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var farStand = FarFromBerries(state, 7, 40);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { Position = farStand, FullnessBasisPoints = 10_000 }
                : item).ToArray(),
        };
        var blockerId = state.Society.Society.Agents
            .Where(item => item.Id != HarvestInstructionActor && item.AgeBand != ClankerWorld.Simulation.Society.SocietyAgeBand.Infant)
            .Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal).First();
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? actorProvider
            : id == blockerId ? blocker : new DeterministicDecisionProvider(), maxCognitionDispatchPerCycle: 1);
        for (var tick = 0; tick < 400 && !blocker.Started.Task.IsCompleted; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).SettleAsync();
        }
        Assert.True(blocker.Started.Task.IsCompleted);
        var callsBefore = actorProvider.CallCount;
        var order = world.SubmitInstruction(new OwnerInstructionRequest("queued-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather 2 berries from berry-patch"));
        for (var tick = 0; tick < 80 && Assert.Single(world.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Task!.Status != "finished"; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal("finished", Assert.Single(world.ExportState().Instructions!,
            item => item.InstructionId == order.InstructionId).Task!.Status);
        Assert.Equal(callsBefore, actorProvider.CallCount);
        // One more tick after it finished while still queued.
        Assert.True((await world.TickModelWorkAsync()).Advanced);

        blocker.Release.TrySetResult(true);
        for (var tick = 0; tick < 12; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).SettleAsync();
        }
        var saved = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == order.InstructionId);
        var sawOrder = actorProvider.Requests.Any(request =>
            request.ObserverGuidance?.Any(message => message.InstructionId == order.InstructionId) == true);
        Assert.True(sawOrder, $"order never reached the model; calls={actorProvider.CallCount - callsBefore} observed={saved.ObservedTick} reply={saved.ObserverReply}");
        Assert.NotNull(saved.ObservedTick);
        Assert.Equal("On it.", saved.ObserverReply);
    }

    private sealed class ImmediateOrderReplyProvider : IDecisionProvider
    {
        private int callCount;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public int CallCount => Volatile.Read(ref callCount);
        public ConcurrentQueue<AgentObservation> Requests { get; } = new();

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            Interlocked.Increment(ref callCount);
            Requests.Enqueue(request.Observation);
            var replies = (request.Observation.ObserverGuidance ?? [])
                .Where(item => item.ReplyAllowed)
                .Select(item => new CognitionObserverReply(item.InstructionId, "On it."))
                .ToArray();
            return ValueTask.FromResult(ModelResponse(request, Kind, ProviderEpoch, "seek_food", replies));
        }
    }

    private sealed class HoldingProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return ModelResponse(request, Kind, ProviderEpoch, "safe_idle");
        }
    }

    [Fact]
    public async Task ASuggestionCarriedWithAFinishedOrderReachesALaterFreshRequest()
    {
        var actorProvider = new ImmediateOrderReplyProvider();
        var blocker = new HoldingProvider();
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var farStand = FarFromBerries(state, 7, 40);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { Position = farStand, FullnessBasisPoints = 10_000 }
                : item).ToArray(),
        };
        var blockerId = state.Society.Society.Agents
            .Where(item => item.Id != HarvestInstructionActor && item.AgeBand != ClankerWorld.Simulation.Society.SocietyAgeBand.Infant)
            .Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal).First();
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? actorProvider
            : id == blockerId ? blocker : new DeterministicDecisionProvider(), maxCognitionDispatchPerCycle: 1);
        for (var tick = 0; tick < 400 && !blocker.Started.Task.IsCompleted; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).SettleAsync();
        }
        Assert.True(blocker.Started.Task.IsCompleted);
        var callsBefore = actorProvider.CallCount;
        var order = world.SubmitInstruction(new OwnerInstructionRequest("queued-harvest-with-suggestion", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather 2 berries from berry-patch"));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        var suggestion = world.SubmitInstruction(new OwnerInstructionRequest("suggestion-with-queued-order", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Suggestion, "Rest a little afterwards."));
        for (var tick = 0; tick < 80 && Assert.Single(world.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Task!.Status != "finished"; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal(callsBefore, actorProvider.CallCount);

        blocker.Release.TrySetResult(true);
        for (var tick = 0; tick < 12; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).SettleAsync();
        }
        var requests = actorProvider.Requests.Skip(callsBefore).ToArray();
        Assert.Contains(requests, request => request.OperativeOrderInstructionId == order.InstructionId &&
            request.ObserverGuidance?.Any(message => message.InstructionId == suggestion.InstructionId) == true);
        Assert.Contains(requests, request => request.OperativeOrderInstructionId is null &&
            request.ObserverGuidance?.Any(message => message.InstructionId == suggestion.InstructionId) == true);
        Assert.Contains(suggestion.InstructionId, world.ExportState().CompletedInstructionIds ?? []);
    }

    private static async Task AwaitRetainedHarvestCall(WorldRuntime world)
    {
        await ModelWorkHarness.For(world).SettleAsync();
        var pending = world.PendingModelWork.FirstOrDefault(item => item.Transition.Kind == ModelWorkKind.Decision &&
            item.Transition.ActorId == HarvestInstructionActor);
        if (pending is not null) await ModelWorkHarness.For(world).WaitAsync(pending.Completion);
    }

    private static GridPoint FarFromBerries(WorldRuntimeState state, int nearest, int farthest)
    {
        var source = state.Map.Resources.Single(item => item.Id == "berry-patch");
        var occupied = state.Agents.Where(item => item.AgentId != HarvestInstructionActor)
            .Select(item => item.Position).ToHashSet();
        return state.Map.Tiles.Select(item => item.Position)
            .Where(point => state.Map.IsPassable(point) && !occupied.Contains(point) &&
                state.Map.Resources.All(resource => resource.Position != point))
            .Select(point => (Point: point, Distance: state.Map.FootDistance(point, source.Position)))
            .Where(item => item.Distance >= nearest && item.Distance < farthest)
            .OrderByDescending(item => item.Distance).ThenBy(item => item.Point.X).ThenBy(item => item.Point.Y)
            .Select(item => item.Point).First();
    }

    // Holds the first reply until released; later calls answer at once.
    private sealed class HeldFirstReplyProvider(string firstChoice, bool replyToSuggestions = false, bool deferReturn = false) : IDecisionProvider
    {
        private int callCount;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public int CallCount => Volatile.Read(ref callCount);
        public ConcurrentQueue<AgentObservation> Requests { get; } = new();
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.None);
        public Task? PreparationGate { get; set; }
        public InlineCompletionSource<bool> ReturnAllowed { get; } = new(TaskCreationOptions.None);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (PreparationGate is { } preparation) await preparation.WaitAsync(cancellationToken);
            request.Validate();
            var call = Interlocked.Increment(ref callCount);
            Requests.Enqueue(request.Observation);
            if (call == 1)
            {
                Started.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
                var response = ModelResponse(request, Kind, ProviderEpoch, firstChoice);
                Returned.TrySetResult(true);
                if (deferReturn) await ReturnAllowed.Task.WaitAsync(cancellationToken);
                return response;
            }
            if (!replyToSuggestions)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("Later calls stay pending in this test.");
            }
            var replies = (request.Observation.ObserverGuidance ?? [])
                .Where(item => item.Kind == "suggestion" && item.ReplyAllowed)
                .Select(item => new CognitionObserverReply(item.InstructionId, "I will rest."))
                .ToArray();
            return ModelResponse(request, Kind, ProviderEpoch, "safe_idle", replies);
        }
    }
}
