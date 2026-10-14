using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Fact]
    public async Task AToolRequestVisitRetainsItsKnownSmithAndRecipeAcrossMovementRollbackReplayAndReload()
    {
        var (state, buyer, _, shop) = Prepared();
        var origin = state.Map.Tiles.Select(tile => tile.Position).First(position => state.Map.IsPassable(position) &&
            state.Map.FootDistance(position, shop.Position) == 4 && !state.Agents.Any(person => person.Position == position) &&
            DeterministicRouteFinder.TryFind(state.Map, position, shop.Position, out _));
        var facts = state.Knowledge!.Facts.Where(fact => fact.OwnerId != buyer || fact.Position != shop.Position).ToArray();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == buyer ? person with
            { Position = origin, TravelCooldownTicks = 0, LastDecisionContext = null } : person).ToArray(),
            Knowledge = state.Knowledge with
            {
                Facts = facts.Append(new AgentKnowledgeFact("tool-visit-known-smith", buyer, buyer,
                shop.Position, state.Map.TerrainKindAt(shop.Position)!.Value.ToString(), [], state.Society.Society.WorldTick, "firsthand")).ToArray()
            },
        };
        var chooser = new ToolVisitChooser(buyer);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ToolVisitChooser(buyer));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<ToolRequestVisitActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == buyer).ActionPlan);
        Assert.Equal(shop.InstanceId, plan.Smith.InstanceId);
        Assert.Equal(plan.Choice, ToolRequestVisitChoice.Decode(plan.CandidateId));
        Assert.NotEmpty(plan.Route);
        Assert.NotEqual(origin, world.Agents.Single(person => person.AgentId == buyer).Position);
        Assert.Empty(world.ToolMakingRequests);
        _ = Roundtrip(world);
        Assert.True(world.DisplaceAdult(buyer));
        using var society = SocietyRuntime.Restore(world.ExportState().Society);
        society.Apply(checkpoint => SocietyRules.JoinHouseholdCareGroup(checkpoint, buyer, shop.HouseholdId!));
        using var changed = WorldRuntime.Restore(world.ExportState() with { Society = society.ExportState() });
        var unchanged = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(unchanged, WorldRuntimeCodec.Encode(changed.ExportState()));
    }

    private sealed class ToolVisitChooser(string actor) : IDecisionProvider
    {
        public ToolRequestVisitActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is ToolRequestVisitActionPlan && item.Description.Contains("wooden axe", StringComparison.Ordinal)) : null;
            if (candidate is not null) Plan = Assert.IsType<ToolRequestVisitActionPlan>(candidate.ActionPlan);
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
