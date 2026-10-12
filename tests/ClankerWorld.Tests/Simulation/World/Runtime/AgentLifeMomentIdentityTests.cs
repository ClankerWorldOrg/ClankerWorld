using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class AgentLifeMomentIdentityTests
{
    private const string ActorId = "founder:00000000000000000000000000000001";
    private const string Personality = "Patient and curious";
    private const string Aspiration = "Explore the riverbanks";

    [Fact]
    public async Task MidlifeUsesASeparatePersonalRequestAndKeepsTheChangeAcrossReload()
    {
        var handler = new IdentityHandler();
        using var client = new HttpClient(handler);
        var model = new OpenAiCompatibleDecisionProvider(client, () => "synthetic-key",
            new Uri("https://model.test/v1/chat/completions"), "synthetic-model");
        using var world = WorldRuntime.Restore(MidlifeState(), Route(model));
        await UntilResolved(world);
        var person = world.Inhabitants.Single(item => item.InhabitantId == ActorId);
        Assert.Equal("More willing to take chances", person.Personality);
        Assert.Equal("Build a lasting home", person.Aspiration);
        var moment = Assert.Single(person.IdentityMoments!);
        Assert.Equal(AgentIdentityMomentKind.Midlife, moment.Kind);
        Assert.Equal("accepted", moment.Outcome);
        Assert.Single(handler.MomentBodies);
        using var question = JsonDocument.Parse(handler.MomentBodies.Single());
        Assert.Equal(moment.Reason, question.RootElement.GetProperty("identity_moment").GetString());
        Assert.Equal(Personality, question.RootElement.GetProperty("self").GetProperty("personality").GetString());
        Assert.False(question.RootElement.GetProperty("needs_name").GetBoolean());
        Assert.Single(question.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.Contains(new OwnerWorldObservationStore(world).GetSnapshot().Inhabitants
            .Single(item => item.Id == ActorId).DecisionFactors,
            item => item.Key == "identity-change" && item.Detail.Contains(moment.Reason, StringComparison.Ordinal));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_identity_revised");
        Assert.DoesNotContain(world.ExportState().Events,
            item => item.Detail.Contains(person.Personality, StringComparison.Ordinal) ||
                item.Detail.Contains(person.Aspiration, StringComparison.Ordinal));
        Assert.DoesNotContain(world.ExportState().Society.Cognition.Runtimes,
            runtime => runtime.CurrentIntention?.CandidateId == "identity_optional");
        for (var tick = 0; tick < 35 && !handler.RoutineBodies.Any(body =>
                     body.Contains("More willing to take chances", StringComparison.Ordinal)); tick++)
        {
            await world.AdvanceOneTickNonBlockingAsync();
            await Task.Delay(10);
        }
        Assert.Contains(handler.RoutineBodies, body =>
            body.Contains("More willing to take chances", StringComparison.Ordinal) &&
            body.Contains("Build a lasting home", StringComparison.Ordinal));
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Route(model));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Resume();
        for (var tick = 0; tick < 12; tick++) await restored.AdvanceOneTickNonBlockingAsync();
        Assert.Equal(person.Personality, restored.Inhabitants.Single(item => item.InhabitantId == ActorId).Personality);
        Assert.Single(handler.MomentBodies);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bad\ntext")]
    [InlineData("")]
    public async Task MissingDeclinedOrInvalidReplyKeepsBothFieldsWithoutRetry(string? personality)
    {
        var handler = new IdentityHandler(personality, personality is null ? null : "A valid aspiration");
        using var client = new HttpClient(handler);
        var model = new OpenAiCompatibleDecisionProvider(client, () => "synthetic-key",
            new Uri("https://model.test/v1/chat/completions"), "synthetic-model");
        using var world = WorldRuntime.Restore(MidlifeState(), Route(model));
        await UntilResolved(world);
        AssertKept(world);
        for (var tick = 0; tick < 12; tick++) await world.AdvanceOneTickNonBlockingAsync();
        world.Pause();
        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), Route(model));
        restored.Resume();
        for (var tick = 0; tick < 5; tick++) await restored.AdvanceOneTickNonBlockingAsync();
        AssertKept(restored);
        Assert.Single(handler.MomentBodies);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("disconnect")]
    [InlineData("provider")]
    [InlineData("reload")]
    public async Task CancelledOrStaleRequestCannotChangeIdentityOrRetry(string interruption)
    {
        var provider = new HeldIdentityProvider();
        using var world = WorldRuntime.Restore(MidlifeState(), Route(provider));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("requested", world.Inhabitants.Single(item => item.InhabitantId == ActorId).IdentityMoments!.Single().Outcome);
        var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        if (interruption == "pause") world.Pause();
        else if (interruption == "disconnect" || interruption == "reload") world.CancelPendingModelCalls();
        else provider.Epoch++;
        provider.Release.TrySetResult(true);
        await provider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var restored = interruption == "reload" ? WorldRuntime.Restore(saved, Route(provider)) : null;
        var current = restored ?? world;
        current.Resume();
        for (var tick = 0; tick < 8; tick++) await current.AdvanceOneTickNonBlockingAsync();
        Assert.Equal(Personality, current.Inhabitants.Single(item => item.InhabitantId == ActorId).Personality);
        Assert.Equal(Aspiration, current.Inhabitants.Single(item => item.InhabitantId == ActorId).Aspiration);
        Assert.DoesNotContain(current.ExportState().Events, item => item.Kind == "agent_identity_revised");
        Assert.Equal("interrupted", current.Inhabitants.Single(item => item.InhabitantId == ActorId).IdentityMoments!.Single().Outcome);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task PresenceMustAllowTheTickBeforeAnyExtraModelCall()
    {
        var provider = new HeldIdentityProvider();
        using var world = WorldRuntime.Restore(MidlifeState(), Route(provider));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickNonBlockingAsync(() => false)).Advanced);
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public async Task ReplyAtDeathCannotApplyAndArchivedAttemptEndsAsInterrupted()
    {
        var state = MidlifeState();
        var checkpoint = state.Society.Society;
        state = state with
        {
            Society = state.Society with
            {
                Society = checkpoint with
                {
                    Inhabitants = checkpoint.Inhabitants.Select(item => item.Id == ActorId ? item with
                    {
                        BirthTick = checkpoint.WorldTick - 60L * checkpoint.Config.TicksPerWorldDay + 2,
                        AgeBand = SocietyAgeBand.Elder,
                        LastLifecycleYearChecked = 59,
                    } : item).ToArray(),
                },
            },
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == ActorId ? item with
            {
                IdentityMoments = [new(AgentIdentityMomentKind.Midlife, 0, "kept", CompletedTick: 0)],
            } : item).ToArray(),
        };
        var provider = new HeldIdentityProvider();
        using var world = WorldRuntime.Restore(state, Route(provider));
        await world.AdvanceOneTickNonBlockingAsync();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        provider.Release.TrySetResult(true);
        await provider.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(10);
        await world.AdvanceOneTickNonBlockingAsync();
        var deceased = world.ExportState().DeceasedInhabitants!.Single(item => item.InhabitantId == ActorId);
        Assert.Equal(Personality, deceased.LastPhysical.Personality);
        Assert.Equal("interrupted", deceased.LastPhysical.IdentityMoments!
            .Single(item => item.Kind == AgentIdentityMomentKind.Elder).Outcome);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_identity_revised");
        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), Route(provider));
        for (var tick = 0; tick < 3; tick++) await restored.AdvanceOneTickNonBlockingAsync();
        Assert.Equal(1, provider.CallCount);
        Assert.Equal("interrupted", restored.ExportState().DeceasedInhabitants!
            .Single(item => item.InhabitantId == ActorId).LastPhysical.IdentityMoments!
            .Single(item => item.Kind == AgentIdentityMomentKind.Elder).Outcome);
    }

    [Fact]
    public async Task HostLogsTheChangedActorWithoutIdentityText()
    {
        var directory = Directory.CreateTempSubdirectory("life-moment-log-");
        try
        {
            var provider = new ImmediateIdentityProvider();
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var world = WorldRuntime.Restore(MidlifeState(), Route(provider));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            using var service = new WorldRuntimeService(world,
                new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            presence.RecordAuthenticatedReconnect("owner");
            for (var tick = 0; tick < 30 && !logger.Messages.Any(item => item.Contains("agent_identity_changed", StringComparison.Ordinal)); tick++)
            {
                Assert.True(await service.TryAdvanceOnceAsync());
                await Task.Delay(10);
            }
            Assert.Contains(logger.Messages, item => item.Contains("agent_identity_changed", StringComparison.Ordinal) &&
                item.Contains(ActorId, StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, item => item.Contains("More willing to take chances", StringComparison.Ordinal) ||
                item.Contains("Build a lasting home", StringComparison.Ordinal) || item.Contains(Personality, StringComparison.Ordinal));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task FiveDistinctLifeMomentsHaveAFiveCallLifetimeLimit()
    {
        var state = MidlifeState();
        var people = state.Society.Society.Inhabitants.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var partnerId = people[1].Id;
        var parentId = people[2].Id;
        var childId = people[3].Id;
        var checkpoint = state.Society.Society;
        var tick = checkpoint.WorldTick;
        checkpoint = checkpoint with
        {
            Relationships = checkpoint.Relationships.Concat(new SocietyRelationship[]
            {
                new("moment-partner", 1, SocietyRelationshipType.Partnership, ActorId, partnerId,
                    SocietyRelationshipState.Accepted, SocietyConsentState.Accepted, tick, tick, "family"),
                new("moment-parent", 1, SocietyRelationshipType.BiologicalParentage, parentId, ActorId,
                    SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle, tick, tick, "family"),
                new("moment-child", 1, SocietyRelationshipType.BiologicalParentage, ActorId, childId,
                    SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle, tick, tick, "family"),
            }).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Inhabitants = checkpoint.Inhabitants.Select(item => item.Id == ActorId ? item with
            {
                BirthTick = tick - 45L * checkpoint.Config.TicksPerWorldDay,
                BirthLifeTick = null,
                AgeBand = SocietyAgeBand.Elder,
                LastLifecycleYearChecked = 45,
            } : item).ToArray(),
        };
        using var society = SocietyRuntime.Restore(state.Society with { Society = checkpoint });
        society.Apply(current => SocietyRules.Kill(current, partnerId, SocietyDeathCause.Accident));
        society.Apply(current => SocietyRules.Kill(current, parentId, SocietyDeathCause.Accident));
        var dead = new[] { partnerId, parentId };
        var postDeath = society.ExportState();
        state = state with
        {
            Society = postDeath,
            Inhabitants = state.Inhabitants.Where(item => !dead.Contains(item.InhabitantId)).ToArray(),
            DeceasedInhabitants = dead.Select(id => new DeceasedInhabitantState(id, tick,
                checkpoint.AgeAt(checkpoint.GetInhabitant(id), tick),
                state.Inhabitants.Single(item => item.InhabitantId == id))).ToArray(),
            Towns = state.Towns!.Select(town =>
            {
                var residents = town.ResidentIds.Where(id => !dead.Contains(id)).ToArray();
                var adults = residents.Where(id => postDeath.Society.Inhabitants.Any(person => person.Id == id &&
                    person.Status == SocietyInhabitantStatus.Active &&
                    person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder));
                // Settle council membership after both deaths without replacing its history.
                return town with
                {
                    ResidentIds = residents,
                    Governance = TownGovernanceRules.Advance(town.Governance!, town.Id, state.WorldSeed,
                        adults, postDeath.Society.WorldTick, state.WorldSystems!.Config.TicksPerDay),
                };
            }).ToArray(),
        };
        var provider = new ImmediateIdentityProvider();
        using var world = WorldRuntime.Restore(state, Route(provider));
        for (var tickIndex = 0; tickIndex < 25 && provider.Calls.Count < 5; tickIndex++)
        {
            await world.AdvanceOneTickNonBlockingAsync();
            await Task.Delay(10);
        }
        await UntilResolved(world);
        Assert.Equal(5, provider.Calls.Count);
        Assert.Equal(5, world.Inhabitants.Single(item => item.InhabitantId == ActorId).IdentityMoments!.Count);
        Assert.Equal(5, provider.Calls.Select(request => request.Observation.IdentityMoment).Distinct().Count());
        world.Pause();
        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), Route(provider));
        restored.Resume();
        for (var index = 0; index < 8; index++) await restored.AdvanceOneTickNonBlockingAsync();
        Assert.Equal(5, provider.Calls.Count);
    }

    [Fact]
    public void DamagedOrRepeatedLifeMomentHistoryIsRefused()
    {
        var state = MidlifeState();
        var moment = new AgentIdentityMoment(AgentIdentityMomentKind.Midlife, 0);
        var duplicate = state with
        {
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == ActorId ?
                item with { IdentityMoments = [moment, moment] } : item).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(duplicate));
        var invalid = state with
        {
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == ActorId ?
                item with { IdentityMoments = [moment with { Outcome = "accepted", CompletedTick = 0, Personality = "bad\ntext" }] } : item).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalid));
        // While the alpha accepts only the current schema, the minimum-schema
        // cutoff refuses an older save before its life moments are checked.
        var older = state with
        {
            SchemaVersion = WorldRuntime.LifeMomentIdentitySchemaVersion - 1,
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == ActorId ?
                item with { IdentityMoments = [moment] } : item).ToArray(),
        };
        var refused = Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(older));
        Assert.Contains("older than the minimum supported schema", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRouterUsesOnlyTheActorsSelectedModelAndMetersItsOneOpportunity(bool selected)
    {
        var directory = Directory.CreateTempSubdirectory("life-moment-routing-");
        try
        {
            var configuration = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));
            configuration.Configure(new("planning", "openai", "world-default-model", "synthetic-world-key", false));
            if (selected)
                configuration.Configure(new("personal", "openai", "agent-life-model", "synthetic-agent-key", false, ActorId));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            var logger = new RecordingLogger<ConfigurableDecisionProvider>();
            using var handler = new IdentityHandler();
            var router = new ConfigurableDecisionProvider(configuration, new ClientFactory(handler), logger, usageStore: usage);
            using var world = WorldRuntime.Restore(MidlifeState(), Route(router));
            await UntilResolved(world);
            Assert.Equal(selected ? 1 : 0, handler.MomentBodies.Count);
            Assert.Equal(selected ? 1 : 0, usage.Capture().Attempts);
            if (selected)
            {
                Assert.Equal("agent-life-model", Assert.Single(handler.Models));
                Assert.Equal("planning", Assert.Single(usage.Capture().Rows).Role);
            }
            else AssertKept(world);
            Assert.DoesNotContain(logger.Messages, message => message.Contains("More willing to take chances", StringComparison.Ordinal) ||
                message.Contains("Build a lasting home", StringComparison.Ordinal));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ReachingUsageCapPausesBeforeTheReplyCanApplyOrRetry()
    {
        var directory = Directory.CreateTempSubdirectory("life-moment-usage-cap-");
        try
        {
            var configuration = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));
            configuration.Configure(new("personal", "openai", "agent-life-model", "synthetic-agent-key", false, ActorId));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            usage.Configure(new ProviderUsageLimitAction(1));
            using var handler = new IdentityHandler();
            var router = new ConfigurableDecisionProvider(configuration, new ClientFactory(handler), usageStore: usage);
            using var world = WorldRuntime.Restore(MidlifeState(), Route(router));
            var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void PauseAtLimit()
            {
                world.Pause();
                paused.TrySetResult(true);
            }
            usage.LimitReached += PauseAtLimit;
            await world.AdvanceOneTickNonBlockingAsync();
            // Wait for the real accounting callback to finish pausing. A short
            // polling window raced the background provider on busy CI runners.
            await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(world.Society.IsPaused);
            Assert.Single(handler.MomentBodies);
            Assert.Equal(1, usage.Capture().Attempts);
            Assert.Equal(Personality, world.Inhabitants.Single(item => item.InhabitantId == ActorId).Personality);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_identity_revised");
            Assert.Equal("interrupted", Assert.Single(world.Inhabitants.Single(item => item.InhabitantId == ActorId).IdentityMoments!).Outcome);
            usage.LimitReached -= PauseAtLimit;
            world.Resume();
            for (var tick = 0; tick < 8; tick++) await world.AdvanceOneTickNonBlockingAsync();
            Assert.Single(handler.MomentBodies);
            Assert.Equal(1, usage.Capture().Attempts);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static Func<string, IDecisionProvider> Route(IDecisionProvider provider) =>
        id => id == ActorId ? provider : new DeterministicDecisionProvider();

    [Theory]
    [InlineData("admitted")]
    [InlineData("cancelled")]
    [InlineData("write_failed")]
    public async Task PreparedIdentityOpportunityIsDurableBeforeDispatchAndNeverRepeats(string boundary)
    {
        var directory = Directory.CreateTempSubdirectory("prepared-identity-");
        var provider = new HeldIdentityProvider();
        try
        {
            using var world = WorldRuntime.Restore(MidlifeState(), Route(provider));
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), Route(provider));
            file.Save(world);
            var baseline = File.ReadAllBytes(file.Path);
            var permitted = true;
            var writes = 0;
            Action Prepare(WorldRuntime proposed)
            {
                var write = file.PrepareSave(proposed);
                if (boundary == "cancelled") permitted = false;
                return () =>
                {
                    writes++;
                    if (boundary == "write_failed") throw new IOException("Controlled write refusal.");
                    write();
                };
            }
            var tick = world.AdvanceOneTickWithCheckpointAsync(Prepare, () => permitted,
                failurePolicy: WorldTickFailurePolicy.RefuseSingleAction).AsTask();
            if (boundary == "write_failed") await Assert.ThrowsAsync<IOException>(() => tick);
            else Assert.Equal(boundary == "admitted", (await tick).Advanced);
            if (boundary == "cancelled")
            {
                Assert.Equal(0, writes);
                Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
                Assert.Equal(baseline, File.ReadAllBytes(file.Path));
                Assert.Equal(0, provider.CallCount);
                return;
            }
            Assert.Equal(1, writes);
            if (boundary == "admitted")
            {
                await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var saved = WorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path));
                Assert.Equal("requested", Assert.Single(saved.Inhabitants.Single(person => person.InhabitantId == ActorId).IdentityMoments!).Outcome);
                Assert.Single(saved.Events, item => item.Kind == "agent_identity_moment_started" && item.Detail.StartsWith(ActorId, StringComparison.Ordinal));
                world.Pause();
                using var restored = file.LoadOrCreate(saved.WorldSeed);
                restored.Resume();
                for (var step = 0; step < 3; step++) await restored.AdvanceOneTickNonBlockingAsync();
                Assert.Equal("interrupted", Assert.Single(restored.Inhabitants.Single(person => person.InhabitantId == ActorId).IdentityMoments!).Outcome);
                Assert.Equal(1, provider.CallCount);
            }
            else
            {
                Assert.Equal(baseline, File.ReadAllBytes(file.Path));
                Assert.Equal(0, provider.CallCount);
                Assert.Equal("requested", Assert.Single(world.Inhabitants.Single(person => person.InhabitantId == ActorId).IdentityMoments!).Outcome);
                world.Pause();
                file.Save(world);
                world.Resume();
                for (var step = 0; step < 3; step++) await world.AdvanceOneTickNonBlockingAsync();
                Assert.Equal("interrupted", Assert.Single(world.Inhabitants.Single(person => person.InhabitantId == ActorId).IdentityMoments!).Outcome);
                Assert.Equal(0, provider.CallCount);
            }
        }
        finally
        {
            provider.Release.TrySetResult(true);
            directory.Delete(recursive: true);
        }
    }

    private static WorldRuntimeState MidlifeState()
    {
        using var world = NormalPathWorld.CreateGenerated("life-moment-identity", _ => new DeterministicDecisionProvider());
        var state = world.ExportState();
        var checkpoint = state.Society.Society;
        return state with
        {
            Society = state.Society with
            {
                Society = checkpoint with
                {
                    Inhabitants = checkpoint.Inhabitants.Select(item => item.Id == ActorId ? item with
                    {
                        BirthTick = checkpoint.WorldTick - 30L * checkpoint.Config.TicksPerWorldDay + 1,
                        BirthLifeTick = null,
                        LastLifecycleYearChecked = 29,
                        NeedsName = false,
                    } : item).ToArray(),
                },
            },
            Inhabitants = state.Inhabitants.Select(item => item.InhabitantId == ActorId ? item with
            {
                IdentityChoicePending = false,
                Personality = Personality,
                Aspiration = Aspiration,
            } : item).ToArray(),
        };
    }

    private static void AssertKept(WorldRuntime world)
    {
        var person = world.Inhabitants.Single(item => item.InhabitantId == ActorId);
        Assert.Equal(Personality, person.Personality);
        Assert.Equal(Aspiration, person.Aspiration);
        Assert.Equal("kept", Assert.Single(person.IdentityMoments!).Outcome);
    }

    private static async Task UntilResolved(WorldRuntime world)
    {
        for (var attempt = 0; attempt < 35; attempt++)
        {
            await world.AdvanceOneTickNonBlockingAsync();
            if (world.Inhabitants.Single(item => item.InhabitantId == ActorId).IdentityMoments is { Count: > 0 } moments &&
                moments.All(item => item.Outcome is not ("waiting" or "requested"))) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("The synthetic life-moment reply did not complete.");
    }

    private sealed class IdentityHandler(string? personality = "More willing to take chances",
        string? aspiration = "Build a lasting home") : HttpMessageHandler
    {
        public ConcurrentQueue<string> MomentBodies { get; } = new();
        public ConcurrentQueue<string> RoutineBodies { get; } = new();
        public ConcurrentQueue<string> Models { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var questionText = payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
            using var question = JsonDocument.Parse(questionText);
            var isMoment = question.RootElement.TryGetProperty("identity_moment", out var moment) && moment.ValueKind == JsonValueKind.String;
            if (isMoment)
            {
                MomentBodies.Enqueue(questionText);
                Models.Enqueue(payload.RootElement.GetProperty("model").GetString()!);
            }
            else RoutineBodies.Enqueue(questionText);
            var answer = JsonSerializer.Serialize(new
            {
                selected_candidate_id = isMoment ? "identity_optional" : "safe_idle",
                confidence = 1d,
                chosen_personality = isMoment ? personality : "Unrequested overwrite",
                chosen_aspiration = isMoment ? aspiration : "Unrequested overwrite",
            });
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = answer } } },
                }), Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ImmediateIdentityProvider : IDecisionProvider
    {
        public ConcurrentQueue<CognitionDecisionRequest> Calls { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.IdentityMoment is not null) Calls.Enqueue(request);
            return ValueTask.FromResult(Reply(request));
        }
    }

    private sealed class HeldIdentityProvider : IDecisionProvider
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }
        public long Epoch { get; set; } = 1;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => Epoch;
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.IdentityMoment is null) return Reply(request);
            CallCount++;
            Started.TrySetResult(true);
            await Release.Task;
            Returned.TrySetResult(true);
            return Reply(request);
        }
    }

    private static CognitionDecisionResponse Reply(CognitionDecisionRequest request) => new(
        request.RequestId, request.Observation.InhabitantId, DecisionProviderKind.LargeLanguageModel,
        request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
        request.Observation.ObservationDigest, request.Observation.IdentityMoment is null ? "safe_idle" : "identity_optional",
        1, new Dictionary<string, double>(), ChosenPersonality: "More willing to take chances", ChosenAspiration: "Build a lasting home");
}
