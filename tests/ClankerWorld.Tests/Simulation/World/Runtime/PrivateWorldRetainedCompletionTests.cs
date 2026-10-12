using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldRuntimeTests
{
    [Fact]
    public async Task AnAcceptedReplyWithNewerQueuedWorkTakesNoSafeRoutineTheSameTick()
    {
        var provider = new HeldFirstReplyProvider("seek_food");
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var farStand = FarFromBerries(state, 15, 60);
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == HarvestInstructionActor
                ? item with { Position = farStand, HungerBasisPoints = 0 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = WithoutFoodLots(state.Society.Society.Inventory) },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new ActionCoverageRecorder(chooseIdle: true));
        GridPoint Position() => world.Inhabitants.Single(item => item.InhabitantId == HarvestInstructionActor).Position;
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        // Newer guidance keeps the actor queued after its held reply is accepted.
        world.SubmitInstruction(new OwnerInstructionRequest("retained-suggestion", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Suggestive, "Find something to eat."));
        for (var tick = 0; tick < 3; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        provider.Release.TrySetResult(true);
        await provider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(50);

        var before = Position();
        var completion = await world.AdvanceOneTickNonBlockingAsync();
        Assert.Contains(completion.Decisions, item => item.InhabitantId == HarvestInstructionActor &&
            item.Admission.Intention?.CandidateId == "seek_food");
        Assert.Contains(world.ExportState().Society.Cognition.Queue, entry => entry.InhabitantId == HarvestInstructionActor);
        Assert.True(world.ExportState().Map.FootDistance(before, Position()) <= 1);
        Assert.True(completion.Events.Count(item => item.Kind == "inhabitant_moved" &&
            item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal)) <= 1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ASuggestionQueuedBehindAFinishedOrderReachesAFreshRequest(bool delayedStartup, bool deferReturn)
    {
        var provider = new HeldFirstReplyProvider("seek_food", replyToSuggestions: true, deferReturn);
        var preparation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (delayedStartup) provider.PreparationGate = preparation.Task;
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var farStand = FarFromBerries(state, 7, 40);
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == HarvestInstructionActor
                ? item with { Position = farStand, HungerBasisPoints = 10_000 }
                : item).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("held-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.MustDo, "gather 2 berries from berry-patch"));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var call = CaptureHeldHarvestCall(world);
        Assert.Equal(order.InstructionId, call.Request.Observation.OperativeOrderInstructionId);
        Assert.False(call.Task.IsCompleted);
        var frame = WorldRuntimeCodec.Encode(world.ExportState());
        var entered = AwaitHarvestPhase(provider.Started.Task, "retained-order provider entry");
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
            HarvestInstructionActor, OwnerInstructionKind.Suggestive, "Please rest a little afterwards."));
        for (var tick = 0; tick < 80 && Assert.Single(world.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Order!.Status != "finished"; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal("finished", Assert.Single(world.ExportState().Instructions!,
            item => item.InstructionId == order.InstructionId).Order!.Status);
        Assert.Equal(1, provider.CallCount);

        var finishedFrame = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False(call.Task.IsCompleted);
        provider.Release.TrySetResult(true);
        var returned = AwaitHarvestPhase(call.Task, "retained-order hosted task return");
        if (deferReturn)
        {
            await AwaitHarvestPhase(provider.Returned.Task, "retained-order response construction");
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
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await AwaitRetainedHarvestCall(world);
        }
        for (var tick = 0; tick < 5; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
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
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == HarvestInstructionActor
                ? item with { Position = farStand, HungerBasisPoints = 10_000 }
                : item).ToArray(),
        };
        var blockerId = state.Society.Society.Inhabitants
            .Where(item => item.Id != HarvestInstructionActor && item.AgeBand != ClankerWorld.Simulation.Society.SocietyAgeBand.Infant)
            .Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal).First();
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? actorProvider
            : id == blockerId ? blocker : new DeterministicDecisionProvider(), maxCognitionDispatchPerCycle: 1);
        for (var tick = 0; tick < 400 && !blocker.Started.Task.IsCompleted; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await Task.Delay(5);
        }
        Assert.True(blocker.Started.Task.IsCompleted);
        var callsBefore = actorProvider.CallCount;
        var order = world.SubmitInstruction(new OwnerInstructionRequest("queued-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.MustDo, "gather 2 berries from berry-patch"));
        for (var tick = 0; tick < 80 && Assert.Single(world.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Order!.Status != "finished"; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal("finished", Assert.Single(world.ExportState().Instructions!,
            item => item.InstructionId == order.InstructionId).Order!.Status);
        Assert.Equal(callsBefore, actorProvider.CallCount);
        // One more tick after it finished while still queued.
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);

        blocker.Release.TrySetResult(true);
        for (var tick = 0; tick < 12; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await Task.Delay(20);
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
        public ConcurrentQueue<InhabitantObservation> Requests { get; } = new();

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
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == HarvestInstructionActor
                ? item with { Position = farStand, HungerBasisPoints = 10_000 }
                : item).ToArray(),
        };
        var blockerId = state.Society.Society.Inhabitants
            .Where(item => item.Id != HarvestInstructionActor && item.AgeBand != ClankerWorld.Simulation.Society.SocietyAgeBand.Infant)
            .Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal).First();
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? actorProvider
            : id == blockerId ? blocker : new DeterministicDecisionProvider(), maxCognitionDispatchPerCycle: 1);
        for (var tick = 0; tick < 400 && !blocker.Started.Task.IsCompleted; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await Task.Delay(5);
        }
        Assert.True(blocker.Started.Task.IsCompleted);
        var callsBefore = actorProvider.CallCount;
        var order = world.SubmitInstruction(new OwnerInstructionRequest("queued-harvest-with-suggestion", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.MustDo, "gather 2 berries from berry-patch"));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var suggestion = world.SubmitInstruction(new OwnerInstructionRequest("suggestion-with-queued-order", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Suggestive, "Rest a little afterwards."));
        for (var tick = 0; tick < 80 && Assert.Single(world.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Order!.Status != "finished"; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal(callsBefore, actorProvider.CallCount);

        blocker.Release.TrySetResult(true);
        for (var tick = 0; tick < 12; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await Task.Delay(20);
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
        // A tick can admit the older reply without dispatching its retained work.
        // When the fresh call starts, finish its actual task before advancing again.
        var field = typeof(WorldRuntime).GetField("pendingModelCalls",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var pending = Assert.IsAssignableFrom<System.Collections.IDictionary>(field!.GetValue(world));
        if (pending.Contains(HarvestInstructionActor))
            await AwaitHarvestPhase(CaptureHeldHarvestCall(world).Task, "fresh retained-suggestion hosted task");
    }

    private static GridPoint FarFromBerries(WorldRuntimeState state, int nearest, int farthest)
    {
        var source = state.Map.Resources.Single(item => item.Id == "berry-patch");
        var occupied = state.Inhabitants.Where(item => item.InhabitantId != HarvestInstructionActor)
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
        public ConcurrentQueue<InhabitantObservation> Requests { get; } = new();
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task? PreparationGate { get; set; }
        public TaskCompletionSource<bool> ReturnAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                .Where(item => item.Kind == "suggestive" && item.ReplyAllowed)
                .Select(item => new CognitionObserverReply(item.InstructionId, "I will rest."))
                .ToArray();
            return ModelResponse(request, Kind, ProviderEpoch, "safe_idle", replies);
        }
    }
}
