using System.Collections.Concurrent;
using System.Text;
using System.Net;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class WorldDeferredCognitionTests
{
    private const string NameTargetId = "founder:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NameOwnerId = "founder:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task DuplicateNameIsRetriedOnceWithCanonicalComparisonAndASeparateProviderRequest()
    {
        var provider = new SequencedModelProvider(
            new NameReply("e\u0301LODIE\u00a0Lake", SelectedCandidateId: "unknown_action"),
            new NameReply("Maia Vale"));
        using var world = CreateNameTestWorld("duplicate-name-retry", provider);
        const string takenName = "  Élodie\u00a0Vale  ";
        Assert.True(world.RenameAgent(NameOwnerId, takenName));
        var placeholder = world.Society.GetAgent(NameTargetId).Name;
        world.StartWorld();

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var first = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        var rejectedAction = Assert.Single(first.Decisions, item => item.AgentId == NameTargetId).Admission;
        Assert.True(rejectedAction.FellBack);
        Assert.Equal("candidate_not_legal", rejectedAction.Outcome);
        Assert.Equal(placeholder, world.Society.GetAgent(NameTargetId).Name);
        Assert.True(world.Society.GetAgent(NameTargetId).NeedsName);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_name_retry_requested" && item.Detail == NameTargetId);

        var retry = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        Assert.True(Assert.Single(retry.Decisions, item => item.AgentId == NameTargetId).Admission.Accepted);
        Assert.Equal(2, provider.CallCount);
        Assert.False(provider.ObservedRequests.ElementAt(0).IsNameRetry);
        Assert.True(provider.ObservedRequests.ElementAt(1).IsNameRetry);
        Assert.True(provider.ObservedRequests.ElementAt(1).NeedsName);
        Assert.Equal("Maia Vale", world.Society.GetAgent(NameTargetId).Name);
        Assert.True(world.Society.GetAgent(NameTargetId).HasChosenName);
        Assert.False(world.Society.GetAgent(NameTargetId).NeedsName);
        Assert.DoesNotContain(world.ExportState().Events, item =>
            item.Detail.Contains("Élodie", StringComparison.OrdinalIgnoreCase) ||
            item.Detail.Contains("Maia Vale", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UniqueChosenFirstNameSurvivesARejectedActionWithoutAnotherNamingCall()
    {
        var provider = new SequencedModelProvider(new NameReply("Maia Vale", SelectedCandidateId: "unknown_action"));
        using var world = CreateNameTestWorld("name-with-rejected-action", provider);
        world.StartWorld();
        var result = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        var admission = Assert.Single(result.Decisions, item => item.AgentId == NameTargetId).Admission;
        Assert.True(admission.FellBack);
        Assert.Equal("candidate_not_legal", admission.Outcome);
        var named = world.Society.GetAgent(NameTargetId);
        Assert.Equal("Maia Vale", named.Name);
        Assert.True(named.HasChosenName);
        Assert.False(named.NeedsName);
        Assert.Single(provider.ObservedRequests);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_name_retry_requested");
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.True(restored.Society.GetAgent(NameTargetId).HasChosenName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4000)]
    public async Task DuplicateNameRetryConsumesASecondConfiguredProviderAllowance(int firstReplyDelayMilliseconds)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-name-retry-meter-");
        try
        {
            var configuration = new ProviderConfigurationStore(
                Path.Combine(directory.FullName, "providers.json"),
                new ProviderConfigurationSeed("deterministic", null, null, null, null, null, null));
            _ = configuration.Configure(new("routine", "openai", "retry-meter-model", "retry-meter-key", false));
            _ = configuration.Configure(new("planning", "openai", "retry-meter-model", "retry-meter-key", false));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            _ = usage.Configure(new ProviderUsageLimitAction(2));
            var handler = new SequencedNameResponseHandler(firstReplyDelayMilliseconds, "Taken Lake", "Maia Vale");
            var router = new ConfigurableDecisionProvider(configuration,
                new FixedHttpClientFactory(handler), usageStore: usage);
            using var world = CreateNameTestWorld("duplicate-name-retry-meter", router);
            Assert.True(world.RenameAgent(NameOwnerId, "Taken Name"));
            world.StartWorld();

            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
            Assert.InRange(handler.RequestCount, 1, 2);
            Assert.InRange(usage.Capture().Attempts, 1, 2);
            Assert.True(world.Society.GetAgent(NameTargetId).NeedsName);

            _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
            Assert.Equal(2, handler.RequestCount);
            var metering = usage.Capture();
            Assert.Equal(2, metering.Attempts);
            Assert.Equal(2, metering.Completed);
            Assert.Equal(2, metering.Rows.Sum(row => row.Attempts));
            Assert.Equal("Maia Vale", world.Society.GetAgent(NameTargetId).Name);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task MalformedReplyDoesNotApplyItsNameOrStartTheDuplicateNameRetry()
    {
        var provider = new SequencedModelProvider(new NameReply("Taken Lake", 1.2));
        // Peers must not take shared tools and create a real follow-up choice before this reply is admitted.
        using var world = CreateNameTestWorld("malformed-name-reply", provider, quietOthers: true);
        Assert.True(world.RenameAgent(NameOwnerId, "Taken Name"));
        var placeholder = world.Society.GetAgent(NameTargetId).Name;
        world.StartWorld();

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var result = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        var admission = Assert.Single(result.Decisions, item => item.AgentId == NameTargetId).Admission;

        Assert.True(admission.FellBack);
        Assert.Equal("malformed_response", admission.Outcome);
        Assert.Equal(placeholder, world.Society.GetAgent(NameTargetId).Name);
        Assert.True(world.Society.GetAgent(NameTargetId).NeedsName);
        Assert.Equal(1, provider.CallCount);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_name_retry_requested");
    }

    [Fact]
    public async Task FailedNameRetryKeepsPlaceholderAndClosesTheNamingAttempt()
    {
        var provider = new SequencedModelProvider(
            [new NameReply("Taken Lake")], false, false, true);
        using var world = CreateNameTestWorld("failed-name-retry", provider);
        Assert.True(world.RenameAgent(NameOwnerId, "Taken Name"));
        var placeholder = world.Society.GetAgent(NameTargetId).Name;
        world.StartWorld();

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        // Retained work is refreshed on the tick after the first reply is admitted.
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.SecondFailed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);
        _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);

        var namedAgent = world.Society.GetAgent(NameTargetId);
        Assert.Equal(placeholder, namedAgent.Name);
        Assert.False(namedAgent.NeedsName);
        Assert.False(namedAgent.HasChosenName);
        var requests = provider.ObservedRequests.ToArray();
        Assert.True(requests.Length >= 2);
        Assert.True(requests[1].IsNameRetry);
        Assert.Contains(world.ExportState().Events,
            item => item.Kind == "agent_name_retry_unusable" && item.Detail == NameTargetId);
        Assert.Single(world.ExportState().Events,
            item => item.Kind == "agent_name_retry_requested" && item.Detail == NameTargetId);
        Assert.DoesNotContain(world.ExportState().Society.Cognition.Queue,
            entry => entry.AgentId == NameTargetId && entry.TriggerIds.Contains(
                SocietyCognitionScheduler.NameRetryTriggerId, StringComparer.Ordinal));
    }

    [Fact]
    public async Task SecondDuplicateKeepsPlaceholderAndDoesNotStartAnotherNamingRequest()
    {
        var provider = new SequencedModelProvider(
            new NameReply("Taken Lake"),
            new NameReply("taken\u00a0Ridge"));
        using var world = CreateNameTestWorld("duplicate-name-exhausted", provider);
        Assert.True(world.RenameAgent(NameOwnerId, "Taken Name"));
        var placeholder = world.Society.GetAgent(NameTargetId).Name;
        world.StartWorld();

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        // How many ticks a model reply takes depends on the runner, so an ordinary
        // re-evaluation may also start; only the two naming requests may ask for a name.
        Assert.Equal(2, provider.ObservedRequests.Count(request => request.NeedsName || request.IsNameRetry));
        Assert.Equal(placeholder, world.Society.GetAgent(NameTargetId).Name);
        Assert.False(world.Society.GetAgent(NameTargetId).NeedsName);
        Assert.False(world.Society.GetAgent(NameTargetId).HasChosenName);
        Assert.Contains(world.ExportState().Events,
            item => item.Kind == "agent_name_retry_exhausted" && item.Detail == NameTargetId);

        var namingRequests = provider.ObservedRequests.Where(request => request.NeedsName || request.IsNameRetry).ToArray();
        Assert.Collection(namingRequests,
            first =>
            {
                Assert.True(first.NeedsName);
                Assert.False(first.IsNameRetry);
            },
            retry =>
            {
                Assert.True(retry.NeedsName);
                Assert.True(retry.IsNameRetry);
            });
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        // Ordinary personal decisions can follow exhaustion when the world
        // context changes; neither may reopen the completed naming attempt.
        Assert.Equal(2, provider.ObservedRequests.Count(request => request.NeedsName || request.IsNameRetry));
        Assert.Equal(namingRequests, provider.ObservedRequests.Where(request => request.NeedsName || request.IsNameRetry).ToArray());
        Assert.Equal(placeholder, world.Society.GetAgent(NameTargetId).Name);
        Assert.False(world.Society.GetAgent(NameTargetId).NeedsName);
        Assert.False(world.Society.GetAgent(NameTargetId).HasChosenName);
        Assert.DoesNotContain(world.ExportState().Society.Cognition.Queue,
            entry => entry.AgentId == NameTargetId && entry.TriggerIds.Contains(
                SocietyCognitionScheduler.NameRetryTriggerId, StringComparer.Ordinal));

        // Compaction must not infer name ownership from the retry events it archives.
        for (var cycle = 0; cycle <= WorldHistory.CompactionThreshold / 2; cycle++)
        {
            world.Pause();
            world.Resume();
        }
        world.Pause();
        var directory = Directory.CreateTempSubdirectory("closed-placeholder-history-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            Assert.True(file.Save(world));
            var compact = world.ExportState();
            Assert.True(compact.EventHistoryFloor > 0);
            Assert.DoesNotContain(compact.Events, item => item.Kind == "agent_name_retry_exhausted");
            using var fromDisk = file.LoadOrCreate(compact.WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(compact), WorldRuntimeCodec.Encode(fromDisk.ExportState()));
            Assert.False(fromDisk.Society.GetAgent(NameTargetId).HasChosenName);
            Assert.False(fromDisk.Society.GetAgent(NameTargetId).NeedsName);
            // Every founder placeholder can coexist with a genuinely chosen Founder first name.
            Assert.True(fromDisk.RenameAgent(NameOwnerId, "Founder Vale"));
            Assert.Equal(placeholder, fromDisk.Society.GetAgent(NameTargetId).Name);

            var ordinaryProvider = new SequencedModelProvider(new NameReply("Unexpected Replacement"));
            using var resumed = WorldRuntime.Restore(
                WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(fromDisk.ExportState())),
                id => id == NameTargetId ? ordinaryProvider : new QuietDecisionProvider());
            _ = resumed.SubmitInstruction(new OwnerInstructionRequest("after-name-compaction", "owner:test", NameTargetId,
                OwnerInstructionKind.Suggestion, "Stay here for a moment."));
            resumed.Resume();
            _ = await AdvanceUntilAcceptedAsync(resumed, NameTargetId);
            Assert.NotEmpty(ordinaryProvider.ObservedRequests);
            Assert.All(ordinaryProvider.ObservedRequests, request =>
            {
                Assert.False(request.NeedsName);
                Assert.False(request.IsNameRetry);
            });
            Assert.Equal(placeholder, resumed.Society.GetAgent(NameTargetId).Name);
            Assert.False(resumed.Society.GetAgent(NameTargetId).HasChosenName);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task SameTickDuplicateNamesGoToStableIdWinner()
    {
        var firstProvider = new SequencedModelProvider(new NameReply("Shared Vale"));
        var secondProvider = new SequencedModelProvider(
            new NameReply("SHARED Lake"),
            new NameReply("Second Name"));
        using var world = new WorldRuntime("same-tick-duplicate-names", id => id switch
        {
            NameTargetId => firstProvider,
            NameOwnerId => secondProvider,
            _ => new DeterministicDecisionProvider(),
        }, startPace: WorldStartPace.FounderSetup);
        world.PlaceFounder(NameTargetId, new GridPoint(0, 0));
        world.PlaceFounder(NameOwnerId, new GridPoint(1, 2));
        world.PlaceFounder("founder:cccccccccccccccccccccccccccccccc", new GridPoint(2, 2));
        world.PlaceFounder("founder:dddddddddddddddddddddddddddddddd", new GridPoint(3, 2));
        var secondPlaceholder = world.Society.GetAgent(NameOwnerId).Name;
        world.StartWorld();

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await Task.WhenAll(firstProvider.FirstReturned.Task, secondProvider.FirstReturned.Task)
            .WaitAsync(TimeSpan.FromSeconds(3));
        // Each provider signals just before the runtime's call task finishes,
        // so wait for both calls to finish before the tick that admits them together.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!world.ModelDecisionsFinished(NameTargetId, NameOwnerId))
        {
            Assert.True(DateTime.UtcNow < deadline, "Both model calls should finish.");
            await Task.Delay(1);
        }
        var simultaneous = await world.AdvanceOneTickNonBlockingAsync();
        Assert.True(simultaneous.Advanced);
        Assert.Contains(simultaneous.Decisions, item => item.AgentId == NameTargetId && item.Admission.Accepted);
        Assert.Contains(simultaneous.Decisions, item => item.AgentId == NameOwnerId && item.Admission.Accepted);
        Assert.Equal("Shared Vale", world.Society.GetAgent(NameTargetId).Name);
        Assert.Equal(secondPlaceholder, world.Society.GetAgent(NameOwnerId).Name);
        Assert.True(world.Society.GetAgent(NameOwnerId).NeedsName);

        _ = await AdvanceUntilAcceptedAsync(world, NameOwnerId);
        Assert.True(secondProvider.ObservedRequests.ElementAt(1).IsNameRetry);
        Assert.Equal("Second Name", world.Society.GetAgent(NameOwnerId).Name);
    }

    [Fact]
    public async Task DuplicateNameRetryMarkerSurvivesPauseSaveRestoreAndIgnoresCanceledReply()
    {
        var provider = new SequencedModelProvider(
            [new NameReply("Taken Lake"), new NameReply("Restored Name")],
            holdSecond: true,
            ignoreSecondCancellation: true);
        using var world = CreateNameTestWorld("duplicate-name-retry-save", provider);
        Assert.True(world.RenameAgent(NameOwnerId, "Taken Name"));
        var placeholder = world.Society.GetAgent(NameTargetId).Name;
        world.StartWorld();

        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(provider.SecondObservation!.IsNameRetry);
        var completedBeforeCancellation = world.ExportState().Events.Count(item =>
            item.Kind == "hosted_decision_completed");
        world.Pause();
        var savedJson = WorldRuntimeCodec.Encode(world.ExportState());
        var saved = WorldRuntimeCodec.Decode(savedJson);
        Assert.Contains(SocietyCognitionScheduler.NameRetryTriggerId,
            saved.Society.Cognition.Queue.Single(item => item.AgentId == NameTargetId).TriggerIds);
        Assert.False(Encoding.UTF8.GetString(savedJson).Contains("isNameRetry", StringComparison.OrdinalIgnoreCase));

        Assert.True(provider.SecondCancellationToken.IsCancellationRequested);
        var canceledReply = provider.CompleteSecondReply();
        Assert.True(canceledReply.IsCompletedSuccessfully);
        Assert.Equal("Restored Name", (await canceledReply).ChosenName);
        Assert.Equal(completedBeforeCancellation, world.ExportState().Events.Count(item =>
            item.Kind == "hosted_decision_completed"));
        Assert.Equal(placeholder, world.Society.GetAgent(NameTargetId).Name);

        var restoredProvider = new SequencedModelProvider(new NameReply("Restored Name"));
        using var restored = WorldRuntime.Restore(saved, id => id == NameTargetId
            ? restoredProvider
            : new DeterministicDecisionProvider());
        restored.Resume();
        Assert.True((await restored.AdvanceOneTickNonBlockingAsync()).Advanced);
        await restoredProvider.FirstReturned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var completed = await AdvanceUntilAcceptedAsync(restored, NameTargetId);
        Assert.True(Assert.Single(completed.Decisions, item => item.AgentId == NameTargetId).Admission.Accepted);
        Assert.True(Assert.Single(restoredProvider.ObservedRequests).IsNameRetry);
        Assert.Equal("Restored Name", restored.Society.GetAgent(NameTargetId).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlayerRenameCancelsQueuedNamingWorkAndPreservesOtherTriggers(bool otherWork)
    {
        var provider = new SequencedModelProvider(
            [new NameReply("Taken Lake"), new NameReply("Retry Name")], holdSecond: true);
        using var world = CreateNameTestWorld("rename-queued-name-retry", provider, quietOthers: true);
        Assert.True(world.RenameAgent(NameOwnerId, "Taken Name"));
        world.StartWorld();
        await world.AdvanceOneTickNonBlockingAsync();
        _ = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        world.Pause();
        var saved = world.ExportState();
        saved = saved with
        {
            Society = saved.Society with
            {
                Cognition = saved.Society.Cognition with
                {
                    Queue = saved.Society.Cognition.Queue.Select(entry => entry.AgentId == NameTargetId
                        ? entry with
                        {
                            TriggerIds = otherWork ? [SocietyCognitionScheduler.NameRetryTriggerId, "other_work"]
                            : [SocietyCognitionScheduler.NameRetryTriggerId]
                        } : entry).ToArray(),
                }
            }
        };
        var freshProvider = new SequencedModelProvider(new NameReply("Unexpected Retry"));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(saved)),
            id => id == NameTargetId ? freshProvider : new QuietDecisionProvider());
        Assert.True(restored.RenameAgent(NameTargetId, "Player Name"));
        var queued = restored.ExportState().Society.Cognition.Queue.SingleOrDefault(entry => entry.AgentId == NameTargetId);
        if (otherWork) Assert.Equal(["other_work"], queued!.TriggerIds);
        else Assert.Null(queued);
        restored.Resume();
        await restored.AdvanceOneTickNonBlockingAsync();
        await Task.Delay(100);
        Assert.DoesNotContain(freshProvider.ObservedRequests, request => request.IsNameRetry || request.NeedsName);
        if (!otherWork) Assert.Equal(0, freshProvider.CallCount);
        Assert.Equal("Player Name", restored.Society.GetAgent(NameTargetId).Name);
    }

    [Fact]
    public async Task HelperSwitchPreservesMemoriesDiscardsLateRepliesAndReplaysItsSavedChoice()
    {
        var modelProvider = new HeldModelProvider(ignoreCancellation: true, kind: DecisionProviderKind.OpenAiDecisions);
        using var world = new WorldRuntime("helper-switch", id => id == "founder-scout" ? modelProvider : new QuietDecisionProvider());
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await modelProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        world.Pause();
        var memories = world.Society.Memories;
        Assert.True(world.SetRoutineHelper(new("decisions", "gpt-6-luna")));
        Assert.True(world.SetRoutineHelper(new("jev", "typed-jev-model")));
        Assert.True(world.SetRoutineHelper(RoutineHelperSettings.Off));
        var checkpoint = world.ExportState();
        Assert.Equal(WorldRuntime.StateSchemaVersion, checkpoint.SchemaVersion);
        var older = checkpoint with { SchemaVersion = checkpoint.SchemaVersion - 1 };
        var refused = Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(older));
        Assert.Contains("older than the minimum supported schema", refused.Message, StringComparison.Ordinal);
        var saved = WorldRuntimeCodec.Encode(checkpoint);
        modelProvider.Release.TrySetResult(true);
        await modelProvider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(memories, world.Society.Memories);
        using var first = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new QuietDecisionProvider());
        using var second = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new QuietDecisionProvider());
        first.Resume();
        second.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await first.AdvanceOneTickAsync()).Advanced);
            Assert.True((await second.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(first.ExportState()), WorldRuntimeCodec.Encode(second.ExportState()));
        }
        Assert.Equal(RoutineHelperSettings.Off, first.RoutineHelper);
        Assert.Equal(3, first.JevPolicyRevision);
        first.Pause();
        Assert.True(first.SetRoutineHelper(new("decisions", "future-model")));
        using var modelRestored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(first.ExportState())));
        Assert.Equal(new RoutineHelperSettings("decisions", "future-model"), modelRestored.RoutineHelper);
    }

    [Fact]
    public async Task AFounderWithASlowModelDoesNotHoldWorldOrOtherFounders()
    {
        var modelProvider = new HeldModelProvider();
        // Keep the offered context unchanged while peers still complete their independent decisions.
        using var world = new WorldRuntime("deferred-founder", id =>
            id == "founder-scout" ? modelProvider : new QuietDecisionProvider());

        var first = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(first.Advanced);
        Assert.Equal(1, world.WorldTick);
        Assert.Contains(first.Decisions, item => item.AgentId != "founder-scout" && item.Admission.Accepted);
        await modelProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains(world.ExportState().Society.Cognition.Queue, item => item.AgentId == "founder-scout");
        Assert.Contains(new OwnerWorldObservationStore(world).GetSnapshot().Agents
                .Single(item => item.Id == "founder-scout").DecisionFactors,
            item => item.Key == "decision-pending");

        for (var tick = 0; tick < 3; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3))).Advanced);
        Assert.Equal(4, world.WorldTick);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");

        modelProvider.Release.TrySetResult(true);
        await modelProvider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var admitted = await AdvanceUntilAcceptedAsync(world, "founder-scout");
        Assert.Contains(admitted.Events, item => item.Kind == "hosted_decision_completed");
        Assert.DoesNotContain(world.ExportState().Society.Cognition.Queue, item => item.AgentId == "founder-scout");
    }

    [Fact]
    public async Task AcceptedPersonalModelThoughtIsSavedAndShownOnlyOnItsOwnersProfile()
    {
        var modelProvider = new HeldModelProvider(kind: DecisionProviderKind.LargeLanguageModel,
            privateThought: "I should gather food before the others wake.", chosenName: "Aster Vale");
        using var world = CreateNameTestWorld("private-thoughts", modelProvider);
        world.StartWorld();
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await modelProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(modelProvider.NeedsNameObserved);
        Assert.True(world.RenameAgent(NameTargetId, "Player-picked"));
        Assert.False(world.Society.IsPaused);
        modelProvider.Release.TrySetResult(true);
        await modelProvider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var admitted = await AdvanceUntilAcceptedAsync(world, NameTargetId);
        Assert.False(Assert.Single(admitted.Decisions, item => item.AgentId == NameTargetId).Admission.FellBack);
        Assert.Equal("Player-picked", world.Society.GetAgent(NameTargetId).Name);

        var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(WorldRuntime.StateSchemaVersion, saved.SchemaVersion);
        using var restored = WorldRuntime.Restore(saved);
        Assert.Equal("Player-picked", restored.Society.GetAgent(NameTargetId).Name);
        var people = new OwnerWorldObservationStore(restored).GetSnapshot().Agents;
        Assert.Equal("I should gather food before the others wake.",
            Assert.Single(people.Single(person => person.Id == NameTargetId).RecentPrivateThoughts).Text);
        Assert.All(people.Where(person => person.Id != NameTargetId),
            person => Assert.Empty(person.RecentPrivateThoughts));
        Assert.DoesNotContain(world.ExportState().Events,
            item => item.Detail.Contains("I should gather food", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PausedObserverReplyRetriesTheSameMessageAndCannotAttachToANewerMessage()
    {
        const string targetId = "founder-scout";
        var staleProvider = new HeldGuidanceReplyProvider("Stale reply must not be saved.");
        using var world = new WorldRuntime("paused-observer-guidance", id =>
            id == targetId ? staleProvider : new DeterministicDecisionProvider());
        var first = world.SubmitInstruction(new OwnerInstructionRequest("observer-first", "owner:test", targetId,
            OwnerInstructionKind.Suggestion, "Try the berries beside the river."));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await staleProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var initialObservation = Assert.Single(staleProvider.ObservedRequests);
        Assert.Equal(world.Society.WorldId, initialObservation.WorldId);
        Assert.Equal(targetId, initialObservation.AgentId);
        Assert.Equal(first.InstructionId.Value, Assert.Single(initialObservation.ObserverGuidance!).InstructionId);

        world.Pause();
        var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        staleProvider.Release.TrySetResult(true);
        await staleProvider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var pausedInstruction = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == first.InstructionId);
        Assert.Null(pausedInstruction.ObservedTick);
        Assert.Null(pausedInstruction.ObserverReply);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");

        var replacement = new HeldGuidanceReplyProvider("Fresh reply for the first message.");
        using var restored = WorldRuntime.Restore(saved, id =>
            id == targetId ? replacement : new DeterministicDecisionProvider());
        var second = restored.SubmitInstruction(new OwnerInstructionRequest("observer-second", "owner:test", targetId,
            OwnerInstructionKind.Suggestion, "Look for a grove after you eat."));
        restored.Resume();
        Assert.True((await restored.AdvanceOneTickNonBlockingAsync()).Advanced);
        await replacement.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var retryObservation = Assert.Single(replacement.ObservedRequests);
        Assert.Equal(saved.Society.Society.WorldId, retryObservation.WorldId);
        Assert.Equal(targetId, retryObservation.AgentId);
        Assert.Equal([first.InstructionId, second.InstructionId], retryObservation.ObserverGuidance!
            .Select(item => item.InstructionId).ToArray());
        replacement.Release.TrySetResult(true);
        _ = await AdvanceUntilAcceptedAsync(restored, targetId);

        var final = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(restored.ExportState()));
        var firstSaved = Assert.Single(final.Instructions!, item => item.InstructionId == first.InstructionId);
        var secondSaved = Assert.Single(final.Instructions!, item => item.InstructionId == second.InstructionId);
        Assert.NotNull(firstSaved.ObservedTick);
        Assert.Equal("Fresh reply for the first message.", firstSaved.ObserverReply);
        Assert.Contains(first.InstructionId.Value, final.CompletedInstructionIds ?? []);
        Assert.NotNull(secondSaved.ObservedTick);
        Assert.Null(secondSaved.ObserverReply);
        Assert.Contains(second.InstructionId.Value, final.CompletedInstructionIds ?? []);

        using var roundtripped = WorldRuntime.Restore(final);
        var projection = new OwnerWorldObservationStore(roundtripped).GetSnapshot();
        var targetMessages = projection.Instructions.Where(item => item.TargetAgentId == targetId).ToArray();
        Assert.Equal(2, targetMessages.Length);
        Assert.Equal(first.InstructionId.Value, targetMessages[0].InstructionId);
        Assert.Equal(firstSaved.ObserverReply, targetMessages[0].ObserverReply);
        Assert.Equal(second.InstructionId.Value, targetMessages[1].InstructionId);
        Assert.NotNull(targetMessages[1].ObservedTick);
        Assert.Null(targetMessages[1].ObserverReply);
        Assert.DoesNotContain(projection.Instructions, item => item.TargetAgentId != targetId);
        roundtripped.Validate();
    }

    private static async Task<WorldStepResult> AdvanceUntilAcceptedAsync(WorldRuntime world, string agentId)
    {
        var field = typeof(WorldRuntime).GetField("pendingModelCalls",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var pending = Assert.IsAssignableFrom<System.Collections.IDictionary>(field!.GetValue(world));
        for (var tick = 0; tick < 20; tick++)
        {
            // Finish the registered wrapper task, including provider metering,
            // without spending native ticks on thread-pool scheduling.
            if (pending.Contains(agentId))
            {
                var call = pending[agentId]!;
                var task = Assert.IsAssignableFrom<Task>(call.GetType().GetProperty("Task")!.GetValue(call));
                var checkpoint = WorldRuntimeCodec.Encode(world.ExportState());
                await task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Same(call, pending[agentId]);
                Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(world.ExportState()));
            }
            var step = await world.AdvanceOneTickNonBlockingAsync();
            Assert.True(step.Advanced);
            if (step.Decisions.Any(item => item.AgentId == agentId && item.Admission.Accepted))
                return step;
        }
        throw new TimeoutException($"The completed hosted decision for {agentId} was not admitted within 20 ticks.");
    }

    private static WorldRuntime CreateNameTestWorld(
        string seed,
        IDecisionProvider provider,
        bool quietOthers = true)
    {
        // Naming-call counts must not include fresh decisions prompted by
        // peers changing the target's ordinary choices while a reply is held.
        var world = new WorldRuntime(seed, id => id == NameTargetId
                ? provider
                : quietOthers ? new QuietDecisionProvider() : new DeterministicDecisionProvider(),
            startPace: WorldStartPace.FounderSetup);
        world.PlaceFounder(NameTargetId, new GridPoint(0, 0));
        world.PlaceFounder(NameOwnerId, new GridPoint(1, 2));
        world.PlaceFounder("founder:cccccccccccccccccccccccccccccccc", new GridPoint(2, 2));
        world.PlaceFounder("founder:dddddddddddddddddddddddddddddddd", new GridPoint(3, 2));
        return world;
    }

    private sealed class QuietDecisionProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, "safe_idle", 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == "safe_idle" ? 1d : 0d, StringComparer.Ordinal)));
        }
    }

    private sealed record NameReply(
        string? Name,
        double Confidence = 1d,
        string SelectedCandidateId = "safe_idle");

    private sealed class SequencedModelProvider(
        IReadOnlyList<NameReply> replies,
        bool holdSecond = false,
        bool ignoreSecondCancellation = false,
        bool failSecond = false) : IDecisionProvider
    {
        private int callCount;
        private readonly TaskCompletionSource<CognitionDecisionResponse> heldSecondReply =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CognitionDecisionResponse? secondReply;
        private Task<CognitionDecisionResponse>? secondResponseTask;

        public SequencedModelProvider(params NameReply[] replies)
            : this((IReadOnlyList<NameReply>)replies)
        {
        }

        public ConcurrentQueue<AgentObservation> ObservedRequests { get; } = new();
        public TaskCompletionSource<bool> FirstReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondFailed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AgentObservation? SecondObservation { get; private set; }
        public CancellationToken SecondCancellationToken { get; private set; }
        public int CallCount => Volatile.Read(ref callCount);
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public Task<CognitionDecisionResponse> CompleteSecondReply()
        {
            // Complete the actual provider response on the test thread. A separate
            // post-release continuation would only measure thread-pool scheduling.
            Assert.True(heldSecondReply.TrySetResult(Assert.IsType<CognitionDecisionResponse>(secondReply)));
            return Assert.IsAssignableFrom<Task<CognitionDecisionResponse>>(secondResponseTask);
        }

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref callCount);
            var observation = request.Observation;
            ObservedRequests.Enqueue(observation);
            if (failSecond && observation.IsNameRetry)
            {
                if (call == 2)
                {
                    SecondObservation = observation;
                    SecondCancellationToken = cancellationToken;
                    SecondStarted.TrySetResult(true);
                    SecondFailed.TrySetResult(true);
                }
                return ValueTask.FromException<CognitionDecisionResponse>(new HttpRequestException("test transport failure"));
            }

            var reply = replies[Math.Min(call - 1, replies.Count - 1)];
            var selected = reply.SelectedCandidateId;
            var probabilities = observation.Candidates.ToDictionary(candidate => candidate.Id,
                candidate => candidate.Id == selected ? 1d : 0d, StringComparer.Ordinal);
            var response = new CognitionDecisionResponse(
                request.RequestId,
                observation.AgentId,
                Kind,
                ProviderEpoch,
                observation.RunEpoch,
                observation.DecisionGeneration,
                observation.ObservationDigest,
                selected,
                reply.Confidence,
                probabilities,
                Usage: new CognitionUsage("test-model", 10, 2),
                ChosenName: reply.Name);
            if (call == 2)
            {
                SecondObservation = observation;
                SecondCancellationToken = cancellationToken;
                secondReply = response;
                if (holdSecond)
                    secondResponseTask = ignoreSecondCancellation
                        ? heldSecondReply.Task
                        : heldSecondReply.Task.WaitAsync(cancellationToken);
                SecondStarted.TrySetResult(true);
                if (holdSecond)
                    return new ValueTask<CognitionDecisionResponse>(secondResponseTask!);
            }
            if (call == 1) FirstReturned.TrySetResult(true);
            return ValueTask.FromResult(response);
        }
    }

    private sealed class FixedHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class SequencedNameResponseHandler(int firstReplyDelayMilliseconds, params string[] names) : HttpMessageHandler
    {
        private int requestCount;

        public int RequestCount => Volatile.Read(ref requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref requestCount);
            _ = await request.Content!.ReadAsStringAsync(cancellationToken);
            // The delayed row outlives the former three-second admission poll.
            if (call == 1) await Task.Delay(firstReplyDelayMilliseconds, cancellationToken);
            var answer = JsonSerializer.Serialize(new
            {
                selected_candidate_id = "safe_idle",
                confidence = 1d,
                chosen_name = names[Math.Min(call - 1, names.Length - 1)],
            });
            var body = JsonSerializer.Serialize(new
            {
                model = "retry-meter-model",
                choices = new[] { new { message = new { role = "assistant", content = answer } } },
                usage = new { prompt_tokens = 10, completion_tokens = 2 },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class HeldModelProvider(
        bool ignoreCancellation = false,
        DecisionProviderKind kind = DecisionProviderKind.Jev,
        string? privateThought = null,
        string? chosenName = null) : IDecisionProvider
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DecisionProviderKind Kind => kind;
        public long ProviderEpoch => 1;
        public bool NeedsNameObserved { get; private set; }

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            NeedsNameObserved = request.Observation.NeedsName;
            Started.TrySetResult(true);
            if (ignoreCancellation) await Release.Task;
            else await Release.Task.WaitAsync(cancellationToken);
            var selected = request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            var probabilities = request.Observation.Candidates.ToDictionary(item => item.Id,
                item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal);
            Returned.TrySetResult(true);
            return new CognitionDecisionResponse(
                request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1d, probabilities,
                PrivateThought: privateThought, ChosenName: chosenName);
        }
    }

    private sealed class HeldGuidanceReplyProvider(string replyText) : IDecisionProvider
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<AgentObservation> ObservedRequests { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Enqueue(request.Observation);
            Started.TrySetResult(true);
            await Release.Task;
            var observation = request.Observation;
            var selected = observation.Candidates.Single(item => item.Id == "safe_idle");
            var probabilities = observation.Candidates.ToDictionary(item => item.Id,
                item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal);
            var firstMessage = observation.ObserverGuidance?.FirstOrDefault(message => message.ReplyAllowed);
            var response = new CognitionDecisionResponse(
                request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                selected.Id, 1d, probabilities,
                ObserverReplies: firstMessage is null ? [] : [new(firstMessage.InstructionId, replyText)]);
            Returned.TrySetResult(true);
            return response;
        }
    }
}
