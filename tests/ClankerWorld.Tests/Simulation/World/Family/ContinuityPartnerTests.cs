using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Fact]
    public async Task ContinuityPartneringFourFoundersChooseMutuallyReloadBeforeDeadlineAndReachTwoCouplesThenChildPlans()
    {
        var seen = new ConcurrentDictionary<string, AgentObservation>(StringComparer.Ordinal);
        using var initial = NormalPathWorld.CreateGenerated("continuity-partner-mutual", _ => new ParentProvider("safe_idle"));
        var ids = initial.Agents.Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray();
        // Cross-household mutual preferences outrank the default ordinal tie-break.
        using var world = WorldRuntime.Restore(initial.ExportState(), actor => new ContinuityPartnerProvider(observation =>
        {
            seen[actor] = observation;
            var index = Array.IndexOf(ids, actor);
            return new PartnershipChoice(PartnershipChoiceKind.Choose, ids[3 - index]).Encode();
        }));
        for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var singles = world.ExportState().Continuity!.Singles;
        Assert.Equal(4, singles.Count);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "choice_refused");
        Assert.All(singles, single => Assert.Equal(ids[3 - Array.IndexOf(ids, single.AgentId)], single.ChosenPartnerId));
        Assert.All(seen.Values, observation =>
        {
            Assert.Contains("may not refuse", observation.Self!.ContinuityNote, StringComparison.Ordinal);
            Assert.Contains("two world days", observation.Self.ContinuityNote, StringComparison.Ordinal);
            Assert.InRange(observation.Candidates.Count(candidate => candidate.ActionPlan is PartnershipActionPlan
            { Choice.Kind: PartnershipChoiceKind.Choose }), 2, 3);
            Assert.Contains(observation.Candidates, candidate => candidate.ActionPlan is PartnershipActionPlan
            { Choice.Kind: PartnershipChoiceKind.Postpone });
        });
        var deadline = singles[0].DeadlineTick;
        Assert.Equal(1 + 2L * world.ExportState().WorldSystems!.Config.TicksPerDay, deadline);
        PositionFamilyFixtureAt(world, deadline - 1);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ParentProvider("safe_idle"));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ParentProvider("safe_idle"));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.False((await restored.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(restored.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        AssertPair(restored, ids[0], ids[3]);
        AssertPair(restored, ids[1], ids[2]);
        Assert.Empty(restored.ExportState().Continuity!.Singles);
        Assert.Equal(2, restored.ExportState().Continuity!.Couples.Count);
        Assert.Equal(2, restored.ExportState().Events.Count(item => item.Kind == "continuity_partnership_formed"));
        Assert.Contains("The continuity rule paired", DescribeForPlayer(restored, "continuity_partnership_formed"), StringComparison.Ordinal);
        Assert.Equal(restored.Society.GetAgent(ids[0]).DomesticFamilyUnitId, restored.Society.GetAgent(ids[3]).DomesticFamilyUnitId);
        Assert.Equal(restored.Society.GetAgent(ids[1]).DomesticFamilyUnitId, restored.Society.GetAgent(ids[2]).DomesticFamilyUnitId);
        restored.Validate();
        PositionFamilyFixtureAt(restored, restored.ExportState().Continuity!.Couples[0].DeadlineTick - 1);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(2, restored.Agents.Count(person => person.Parenthood?.Stage == "preparing"));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(restored.ExportState())));
        reloaded.Validate();
    }

    [Fact]
    public async Task ContinuityPartneringNoMutualChoiceUsesStrongestRelationshipThenStableTieBreak()
    {
        using var initial = NormalPathWorld.CreateGenerated("continuity-partner-strength", _ => new ParentProvider("safe_idle"));
        var state = initial.ExportState();
        var ids = state.Agents.Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray();
        state = state with
        {
            Agents = state.Agents.Select(person => person with
            {
                SocialStanding = person.AgentId == ids[0] ? [new(ids[2], 10, initial.WorldTick)]
                : person.AgentId == ids[2] ? [new(ids[0], 10, initial.WorldTick)] : null,
            }).ToArray()
        };
        using var world = WorldRuntime.Restore(state, actor => new ContinuityPartnerProvider(_ =>
            new PartnershipChoice(PartnershipChoiceKind.Choose, ids[(Array.IndexOf(ids, actor) + 1) % 4]).Encode()));
        for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        PositionFamilyFixtureAt(world, world.ExportState().Continuity!.Singles[0].DeadlineTick - 1);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        AssertPair(world, ids[0], ids[2]);
        AssertPair(world, ids[1], ids[3]);
    }

    [Fact]
    public async Task ContinuityPartneringNotYetKeepsDeadlineAndStillPairsWithoutAnyTrust()
    {
        using var world = NormalPathWorld.CreateGenerated("continuity-partner-not-yet", actor =>
            new ContinuityPartnerProvider(_ => new PartnershipChoice(PartnershipChoiceKind.Postpone, actor).Encode()));
        for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var singles = world.ExportState().Continuity!.Singles;
        Assert.All(singles, single => Assert.Null(single.ChosenPartnerId));
        Assert.All(singles, single => Assert.True(single.Postponed));
        PositionFamilyFixtureAt(world, singles[0].DeadlineTick - 1);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        AssertPair(world, singles[0].AgentId, singles[1].AgentId);
        AssertPair(world, singles[2].AgentId, singles[3].AgentId);
    }

    [Fact]
    public async Task ContinuityPartneringSomeoneWhoChoseAnAlreadyTakenAdultPairsWithAnAvailableAdult()
    {
        var plans = new ConcurrentDictionary<string, PartnershipActionPlan>(StringComparer.Ordinal);
        using var initial = NormalPathWorld.CreateGenerated("continuity-partner-taken", actor =>
            new ContinuityPartnerProvider(observation =>
            {
                var candidate = observation.Candidates.FirstOrDefault(candidate =>
                    candidate.ActionPlan is PartnershipActionPlan { Choice.Kind: PartnershipChoiceKind.Choose });
                if (candidate?.ActionPlan is PartnershipActionPlan plan) plans[actor] = plan;
                return candidate?.Id ?? "safe_idle";
            }));
        for (var tick = 0; tick < 4; tick++) Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var singles = state.Continuity!.Singles;
        var first = singles[0].AgentId;
        var chosen = singles[0].ChosenPartnerId!;
        var third = singles.First(single => single.AgentId != first && single.AgentId != chosen).AgentId;
        var fourth = singles.Single(single => single.AgentId != first && single.AgentId != chosen && single.AgentId != third).AgentId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
            new("taken-partner", 1, SocietyRelationshipType.Partnership, chosen, third, checkpoint.WorldTick)));
        society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, "taken-partner", 1, third));
        using var world = WorldRuntime.Restore(state with { Society = society.ExportState() }, _ => new ParentProvider("safe_idle"));
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plans[first]).Refusal!.Reason);
        PositionFamilyFixtureAt(world, singles[0].DeadlineTick - 1);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        AssertPair(world, chosen, third);
        AssertPair(world, first, fourth);
        world.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContinuityPartneringCloseRelativesAndEldersAreNeverForcedIntoAPartnership(bool elder)
    {
        using var initial = NormalPathWorld.CreateGenerated("continuity-partner-ineligible", _ => new ParentProvider("safe_idle"));
        var state = initial.ExportState();
        var ids = state.Agents.Select(person => person.AgentId).ToArray();
        state = AsElder(AsElder(state, ids[2]), ids[3]);
        if (elder) state = AsElder(state, ids[1]);
        else
        {
            (state, var parent) = FamilyTreeFixture.WithDeadAncestor(state, "continuity-single-ancestor");
            state = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.BiologicalParentage, (parent, ids[0]), (parent, ids[1]));
        }
        using var world = WorldRuntime.Restore(state, _ => new ParentProvider("safe_idle"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        PositionFamilyFixtureAt(world, world.ExportState().Continuity!.Singles[0].DeadlineTick - 1);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership);
    }

    [Fact]
    public async Task ContinuityPartneringEndingAForcedPartnershipStartsAFreshDeadline()
    {
        using var initial = NormalPathWorld.CreateGenerated("continuity-partner-again", _ => new ParentProvider("safe_idle"));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        PositionFamilyFixtureAt(initial, initial.ExportState().Continuity!.Singles[0].DeadlineTick - 1);
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var partnership = state.Society.Society.Relationships.First(edge => edge.Type == SocietyRelationshipType.Partnership);
        // The newly supplied provider must receive a fresh decision instead of retaining safe idle.
        state = state with { Agents = state.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        using var world = WorldRuntime.Restore(state, actor => new ParentProvider(actor == partnership.ProposerId ? "partner_leave:" : "safe_idle"));
        for (var tick = 0; tick < 40 && world.Society.GetRelationship(partnership.Id).State != SocietyRelationshipState.Revoked; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(SocietyRelationshipState.Revoked, world.Society.GetRelationship(partnership.Id).State);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var singles = world.ExportState().Continuity!.Singles;
        Assert.Equal(2, singles.Count);
        Assert.All(singles, single => Assert.Equal(world.WorldTick + 2L * state.WorldSystems!.Config.TicksPerDay, single.DeadlineTick));
    }

    [Fact]
    public async Task ContinuityPartneringRuleTurningOffDiscardsSingleDeadlinesAndChoices()
    {
        using var initial = NormalPathWorld.CreateGenerated("continuity-partner-off", _ => new ParentProvider("safe_idle"));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = WithFourEligibleCouples(initial.ExportState());
        using var world = WorldRuntime.Restore(state, _ => new ParentProvider("safe_idle"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.False(world.ExportState().Continuity!.Active);
        Assert.Empty(world.ExportState().Continuity!.Singles);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "continuity_partnership_formed");
    }

    [Fact]
    public async Task ContinuityPartneringSingleDeadlineAndChoiceValidationRejectsDamageAndMissingState()
    {
        using var world = NormalPathWorld.CreateGenerated("continuity-partner-validation", _ => new ParentProvider("safe_idle"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var state = world.ExportState();
        var saved = state.Continuity!;
        var single = saved.Singles[0];
        var damaged = new[]
        {
            saved with { Active = false },
            saved with { Singles = [single, single] },
            saved with { Singles = [single with { AgentId = "missing" }] },
            saved with { Singles = [single with { ChosenPartnerId = "missing" }] },
            saved with { Singles = [single with { ChosenPartnerId = single.AgentId }] },
            saved with { Singles = [single with { DeadlineTick = single.DeadlineTick + 1 }] },
        };
        foreach (var continuity in damaged)
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with { Continuity = continuity }));
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        document["state"]!["continuity"]!.AsObject().Remove("singles");
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(Encoding.UTF8.GetBytes(document.ToJsonString())));
    }

    private static void AssertPair(WorldRuntime world, string first, string second) => Assert.Single(world.Society.Relationships,
        edge => edge.Type == SocietyRelationshipType.Partnership && edge.State == SocietyRelationshipState.Accepted &&
            (edge.ProposerId == first && edge.TargetId == second || edge.ProposerId == second && edge.TargetId == first));

    private sealed class ContinuityPartnerProvider(Func<AgentObservation, string> select) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id == select(request.Observation))
                ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [candidate] } }, cancellationToken);
        }
    }
}
