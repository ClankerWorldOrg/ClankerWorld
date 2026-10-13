using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class DescendantConversationListenerTests
{
    private const string Speaker = "founder-scout";
    private const string Partner = "founder-mira";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeBornDescendantCanHearTwoOtherAdultsWithoutRejectingTheirTurn(bool nearby)
    {
        using var seed = new WorldRuntime(new string('s', 100));
        var state = seed.ExportState();
        var society = ChosenBirthNameTestFixture.NameParent(state.Society.Society, Speaker);
        society = SocietyRules.ProposeRelationship(society, new("listener-partnership", 1,
            SocietyRelationshipType.Partnership, Speaker, Partner, society.WorldTick)).Checkpoint;
        society = SocietyRules.AcceptRelationship(society, "listener-partnership", 1, Partner).Checkpoint;
        var household = society.GetAgent(Speaker).HouseholdId!;
        var caregivers = new[] { Speaker, Partner }.Where(id => society.GetAgent(id).HouseholdId == household)
            .Append(Speaker).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var birth = SocietyRules.CommitBirth(society, new($"family:{Speaker}:{society.WorldTick}", 1,
            Speaker, Partner, household, caregivers, [Speaker, Partner], "food:first", 2, society.WorldTick,
            ChildName: ChosenBirthNameTestFixture.ChildName(society, Speaker, "Listener"), PrimaryCaregiverId: Speaker));
        var child = Assert.IsType<string>(birth.CreatedId);
        Assert.InRange(child.Length, 1, DerivedId.MaximumLength);
        society = birth.Checkpoint;
        // Accelerate age only; retain the actual birth identity, ancestry and family records.
        var adultBirth = society.LifeTickAt(society.WorldTick) - 20 * society.Config.TicksPerLifecycleAge;
        society = society with
        {
            Agents = society.Agents.Select(person => person.Id == child ? person with
            {
                BirthTick = adultBirth,
                BirthLifeTick = society.LifeClock is null ? null : adultBirth,
                AgeBand = SocietyAgeBand.Adult,
                LastLifecycleYearChecked = 20,
            } : person).ToArray(),
        };
        var blocked = state.Map.MapObjects.Select(item => item.Position)
            .Concat(state.Map.Resources.Select(item => item.Position)).ToHashSet();
        var land = state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => state.Map.IsBuildable(point) && !blocked.Contains(point)).ToArray();
        var placement = (from a in land
                         from b in land
                         where b != a && state.Map.FootDistance(a, b) == 1
                         let listeners = land.Where(point => point != a && point != b && state.Map.FootDistance(a, point) <= 3).ToArray()
                         let distant = land.Where(point => state.Map.FootDistance(a, point) > 3 && state.Map.FootDistance(b, point) > 3).ToArray()
                         where listeners.Length > 0 && distant.Length >= state.Agents.Count
                         select (First: a, Second: b, Close: listeners[0], Far: distant)).First();
        var (first, second, close, far) = placement;
        var distantIndex = 1;
        state = state with
        {
            Society = state.Society with { Society = society },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == Speaker ? first : person.AgentId == Partner ? second : far[distantIndex++],
                FullnessBasisPoints = 9_500,
            }).Append(new(child, nearby ? close : far[0], 9_500, 0, "patient", "listen")).ToArray(),
        };
        // Import the newly born physical actor through the native restore boundary
        // before measuring byte stability of the complete runtime checkpoint.
        using var prepared = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => new ListenerProvider());
        prepared.Validate();
        var initial = WorldRuntimeCodec.Encode(prepared.ExportState());
        var leftProvider = new ListenerProvider();
        var rightProvider = new ListenerProvider();
        using var left = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), _ => leftProvider);
        using var right = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), _ => rightProvider);
        left.Validate();
        Assert.Equal(initial, WorldRuntimeCodec.Encode(left.ExportState()));
        Assert.False((await left.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(left.ExportState()));
        for (var tick = 0; tick < 20 && !left.Conversations.Any(item => item.Turns.Count > 0); tick++)
        {
            await AwaitNativeProviderWorkAsync(left);
            await AwaitNativeProviderWorkAsync(right);
            Assert.True((await left.AdvanceOneTickAsync()).Advanced);
            Assert.True((await right.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(left.ExportState()), WorldRuntimeCodec.Encode(right.ExportState()));
        }
        var conversation = Assert.Single(left.Conversations);
        Assert.True(conversation.Turns.Count > 0,
            $"The native descendant listener must not reject speech: nearby={nearby}, child ID length={child.Length}, status={conversation.Status}, interruption={conversation.Interruption}.");
        var turn = Assert.Single(conversation.Turns);
        Assert.Equal("A short valid public turn.", turn.Text);
        Assert.Equal(nearby, turn.ListenerIds.Contains(child, StringComparer.Ordinal));
        var beliefs = left.ExportState().Society.Society.Beliefs ?? [];
        if (nearby)
        {
            var belief = Assert.Single(beliefs, item => item.OwnerId == child && item.SourceTurnId == turn.Id);
            Assert.Equal(turn.SpeakerId, belief.SourceAgentId);
            Assert.Equal(turn.Text, belief.Statement);
            Assert.Equal(SocietyBeliefProvenance.Hearsay, belief.Provenance);
            Assert.Throws<InvalidOperationException>(() => SocietyRules.RecordAgentBelief(left.Society,
                belief with { Id = Identity.Parse("belief:unknown-native-owner"), OwnerId = "missing-listener" }));
            // Salience indexes refer to the same genuine heard belief and native
            // owner. Scoring it must not make an otherwise valid world unloadable.
            var compacted = SocietyRules.RecordAgentMemoryCompaction(left.Society, child,
                [new SocietyAgentMemoryImportance(belief.Id, SocietyMemorySourceKind.Belief,
                    belief.FormedTick, 5_000, 8_000, left.WorldTick)]);
            SocietyRules.Validate(compacted);
            var compactedState = left.ExportState() with
            {
                Society = left.ExportState().Society with { Society = compacted },
            };
            using var compactedWorld = WorldRuntime.Restore(
                WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(compactedState)),
                _ => new ListenerProvider());
            compactedWorld.Validate();
            var index = Assert.Single(compactedWorld.Society.MemoryCompactions!);
            Assert.Equal(child, index.OwnerId);
            Assert.Equal(belief.Id, Assert.Single(index.Sources).SourceId);
            Assert.Equal(beliefs, compactedWorld.Society.Beliefs);
            Assert.Throws<InvalidDataException>(() => SocietyRules.Validate(compacted with
            {
                MemoryCompactions = [index with { OwnerId = "missing-listener" }],
            }));
        }
        else Assert.DoesNotContain(beliefs, item => item.OwnerId == child && item.SourceTurnId == turn.Id);
        left.Validate();
        var saved = WorldRuntimeCodec.Encode(left.ExportState());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(saved)));
        var reloadedProvider = new ListenerProvider();
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => reloadedProvider);
        restored.Validate();
        var restoredTurn = Assert.Single(Assert.Single(restored.Conversations).Turns);
        Assert.Equal(turn.Id, restoredTurn.Id);
        Assert.Equal(turn.Text, restoredTurn.Text);
        Assert.Equal(turn.SpeakerId, restoredTurn.SpeakerId);
        Assert.Equal(turn.ListenerIds, restoredTurn.ListenerIds);
        Assert.Equal(beliefs, restored.ExportState().Society.Society.Beliefs);
        Assert.Contains(restored.Society.Agents, item => item.Id == child);
        using var pairedReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ListenerProvider());
        restored.Resume();
        pairedReload.Resume();
        if (nearby)
        {
            // A normal owner suggestion prompts the idle listener's next decision
            // without waiting for the idle plan's separate 300-tick interval.
            var recall = new OwnerInstructionRequest("recall-heard-turn", "owner:test", child,
                OwnerInstructionKind.Suggestion, "Consider what you heard in the nearby conversation.");
            Assert.Equal(restored.SubmitInstruction(recall), pairedReload.SubmitInstruction(recall));
        }
        for (var tick = 0; tick < 40; tick++)
        {
            await AwaitNativeProviderWorkAsync(restored);
            await AwaitNativeProviderWorkAsync(pairedReload);
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.True((await pairedReload.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(restored.ExportState()), WorldRuntimeCodec.Encode(pairedReload.ExportState()));
            if (tick >= 3 && (!nearby || reloadedProvider.Observations.Any(item => item.AgentId == child &&
                (item.RetrievedMemories ?? []).Any(memory => memory.Kind == "belief" && memory.Summary == turn.Text))))
                break;
        }
        Assert.Equal(beliefs, restored.Society.Beliefs);
        restored.Validate();
        if (nearby)
        {
            var recalled = reloadedProvider.Observations.Where(item => item.AgentId == child)
                .SelectMany(item => item.RetrievedMemories ?? []).Where(item => item.Kind == "belief" && item.Summary == turn.Text).ToArray();
            Assert.NotEmpty(recalled);
            Assert.All(recalled, memory =>
            {
                Assert.Equal(child, memory.OwnerId);
                Assert.Equal(turn.SpeakerId, memory.SourceAgentId);
                Assert.InRange(memory.SubjectId.Length, 1, 128);
                Assert.Equal(child, memory.SubjectId);
            });
            Assert.Single(recalled.Select(memory => memory.SubjectId).Distinct(StringComparer.Ordinal));
        }
    }

    private static async Task AwaitNativeProviderWorkAsync(WorldRuntime world)
    {
        // Native birth also starts parenthood identity work. As in the existing
        // conversation fixture, finish pending local tasks before advancing ticks
        // so scheduler timing cannot move the paired world's admission boundary.
        var tasks = new List<Task>();
        foreach (var fieldName in new[] { "pendingModelCalls", "pendingConversationTurns", "pendingIdentityMoments" })
        {
            var field = typeof(WorldRuntime).GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            var pending = Assert.IsAssignableFrom<System.Collections.IDictionary>(field.GetValue(world));
            tasks.AddRange(pending.Values.Cast<object>().Select(item =>
                Assert.IsAssignableFrom<Task>(item.GetType().GetProperty("Task")!.GetValue(item))));
        }
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class ListenerProvider : IDecisionProvider, IAgentConversationProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public bool CanSpeakAs(string agentId) => agentId is Speaker or Partner;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            var observation = request.Observation;
            Observations.Enqueue(observation);
            var selected = observation.Candidates.FirstOrDefault(candidate =>
                observation.AgentId == Speaker && candidate.Id == $"talk:{Partner}") ??
                observation.Candidates.FirstOrDefault(candidate => observation.AgentId == Partner &&
                    candidate.Id.StartsWith("conversation_accept:", StringComparison.Ordinal)) ??
                observation.Candidates.First(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId,
                Kind, ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                selected.Id, 1d, observation.Candidates.ToDictionary(item => item.Id,
                    item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                ChosenName: observation.NeedsName ? observation.Self?.Name ?? "Test Agent" : null,
                ChosenPersonality: observation.NeedsPersonality ? "patient" : null,
                ChosenAspiration: observation.NeedsAspiration ? "listen" : null));
        }

        public ValueTask<AgentConversationTurnResponse> SpeakAsync(AgentConversationTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new AgentConversationTurnResponse(request.RequestId, request.ConversationId,
                request.Revision, request.RunEpoch, request.SpeakerId, "A short valid public turn.",
                AgentConversationDisposition.Continue, AgentConversationEffect.None));
        }
    }
}
