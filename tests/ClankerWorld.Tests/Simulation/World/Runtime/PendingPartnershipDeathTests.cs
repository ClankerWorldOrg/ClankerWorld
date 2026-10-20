using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class PendingPartnershipDeathTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task NativeDeathDistinguishesAnUnansweredProposalFromAnAcceptedPartner(bool proposerDies, bool accepted)
    {
        using var seed = NormalPathWorld.CreateGenerated("agents-audit-partner-loss",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = seed.ExportState();
        var people = state.Society.Society.Agents.Select(person => person.Id).ToArray();
        var deceased = proposerDies ? people[0] : people[1];
        var survivor = proposerDies ? people[1] : people[0];
        var provider = new PartnerLossProvider();
        state = PrepareDeath(state, "first-partnership", people[0], people[1], deceased, accepted);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickNonBlockingAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Empty(provider.IdentityCalls);
        await AdvanceAndAdmit(world);

        Assert.Equal(SocietyDeathCause.NaturalAge, world.Society.GetAgent(deceased).DeathCause);
        var relationship = world.Society.GetRelationship("first-partnership");
        Assert.Equal(accepted ? SocietyRelationshipState.EndedByDeath : SocietyRelationshipState.Revoked, relationship.State);
        Assert.Equal(SocietyConsentState.Revoked, relationship.Consent);
        Assert.Equal(accepted ? 2 : 0, relationship.AcceptedParties.Count);
        AssertPartnerLoss(world, provider, survivor, accepted ? 1 : 0);
        var will = Assert.Single(provider.WillObservations, observation => observation.AgentId == deceased).Will!;
        var survivorName = world.Society.GetAgent(survivor).Name;
        var relation = will.Heirs.Single(heir => heir.Name == survivorName).Relation;
        if (accepted) Assert.Equal("your partner", relation);
        else Assert.NotEqual("your partner", relation);

        var checkpoint = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(checkpoint), _ => provider);
        Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.True((await loaded.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
        AssertPartnerLoss(loaded, provider, survivor, accepted ? 1 : 0);

        if (accepted) return;
        // The unanswered proposal must leave the lifetime opportunity available
        // when a later, actually accepted partner dies, including after reload.
        var laterPartner = people[2];
        var laterState = PrepareDeath(loaded.ExportState(), "later-partnership", survivor, laterPartner, laterPartner, accepted: true);
        using var later = WorldRuntime.Restore(laterState, _ => provider);
        await AdvanceAndAdmit(later);
        Assert.Equal(SocietyDeathCause.NaturalAge, later.Society.GetAgent(laterPartner).DeathCause);
        AssertPartnerLoss(later, provider, survivor, 1);
        var laterWill = Assert.Single(provider.WillObservations, observation => observation.AgentId == laterPartner).Will!;
        Assert.Equal("your partner", laterWill.Heirs.Single(heir => heir.Name == survivorName).Relation);
        var laterCheckpoint = WorldRuntimeCodec.Encode(later.ExportState());
        using var laterLoaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(laterCheckpoint), _ => provider);
        Assert.Equal(laterCheckpoint, WorldRuntimeCodec.Encode(laterLoaded.ExportState()));
        Assert.True((await later.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.True((await laterLoaded.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(later.ExportState()), WorldRuntimeCodec.Encode(laterLoaded.ExportState()));
        AssertPartnerLoss(laterLoaded, provider, survivor, 1);
    }

    private static WorldRuntimeState PrepareDeath(WorldRuntimeState state, string relationshipId,
        string proposer, string target, string deceased, bool accepted)
    {
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint,
            new SocietyRelationshipProposal(relationshipId, 1, SocietyRelationshipType.Partnership,
                proposer, target, checkpoint.WorldTick, PrivacyClass: "family")));
        if (accepted)
            society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, relationshipId, 1, target));
        var prepared = society.ExportState();
        var checkpoint = prepared.Society;
        var maximumAge = checkpoint.Config.DayLifecycle!.MaximumDay;
        var birth = checkpoint.LifeTickAt(checkpoint.WorldTick + 1) - maximumAge * checkpoint.Config.TicksPerLifecycleAge;
        // Position the calendar at its real next mortality boundary. The native
        // tick performs death, physical archival, estate creation and discovery.
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.Id == deceased ? person with
            {
                BirthTick = birth,
                BirthLifeTick = checkpoint.LifeClock is null ? null : birth,
                AgeBand = SocietyAgeBand.Elder,
                LastLifecycleYearChecked = maximumAge - 1,
            } : person).ToArray(),
            Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, relationshipId + "-keepsake", "wood", deceased, 1),
        };
        return state with
        {
            Society = prepared with { Society = checkpoint },
            Agents = state.Agents.Select(person => person with { IdentityChoicePending = false }).ToArray(),
        };
    }

    private static async Task AdvanceAndAdmit(WorldRuntime world)
    {
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        await GrownAgentHelperMemoryTests.WaitForTransferChoices(world);
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
    }

    private static void AssertPartnerLoss(WorldRuntime world, PartnerLossProvider provider, string survivor, int count)
    {
        var moments = world.Agents.Single(person => person.AgentId == survivor).IdentityMoments ?? [];
        Assert.Equal(count, moments.Count(moment => moment.Kind == AgentIdentityMomentKind.PartnerLoss));
        Assert.Equal(count, provider.IdentityCalls.Count(request => request.Observation.AgentId == survivor));
        Assert.Equal(count, world.ExportState().Events.Count(entry => entry.Kind == "agent_identity_moment_started" &&
            entry.Detail == survivor + ":PartnerLoss"));
        Assert.All(moments, moment => Assert.Equal("kept", moment.Outcome));
    }

    private sealed class PartnerLossProvider : IDecisionProvider
    {
        private readonly ActionCoverageRecorder idle = new(chooseIdle: true);
        public ConcurrentQueue<CognitionDecisionRequest> IdentityCalls { get; } = new();
        public ConcurrentQueue<AgentObservation> WillObservations { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public DecisionProviderKind KindFor(AgentObservation observation)
        {
            if (observation.Will is not null) WillObservations.Enqueue(observation);
            return observation.IdentityMoment is null ? DecisionProviderKind.Deterministic : DecisionProviderKind.LargeLanguageModel;
        }

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.IdentityMoment is null) return idle.DecideAsync(request, cancellationToken);
            IdentityCalls.Enqueue(request);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                DecisionProviderKind.LargeLanguageModel, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, "identity_optional", 1,
                new Dictionary<string, double>()));
        }
    }
}
