using System.Collections.Concurrent;
using System.Reflection;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static readonly Lazy<Task<byte[]>> FinalWordsFamily = new(() => CreateLaterAdultAsync(2));
    [Fact]
    public async Task NativeHeirRecallsFinalWordsFromAnAcceptedWillAfterSettlementAndReload()
    {
        var state = WorldRuntimeCodec.Decode(await FinalWordsFamily.Value);
        var checkpoint = state.Society.Society;
        var heir = checkpoint.Births.OrderBy(item => item.CommittedTick).Last().ChildId;
        var parent = Assert.Single(checkpoint.Relationships, item =>
            item.Type == SocietyRelationshipType.BiologicalParentage && item.TargetId == heir &&
            checkpoint.Births.Any(birth => birth.ChildId == item.ProposerId)).ProposerId;
        Assert.Equal(2, checkpoint.Births.Count);
        Assert.InRange(heir.Length, 1, DerivedId.MaximumLength);
        var physical = Assert.Single(state.Agents, item => item.AgentId == parent);
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "final-words-wood", "wood", parent, 4,
                groundPosition: new(physical.Position.X, physical.Position.Y)),
        };
        // Explicit fixture death, as in the existing will tests. Kinship and
        // identities come from births; the offered will and settlement are native.
        checkpoint = SocietyRules.Kill(checkpoint, parent, SocietyDeathCause.Accident).Checkpoint;
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.DropCarrierGoods(checkpoint.Inventory, parent,
                new(physical.Position.X, physical.Position.Y)),
        };
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Where(item => item.AgentId != parent).ToArray(),
            DeceasedAgents = (state.DeceasedAgents ?? []).Append(new DeceasedAgentState(
                parent, checkpoint.WorldTick, checkpoint.AgeAt(checkpoint.GetAgent(parent), checkpoint.WorldTick),
                physical)).ToArray(),
            Towns = state.Towns!.Select(town =>
            {
                var residents = town.ResidentIds.Where(id => id != parent).ToArray();
                var adults = residents.Where(id => checkpoint.GetAgent(id).Status == SocietyAgentStatus.Active &&
                    checkpoint.GetAgent(id).AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder);
                var advanced = TownGovernmentRules.Advance(town.Council!, town.Government!, town.Id,
                    town.Name, state.WorldSeed, adults, checkpoint.WorldTick, state.WorldSystems!.Config.TicksPerDay);
                return town with { ResidentIds = residents, Council = advanced.Council, Government = advanced.Government };
            }).ToArray(),
        };
        var provider = new FinalWordsRecallProvider(checkpoint.GetAgent(heir).Name);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => provider);
        world.Resume();
        var estateId = Assert.Single(world.Society.Estates, item => item.DeceasedId == parent).Id;
        await AdvanceFinalWordsTick(world);
        await AdvanceFinalWordsTick(world);
        var accepted = world.Society.GetEstate(estateId);
        Assert.Equal("accepted", accepted.WillStatus);
        Assert.Equal([heir], accepted.WillHeirIds);
        Assert.Equal(FinalWordsRecallProvider.Words, accepted.FinalWords);
        var offered = Assert.Single(provider.Observations, item => item.Will is not null);
        offered.Validate();
        Assert.Contains(offered.Will!.Heirs, item => item.Name == checkpoint.GetAgent(heir).Name);
        Assert.DoesNotContain(world.Society.Memories, item => item.Id.StartsWith("final-words:", StringComparison.Ordinal) &&
            item.SubjectId == parent);

        // Keep the full escrow wait. Replay the actual settlement boundary,
        // rather than duplicating every idle tick in the seven-day hold.
        while (world.WorldTick < accepted.ExpiryTick - 1)
            await AdvanceFinalWordsTick(world);
        var held = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(held),
            _ => new FinalWordsRecallProvider(checkpoint.GetAgent(heir).Name));
        Assert.Equal(held, WorldRuntimeCodec.Encode(replay.ExportState()));
        for (var tick = 0; tick < 2; tick++)
        {
            await AdvanceFinalWordsTick(world);
            await AdvanceFinalWordsTick(replay);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(world.Society.GetEstate(estateId).Settled);
        Assert.Single(world.Society.Events, item => item.Kind == "estate_settled" && item.Detail == estateId);
        var inherited = Assert.Single(world.Society.Inventory.Lots, item => item.ProvenanceLotId == "final-words-wood");
        Assert.Equal((heir, 4, new InventoryGroundPosition(physical.Position.X, physical.Position.Y)),
            (inherited.OwnerId, inherited.Quantity, inherited.GroundPosition));
        var memory = Assert.Single(world.Society.Memories, item => item.OwnerId == heir &&
            item.Id.StartsWith("final-words:", StringComparison.Ordinal) && item.SubjectId == parent);
        Assert.Equal((heir, parent, "private", SocietyMemoryKind.LifeEvent,
                $"{checkpoint.GetAgent(parent).Name}'s final words were: '{FinalWordsRecallProvider.Words}'"),
            (memory.OwnerId, memory.SubjectId, memory.Visibility, memory.Kind, memory.Summary));
        Assert.True(SocietyMemoryArchiveRules.IsPermanent(memory));

        var settled = WorldRuntimeCodec.Encode(world.ExportState());
        var recall = new FinalWordsRecallProvider(checkpoint.GetAgent(heir).Name, heir);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(settled), _ => recall);
        Assert.Equal(settled, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.SubmitInstruction(new("recall-final-words", "owner:test", heir, OwnerInstructionKind.Suggestion,
            "Remember the final words about keeping the trees growing."));
        for (var tick = 0; tick < 20 && !recall.Observations.Any(item => item.AgentId == heir); tick++)
            await AdvanceFinalWordsTick(restored);
        var observation = Assert.Single(recall.Observations.Where(item => item.AgentId == heir).Take(1));
        observation.Validate();
        Assert.True(observation.WorldTick > memory.SourceTick);
        var excerpt = Assert.Single(observation.RetrievedMemories!, item => item.Id == memory.Id);
        Assert.InRange(memory.Id.Length, 1, 128);
        Assert.Equal((heir, memory.Summary, memory.SourceTick, "private"),
            (excerpt.OwnerId, excerpt.Summary, excerpt.SourceTick, excerpt.Visibility));
        Assert.Equal(parent, excerpt.SubjectId);
        Assert.Equal(memory, Assert.Single(restored.Society.Memories, item => item.Id == memory.Id));
        Assert.DoesNotContain(recall.Observations, item => item.Will is not null);
        Assert.Single(restored.Society.Events, item => item.Kind == "estate_settled" && item.Detail == estateId);
        var final = WorldRuntimeCodec.Encode(restored.ExportState());
        using var validated = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), _ => recall);
        Assert.Equal(final, WorldRuntimeCodec.Encode(validated.ExportState()));
        validated.Validate();
    }

    private static async Task AdvanceFinalWordsTick(WorldRuntime world)
    {
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        foreach (var name in new[] { "pendingHosted", "pendingWills" })
        {
            var field = typeof(WorldRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            var pending = Assert.IsAssignableFrom<System.Collections.IDictionary>(field!.GetValue(world));
            var tasks = pending.Values.Cast<object>().Select(item =>
                Assert.IsAssignableFrom<Task>(item.GetType().GetProperty("Task")!.GetValue(item))).ToArray();
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private sealed class FinalWordsRecallProvider(string heirName, string? recallOwner = null) : IDecisionProvider
    {
        public const string Words = "Keep the trees growing.";
        public ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public DecisionProviderKind KindFor(AgentObservation observation) =>
            observation.Will is not null || observation.AgentId == recallOwner
                ? DecisionProviderKind.LargeLanguageModel : DecisionProviderKind.Deterministic;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();
            var observation = request.Observation;
            Observations.Enqueue(observation);
            var selected = observation.Will is null ? "safe_idle" : CognitionWillContext.HeirsCandidateId;
            var will = observation.Will is null ? null : new CognitionWillChoice(
                [observation.Will.Heirs.Single(item => item.Name == heirName).Key], CognitionWillContext.EqualSplit,
                FinalWords: Words);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, KindFor(observation),
                ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                selected, 1, observation.Candidates.ToDictionary(item => item.Id,
                    item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal), Will: will));
        }
    }
}
