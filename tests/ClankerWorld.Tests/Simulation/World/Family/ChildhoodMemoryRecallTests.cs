using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ChildhoodMemoryRecallTests
{
    [Theory]
    [InlineData("play")]
    [InlineData("converse")]
    [InlineData("learn")]
    public async Task GeneratedChildRecallsAnActuallyOfferedSocialExperienceAfterReload(string kind)
    {
        using var generated = NormalPathWorld.CreateGenerated("social-memory-d9efa858",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var checkpoint = state.Society.Society;
        var householdId = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a").HouseholdId;
        var household = checkpoint.Households.Single(item => item.Id == householdId);
        var parent = household.MemberIds[0];
        var partner = household.MemberIds[1];
        checkpoint = SocietyRules.ProposeRelationship(checkpoint, new("recall-partnership", 1,
            SocietyRelationshipType.Partnership, parent, partner, checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "recall-partnership", 1, partner).Checkpoint;
        checkpoint = ChosenBirthNameTestFixture.NameParent(checkpoint, parent);
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "recall-birth-food", "food", household.Id, 4),
        };
        var birth = SocietyRules.CommitBirth(checkpoint, new($"family:{parent}:{checkpoint.WorldTick}:" + new string('x', 96), 1,
            parent, partner, household.Id, household.MemberIds, [parent, partner], "recall-birth-food", 4,
            checkpoint.WorldTick, ChildName: ChosenBirthNameTestFixture.ChildName(checkpoint, parent, "Recall"),
            PrimaryCaregiverId: parent));
        var child = Assert.IsType<string>(birth.CreatedId);
        Assert.InRange(child.Length, 1, DerivedId.MaximumLength);
        checkpoint = birth.Checkpoint;
        var age = Assert.IsType<SocietyDayLifecycle>(checkpoint.Config.DayLifecycle).ChildStartDay;
        var birthLifeTick = checkpoint.LifeTickAt(checkpoint.WorldTick) - age * checkpoint.Config.TicksPerLifecycleAge;
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.Id == child ? person with
            {
                BirthTick = birthLifeTick,
                BirthLifeTick = checkpoint.LifeClock is null ? null : birthLifeTick,
                AgeBand = SocietyAgeBand.Child,
                LastLifecycleYearChecked = age,
            } : person).ToArray(),
        };
        var parentPosition = state.Agents.Single(person => person.AgentId == parent).Position;
        var position = state.Map.FootNeighbors(parentPosition).First(point => state.Map.IsPassable(point) &&
            state.Agents.All(person => person.Position != point));
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Append(new(child, position, 10_000, 0, "curious", "grow with the household")).ToArray(),
            Towns = state.Towns!.Select(town => town.ResidentIds.Contains(parent)
                ? town with { ResidentIds = town.ResidentIds.Append(child).Order(StringComparer.Ordinal).ToArray() }
                : town).ToArray(),
        };
        var choice = $"child_{kind}:{parent}";
        var chooser = new SocialProvider(choice);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == child ? chooser : new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 20 && !world.Society.Memories.Any(memory => memory.OwnerId == child); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(chooser.Observations, observation => observation.Candidates.Any(candidate => candidate.Id == choice));
        var memory = Assert.Single(world.Society.Memories, memory => memory.OwnerId == child);
        Assert.Equal(parent, memory.SubjectId);
        Assert.StartsWith($"child-social:{kind}:", memory.Id.Value);
        Assert.InRange(memory.Id.Value.Length, 1, 128);
        using var replay = WorldRuntime.Restore(state,
            id => id == child ? new SocialProvider(choice) : new ActionCoverageRecorder(chooseIdle: true));
        while (replay.WorldTick < world.WorldTick)
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(memory, Assert.Single(replay.Society.Memories, item => item.OwnerId == child));
        var chosen = ChosenBirthNameTestFixture.ChildName(world.Society, parent, "Remi");
        Assert.True(world.RenameAgent(child, chosen));
        var rename = Assert.Single(world.Society.Memories, item => item.OwnerId == child && item.Permanent);
        Assert.Equal(child, rename.SubjectId);
        Assert.InRange(rename.Id.Value.Length, 1, 128);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        var recall = new SocialProvider("safe_idle");
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved),
            id => id == child ? recall : new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.SubmitInstruction(new("recall-experience", "owner", child, OwnerInstructionKind.Suggestion,
            memory.Summary));
        for (var tick = 0; tick < 20 && recall.Observations.Count == 0; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        var observed = Assert.Single(recall.Observations);
        observed.Validate();
        var recalled = Assert.Single(observed.RetrievedMemories!, item => item.Id == memory.Id);
        Assert.Equal((child, parent, memory.Summary, memory.SourceTick),
            (recalled.OwnerId, recalled.SubjectId, recalled.Summary, recalled.SourceTick));
        var recalledRename = Assert.Single(observed.RetrievedMemories!, item => item.Id == rename.Id);
        Assert.Equal(child, recalledRename.OwnerId);
        Assert.Equal(rename.Summary, recalledRename.Summary);
        Assert.InRange(recalledRename.SubjectId.Length, 1, 128);
        Assert.Equal(child, recalledRename.SubjectId);
        Assert.Equal(rename, Assert.Single(restored.Society.Memories, item => item.Id == rename.Id));
        Assert.DoesNotContain(observed.Candidates, candidate => candidate.Id == choice);
        Assert.Equal(memory, Assert.Single(restored.Society.Memories, item => item.Id == memory.Id));
        var again = WorldRuntimeCodec.Encode(restored.ExportState());
        using var roundtrip = WorldRuntime.Restore(WorldRuntimeCodec.Decode(again));
        Assert.Equal(again, WorldRuntimeCodec.Encode(roundtrip.ExportState()));
    }

    [Fact]
    public async Task MalformedMemoryKeysRemainExcludedFromPersonalRecall()
    {
        using var seed = new WorldRuntime("child-memory-validation");
        var state = seed.ExportState();
        var valid = new SocietySocialMemory(Identity.Parse("valid-memory"), "founder-scout", "founder-mira", "A shared game.", "public", 0);
        Assert.Throws<ArgumentException>(() => Identity.Parse(new string('x', 129)));
        Assert.Throws<ArgumentException>(() => Identity.Parse("control\0memory"));
        var memories = new[]
        {
            valid,
            valid with { Id = Identity.ParseStored(new string('x', 129)) },
            valid with { Id = Identity.ParseStored("control\0memory") },
            valid with { Id = Identity.Parse(" padded-memory") },
        };
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with
            {
                Society = state.Society.Society with { Memories = memories.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray() },
            },
        };
        var capture = new SocialProvider("safe_idle");
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == "founder-scout" ? capture : new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var observation = Assert.Single(capture.Observations);
        observation.Validate();
        Assert.Equal(valid.Id.Value, Assert.Single(observation.RetrievedMemories!).Id);
        Assert.Equal(memories.OrderBy(item => item.Id.Value, StringComparer.Ordinal), world.Society.Memories);
    }

    private sealed class SocialProvider(string choice) : IDecisionProvider
    {
        public List<AgentObservation> Observations { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            Observations.Add(request.Observation);
            var selected = request.Observation.Candidates.Any(candidate => candidate.Id == choice) ? choice : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
