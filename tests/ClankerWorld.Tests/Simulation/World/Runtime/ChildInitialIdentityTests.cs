using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class ChildInitialIdentityTests
{
    private static readonly Lazy<Task<byte[]>> Born = new(CreateBornAsync);
    private static readonly Lazy<Task<byte[]>> InfancyEnds = new(CreateBeforeChildhoodAsync);
    private static readonly Lazy<Task<byte[]>> LongParentInfancyEnds = new(CreateLongParentBeforeChildhoodAsync);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANativeBornLongIdParentRemainsInItsChildsOrdinarySavedFamilyBackground(bool delayRequestStart)
    {
        var state = WorldRuntimeCodec.Decode(await LongParentInfancyEnds.Value);
        var parentId = Assert.Single(state.Society.Society.Births, birth =>
            state.Society.Society.GetAgent(birth.ChildId).AgeBand == SocietyAgeBand.Adult).ChildId;
        var childId = Assert.Single(state.Society.Society.Births, birth => birth.ChildId != parentId).ChildId;
        Assert.InRange(parentId.Length, 1, DerivedId.MaximumLength);
        Assert.Equal(SocietyAgeBand.Infant, state.Society.Society.GetAgent(childId).AgeBand);

        using var handler = new ChildReplyHandler
        {
            Release = new(TaskCreationOptions.RunContinuationsAsynchronously),
            StartGate = delayRequestStart ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null,
        };
        using var client = new HttpClient(handler);
        var model = PersonalModel(client);
        using var world = WorldRuntime.Restore(state, id => id == childId ? model : new QuietProvider());
        // Normal aging is already restored before the first ordinary child
        // request. Delaying its start must not age this child into an adult.
        for (var tick = 0; tick < 24 && !handler.Started.Task.IsCompleted; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        handler.StartGate?.TrySetResult(true);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(SocietyAgeBand.Child, world.Society.GetAgent(childId).AgeBand);
        var observation = Assert.Single(world.ExportState().Society.Cognition.Queue, entry => entry.AgentId == childId).Observation;
        var background = Assert.IsAssignableFrom<IReadOnlyList<CognitionParentIdentity>>(observation.Self!.FamilyBackground);
        Assert.Equal(parentId, Assert.Single(background, parent => parent.ParentId == parentId).ParentId);
        Assert.Equal(2, background.Count);
        observation.Validate();
        var directory = Directory.CreateTempSubdirectory("native-parent-background-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var saved = File.ReadAllBytes(file.Path);
            var loaded = WorldRuntimeCodec.Decode(saved);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded));
            var retained = Assert.Single(loaded.Society.Cognition.Queue, entry => entry.AgentId == childId).Observation;
            Assert.Equal(parentId, Assert.Single(retained.Self!.FamilyBackground!, parent => parent.ParentId == parentId).ParentId);

            handler.Release.TrySetResult(true);
            await WaitForActualRequests(world);
            for (var tick = 0; tick < 8 && world.Agents.Single(person => person.AgentId == childId).IdentityChoicePending; tick++)
                Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var child = world.Agents.Single(person => person.AgentId == childId);
            Assert.False(child.IdentityChoicePending);
            Assert.Equal(("Inventive and independent", "Study the hills"), (child.Personality, child.Aspiration));
            Assert.Single(handler.Bodies);
            Assert.All(world.ExportState().Society.Cognition.Queue.Where(entry => entry.AgentId == childId),
                entry => Assert.Null(entry.Observation.Self!.FamilyBackground));
            file.Save(world);
            saved = File.ReadAllBytes(file.Path);
            using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved),
                id => id == childId ? model : new QuietProvider());
            Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
            for (var tick = 0; tick < 2; tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
                Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            }
        }
        finally
        {
            handler.Release.TrySetResult(true);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task NativeBirthKeepsInitialIdentityPendingThroughInfancyAndReload()
    {
        var state = WorldRuntimeCodec.Decode(await Born.Value);
        var birth = Assert.Single(state.Society.Society.Births);
        var child = Assert.Single(state.Agents, person => person.AgentId == birth.ChildId);
        Assert.Equal(SocietyAgeBand.Infant, state.Society.Society.GetAgent(birth.ChildId).AgeBand);
        Assert.True(child.IdentityChoicePending);
        Assert.Equal(("undecided", "find a purpose"), (child.Personality, child.Aspiration));
        using var restored = WorldRuntime.Restore(state);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.True(restored.Agents.Single(person => person.AgentId == birth.ChildId).IdentityChoicePending);
        Assert.DoesNotContain(restored.ExportState().Events, item => item.Kind.StartsWith("agent_identity_", StringComparison.Ordinal) && item.Detail == birth.ChildId);
        var saved = WorldRuntimeCodec.Encode(restored.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    [Theory]
    [InlineData(ModelNeedFormat.Numbers)]
    [InlineData(ModelNeedFormat.Words)]
    public async Task FirstChildDecisionChoosesItsOwnIdentityAtDayThreeWithOnlyFamilyBackground(ModelNeedFormat format)
    {
        var state = await BeforeChildhoodAsync();
        var childId = Assert.Single(state.Society.Society.Births).ChildId;
        var parents = state.Society.Society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == childId)
            .Select(edge => edge.ProposerId).Order(StringComparer.Ordinal).ToArray();
        using var handler = new ChildReplyHandler();
        using var replayHandler = new ChildReplyHandler();
        using var client = new HttpClient(handler);
        using var replayClient = new HttpClient(replayHandler);
        var model = PersonalModel(client, format);
        var replayModel = PersonalModel(replayClient, format);
        using var world = WorldRuntime.Restore(state, id => id == childId ? model : new QuietProvider());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == childId ? replayModel : new QuietProvider());
        Assert.Equal(SocietyAgeBand.Infant, world.Society.GetAgent(childId).AgeBand);
        Assert.Empty(handler.Bodies);
        for (var tick = 0; tick < 8 && world.Agents.Single(person => person.AgentId == childId).IdentityChoicePending; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForActualRequests(world);
            Assert.True((await replay.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForActualRequests(replay);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal(3, world.Society.AgeAt(world.Society.GetAgent(childId), world.WorldTick));
        var child = world.Agents.Single(person => person.AgentId == childId);
        Assert.False(child.IdentityChoicePending);
        Assert.Equal(("Inventive and independent", "Study the hills"), (child.Personality, child.Aspiration));
        var body = Assert.Single(handler.Bodies);
        using var question = JsonDocument.Parse(body);
        var self = question.RootElement.GetProperty("self");
        Assert.Equal("Child", self.GetProperty("life_stage").GetString());
        Assert.True(question.RootElement.GetProperty("needs_personality").GetBoolean());
        Assert.True(question.RootElement.GetProperty("needs_aspiration").GetBoolean());
        Assert.Equal(state.Society.Society.GetHousehold(world.Society.GetAgent(childId).HouseholdId!).Name,
            self.GetProperty("household").GetString());
        Assert.Equal(Assert.Single(state.Towns!, town => town.ResidentIds.Contains(childId)).Name, self.GetProperty("town").GetString());
        var background = self.GetProperty("family_background").EnumerateArray().ToArray();
        Assert.Equal(2, background.Length);
        for (var index = 0; index < parents.Length; index++)
        {
            var parent = state.Agents.Single(person => person.AgentId == parents[index]);
            Assert.Equal(state.Society.Society.GetAgent(parents[index]).Name, background[index].GetProperty("name").GetString());
            Assert.Equal(parent.Personality, background[index].GetProperty("personality").GetString());
            Assert.Equal(parent.Aspiration, background[index].GetProperty("aspiration").GetString());
        }
        Assert.DoesNotContain("parent-private-sentinel", body, StringComparison.Ordinal);
        Assert.DoesNotContain("unrelated-identity-sentinel", body, StringComparison.Ordinal);
        Assert.Single(world.ExportState().Events, item => item.Kind == "agent_identity_chosen" && item.Detail == childId);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())),
            id => id == childId ? model : new QuietProvider());
        Assert.True((await restored.AdvanceOneTickNonBlockingAsync()).Advanced);
        await WaitForActualRequests(restored);
        Assert.False(restored.Agents.Single(person => person.AgentId == childId).IdentityChoicePending);
        Assert.Single(handler.Bodies);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("partial")]
    [InlineData("refused")]
    public async Task UnusableInitialChildReplyStaysPendingAndRetriesOnAnOrdinaryDecisionAfterReload(string reply)
    {
        var state = await BeforeChildhoodAsync();
        var childId = Assert.Single(state.Society.Society.Births).ChildId;
        using var handler = new ChildReplyHandler { Reply = reply };
        using var client = new HttpClient(handler);
        var model = PersonalModel(client);
        using var world = WorldRuntime.Restore(state, id => id == childId ? model : new QuietProvider());
        await CompleteFirstChildReply(world, childId);
        var child = world.Agents.Single(person => person.AgentId == childId);
        Assert.True(child.IdentityChoicePending);
        Assert.Equal(("undecided", "find a purpose"), (child.Personality, child.Aspiration));
        Assert.Single(handler.Bodies);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind.StartsWith("agent_identity_", StringComparison.Ordinal) && item.Detail == childId);
        world.Pause();
        world.SetLifePace(1);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), id => id == childId ? model : new QuietProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.True(restored.Agents.Single(person => person.AgentId == childId).IdentityChoicePending);
        handler.Reply = "valid";
        restored.Resume();
        // Safe idle uses the normal 300-step reevaluation, not an immediate
        // identity retry. Other ordinary triggers may still prompt earlier.
        for (var tick = 0; tick < 310 && restored.Agents.Single(person => person.AgentId == childId).IdentityChoicePending; tick++)
        {
            Assert.True((await restored.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForActualRequests(restored);
        }
        Assert.False(restored.Agents.Single(person => person.AgentId == childId).IdentityChoicePending);
        Assert.Equal(2, handler.Bodies.Count);
        Assert.Single(restored.ExportState().Events, item => item.Kind == "agent_identity_chosen" && item.Detail == childId);
    }

    private static OpenAiCompatibleDecisionProvider PersonalModel(HttpClient client, ModelNeedFormat format = ModelNeedFormat.Numbers) =>
        new(client, () => "synthetic-key", new Uri("https://model.test/v1/chat/completions"), "synthetic-child-model", needFormat: format);

    [Fact]
    public async Task PausedChildRequestCannotAdmitLateIdentityAndReloadKeepsTheOpportunity()
    {
        var state = await BeforeChildhoodAsync();
        var childId = Assert.Single(state.Society.Society.Births).ChildId;
        using var held = new ChildReplyHandler { Release = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var client = new HttpClient(held);
        var model = PersonalModel(client);
        using var world = WorldRuntime.Restore(state, id => id == childId ? model : new QuietProvider());
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await held.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        world.Pause();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        held.Release.TrySetResult(true);
        await held.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(world.Agents.Single(person => person.AgentId == childId).IdentityChoicePending);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "agent_identity_chosen" && item.Detail == childId);
        using var reply = new ChildReplyHandler();
        using var retryClient = new HttpClient(reply);
        var retryModel = PersonalModel(retryClient);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), id => id == childId ? retryModel : new QuietProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Resume();
        await CompleteFirstChildReply(restored, childId);
        Assert.False(restored.Agents.Single(person => person.AgentId == childId).IdentityChoicePending);
        Assert.Single(held.Bodies);
        Assert.Single(reply.Bodies);
        Assert.Single(restored.ExportState().Events, item => item.Kind == "agent_identity_chosen" && item.Detail == childId);
    }

    private static async Task CompleteFirstChildReply(WorldRuntime world, string childId)
    {
        for (var tick = 0; tick < 8; tick++)
        {
            var step = await world.AdvanceOneTickNonBlockingAsync();
            Assert.True(step.Advanced);
            await WaitForActualRequests(world);
            if (step.Decisions.Any(decision => decision.AgentId == childId)) return;
        }
        throw new TimeoutException("The first child decision did not finish.");
    }

    private static async Task WaitForActualRequests(WorldRuntime world)
    {
        var field = typeof(WorldRuntime).GetField("pendingHosted", BindingFlags.Instance | BindingFlags.NonPublic);
        var pending = Assert.IsAssignableFrom<System.Collections.IDictionary>(field!.GetValue(world));
        var tasks = pending.Values.Cast<object>().Select(item => Assert.IsAssignableFrom<Task>(item.GetType().GetProperty("Task")!.GetValue(item))).ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static async Task<WorldRuntimeState> BeforeChildhoodAsync() => WorldRuntimeCodec.Decode(await InfancyEnds.Value);

    private static async Task<byte[]> CreateLongParentBeforeChildhoodAsync()
    {
        var state = WorldRuntimeCodec.Decode(await NewbornModelBindingCheckpointTests.BeforeBirth(78));
        string parentId;
        using (var firstGeneration = WorldRuntime.Restore(state, NewbornModelBindingCheckpointTests.Providers))
        {
            Assert.True((await firstGeneration.AdvanceOneTickAsync()).Advanced);
            parentId = Assert.Single(firstGeneration.Society.Births).ChildId;
            Assert.InRange(parentId.Length, 1, DerivedId.MaximumLength);
            firstGeneration.Pause();
            firstGeneration.SetLifePace(1460);
            firstGeneration.Resume();
            for (var tick = 0; tick < 40 && firstGeneration.Society.GetAgent(parentId).AgeBand != SocietyAgeBand.Adult; tick++)
                Assert.True((await firstGeneration.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(SocietyAgeBand.Adult, firstGeneration.Society.GetAgent(parentId).AgeBand);
            firstGeneration.Pause();
            firstGeneration.SetLifePace(1);
            firstGeneration.Resume();
            state = firstGeneration.ExportState();
        }

        const string partnerId = "founder:00000000000000000000000000000003";
        Assert.Equal(SocietyAgeBand.Adult, state.Society.Society.GetAgent(partnerId).AgeBand);
        var household = state.Society.Society.GetAgent(parentId).HouseholdId!;
        var home = state.WorldSimulation!.Buildings.Single(building => building.HouseholdId == household &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        using (var society = SocietyRuntime.Restore(state.Society))
        {
            society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
                new("native-grandchild-parents", 1, SocietyRelationshipType.Partnership, parentId, partnerId, checkpoint.WorldTick)));
            society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, "native-grandchild-parents", 1, partnerId));
            state = state with { Society = society.ExportState() };
        }
        var checkpoint = state.Society.Society;
        foreach (var person in checkpoint.Agents)
            checkpoint = ChosenBirthNameTestFixture.NameParent(checkpoint, person.Id);
        // Names, chosen traits, food and position are controlled fixture inputs;
        // both births, the long parent ID, aging and the observation are native.
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "native-grandchild-food", "food", household, 12,
                storageBuildingId: home.InstanceId),
        };
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == parentId || person.AgentId == partnerId ? home.Position : person.Position,
                HungerBasisPoints = 10_000,
                LastDecisionContext = null,
                Parenthood = null,
                IdentityChoicePending = false,
                Personality = "Patient native parent",
                Aspiration = "Supply the household",
            }).ToArray(),
        };
        string childId;
        using (var secondGeneration = WorldRuntime.Restore(state,
            id => new CookedBirthFixture.BirthChooser(id, parentId, partnerId)))
        {
            for (var tick = 0; tick < 650 && secondGeneration.Society.Births.Count == 1; tick++)
                Assert.True((await secondGeneration.AdvanceOneTickAsync()).Advanced);
            var birth = Assert.Single(secondGeneration.Society.Births, birth => birth.ChildId != parentId);
            childId = birth.ChildId;
            Assert.Contains(secondGeneration.Society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
                edge.ProposerId == parentId && edge.TargetId == childId);
            state = secondGeneration.ExportState();
        }

        using var world = WorldRuntime.Restore(state, _ => new QuietProvider());
        await ApproachChildhoodAtOrdinaryRate(world, childId);
        return WorldRuntimeCodec.Encode(world.ExportState());
    }

    private static async Task<byte[]> CreateBeforeChildhoodAsync()
    {
        var state = WorldRuntimeCodec.Decode(await Born.Value);
        var childId = Assert.Single(state.Society.Society.Births).ChildId;
        var parents = state.Society.Society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == childId)
            .Select(edge => edge.ProposerId).Order(StringComparer.Ordinal).ToArray();
        var society = state.Society.Society;
        foreach (var parent in parents) society = ChosenBirthNameTestFixture.NameParent(society, parent);
        state = state with
        {
            Society = state.Society with { Society = society },
            Agents = state.Agents.Select(person => person with
            {
                Personality = person.AgentId == childId ? person.Personality : parents.Contains(person.AgentId)
                    ? "Patient grower " + Array.IndexOf(parents, person.AgentId) : "unrelated-identity-sentinel",
                Aspiration = person.AgentId == childId ? person.Aspiration : "Supply the Town",
                IdentityChoicePending = person.AgentId == childId,
                RecentThoughts = parents.Contains(person.AgentId) ? [new(state.Society.Society.WorldTick, "parent-private-sentinel")] : person.RecentThoughts,
            }).ToArray()
        };
        using var world = WorldRuntime.Restore(state, _ => new QuietProvider());
        await ApproachChildhoodAtOrdinaryRate(world, childId);
        return WorldRuntimeCodec.Encode(world.ExportState());
    }

    private static async Task ApproachChildhoodAtOrdinaryRate(WorldRuntime world, string childId)
    {
        world.Pause();
        world.SetLifePace(365);
        world.Resume();
        while (world.Society.AgeAt(world.Society.GetAgent(childId), world.WorldTick + 1) < 3)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(SocietyAgeBand.Infant, world.Society.GetAgent(childId).AgeBand);
        }
        // Finish the boundary at the ordinary rate so completing the reply
        // does not age the child another whole day.
        world.Pause();
        world.SetLifePace(1);
        world.Resume();
        while (world.Society.AgeAt(world.Society.GetAgent(childId), world.WorldTick + 1) < 3)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(SocietyAgeBand.Infant, world.Society.GetAgent(childId).AgeBand);
    }

    private sealed class QuietProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle")] } }, cancellationToken);
    }

    private sealed class ChildReplyHandler : HttpMessageHandler
    {
        public ConcurrentQueue<string> Bodies { get; } = new();
        public string Reply { get; set; } = "valid";
        public TaskCompletionSource<bool>? Release { get; init; }
        public TaskCompletionSource<bool>? StartGate { get; init; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (StartGate is not null) await StartGate.Task.WaitAsync(cancellationToken);
            using var envelope = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = envelope.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
            Bodies.Enqueue(body);
            Started.TrySetResult(true);
            if (Release is not null) await Release.Task;
            if (Reply == "refused") return new(HttpStatusCode.Forbidden);
            using var question = JsonDocument.Parse(body);
            var surname = question.RootElement.GetProperty("needs_name").GetBoolean()
                ? question.RootElement.GetProperty("self").GetProperty("allowed_child_surnames").EnumerateArray().First().GetString() : null;
            var content = JsonSerializer.Serialize(new
            {
                selected_candidate_id = "safe_idle",
                confidence = 1d,
                chosen_name = surname is null ? null : "Zuri " + surname,
                chosen_personality = Reply == "missing" ? null : Reply == "invalid" ? "bad\ntext" : "Inventive and independent",
                chosen_aspiration = Reply is "missing" or "partial" ? null : "Study the hills",
            });
            Returned.TrySetResult(true);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } }), Encoding.UTF8, "application/json")
            };
        }
    }

    private static async Task<byte[]> CreateBornAsync()
    {
        var prepared = await CookedBirthFixture.PrepareAsync(inPot: true);
        using var world = CookedBirthFixture.Restore(prepared);
        for (var tick = 0; tick < 650 && world.Society.Births.Count == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var birth = Assert.Single(world.Society.Births);
        Assert.Equal(prepared.Household, birth.HouseholdId);
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => prepared.OutputIds.Contains(lot.Id)).Sum(lot => lot.Quantity));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "child_born" && item.Detail == birth.ChildId);
        return WorldRuntimeCodec.Encode(world.ExportState());
    }
}
