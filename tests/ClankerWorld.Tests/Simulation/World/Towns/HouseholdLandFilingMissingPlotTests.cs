using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HouseholdLandGrantTests
{
    [Fact]
    public async Task AHouseholdLandReplyWithoutTilesIsRefusedBeforeNativeSubmission()
    {
        using var source = Create("household-grant", new Choices());
        var initial = source.ExportState();
        var town = initial.Towns![0];
        var actor = town.ResidentIds[0];
        using var world = WorldRuntime.Restore(initial, _ => new MissingHouseholdPlotProvider(actor, town.Id));
        SocietyCognitionDispatchResult? admitted = null;
        await Until(world, () => admitted is not null, step => admitted ??= step.Decisions.SingleOrDefault(decision =>
            decision.AgentId == actor && decision.Admission.Intention?.CandidateId.Contains("|request_land_use|", StringComparison.Ordinal) == true));
        Assert.True(admitted!.Admission.Accepted);
        Assert.False(admitted.Admission.FellBack);
        Assert.Equal(DecisionProviderKind.LargeLanguageModel, admitted.Admission.Intention!.Provider);
        var plan = Assert.IsType<HouseholdLandFilingActionPlan>(admitted.ActionPlan);
        Assert.NotEmpty(plan.OfferedTiles);
        Assert.Equal(world.Society.GetAgent(actor).HouseholdId, plan.Household);
        Assert.NotNull(admitted.PlanRefusal);
        Assert.Equal(ActionRefusalReason.TargetUnavailable, admitted.PlanRefusal.Reason);
        Assert.NotNull(plan.SelectedTiles);
        Assert.Empty(plan.SelectedTiles);
        Assert.Empty(world.HouseholdLandUseRequests);
        Assert.Equal(initial.Towns[0].Council!.Proposals, world.Towns[0].Council!.Proposals);
        Assert.Equal(initial.TownLandTitles, world.TownLandTitles);
        Assert.Equal(initial.HouseholdLandUseRights, world.HouseholdLandUseRights);
        Assert.Equal(initial.WorldSimulation!.Buildings, world.WorldSimulation.Buildings);
        var after = world.ExportState();
        Assert.Single(after.Events.Skip(initial.Events.Count), item => item.Kind == "choice_refused");
        Assert.DoesNotContain(after.Events.Skip(initial.Events.Count), item => item.Kind.StartsWith("town_civic_action", StringComparison.Ordinal));
        Assert.Null(after.Society.Cognition.Runtimes.Single(runtime => runtime.AgentId == actor).CurrentIntention);
        var encoded = WorldRuntimeCodec.Encode(after);
        Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Validate();
    }

    private sealed class MissingHouseholdPlotProvider(string actor, string town) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.ActionPlan is HouseholdLandFilingActionPlan
                { Choice.BuildingId: null } plan && plan.Choice.TownId == town) : null;
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
