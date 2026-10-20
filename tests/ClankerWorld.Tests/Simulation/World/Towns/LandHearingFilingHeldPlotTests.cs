using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownLandHearingRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADelayedFilingChecksItsExactPlotAfterSuggestedRequestsAreWithdrawn(bool withdrawSuggestions)
    {
        var settled = await SettledDispute.Value;
        var provider = new HeldFilingProvider(withdrawSuggestions);
        var initial = WorldRuntimeCodec.Decode(settled.Save);
        using var world = WorldRuntime.Restore(initial with
        {
            Agents = initial.Agents.Select(person => person with
            { Position = initial.Towns![0].OriginSite!.Value, FullnessBasisPoints = 10_000 }).ToArray()
        }, _ => provider);
        var town = world.Towns[0];
        var available = world.TownLandTitles.SelectMany(title => title.Tiles).Distinct()
            .Where(tile => !world.HouseholdLandUseRights.Any(right => right.Tiles.Contains(tile)))
            .OrderBy(tile => initial.Map.FootDistance(town.OriginSite!.Value, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X);
        foreach (var tile in available)
        {
            var result = world.RequestHouseholdLandUse("held-filing-request-" + provider.Plots.Count, Waiver, town.Id, [tile]);
            if (!result.Applied) continue;
            Assert.False(result.IsDisputed);
            provider.Plots.Add(tile);
            if (provider.Plots.Count == 7) break;
        }
        Assert.Equal(7, provider.Plots.Count);
        var permissions = Permissions(world.ExportState());
        var property = PrivateProperty(world.ExportState());
        world.SubmitInstruction(new("consider-held-filing", "owner:test", Filer, OwnerInstructionKind.Suggestion,
            "Consider filing the exact household land disagreement."));
        try
        {
            for (var tick = 0; tick < 8 && !provider.Started.Task.IsCompleted; tick++) await world.TickModelWorkAsync();
            await ModelWorkHarness.WaitForSignalAsync(provider.Started.Task);
            var offered = Assert.IsType<LandHearingFilingActionPlan>(provider.OfferedPlan);
            Assert.Equal(6, offered.NearbyAffectedTiles.Count);
            Assert.DoesNotContain(provider.SelectedPlot, offered.NearbyAffectedTiles);
            var withdrawals = new List<SocietyCognitionDispatchResult>();
            for (var tick = 0; withdrawSuggestions && tick < 35 && world.HouseholdLandUseRequests.Count(request => request.Status == "pending") > 1; tick++)
            {
                world.SubmitInstruction(new("withdraw-suggested-request-" + tick, "owner:test", Waiver, OwnerInstructionKind.Suggestion,
                    "Consider withdrawing the specified pending household land requests."));
                var step = await world.TickModelWorkAsync();
                withdrawals.AddRange(step.Decisions.Where(decision => decision.AgentId == Waiver &&
                    decision.ActionPlan is HouseholdLandResponseActionPlan { Choice.Kind: HouseholdLandResponseKind.Withdraw }));
            }
            if (withdrawSuggestions)
            {
                Assert.Equal(6, withdrawals.Count);
                Assert.All(withdrawals, decision =>
                {
                    Assert.True(decision.Admission.Accepted);
                    Assert.False(decision.Admission.FellBack);
                    Assert.Null(decision.PlanRefusal);
                });
                Assert.All(offered.NearbyAffectedTiles, tile => Assert.Contains(world.HouseholdLandUseRequests,
                    request => request.Tiles.Contains(tile) && request.Status == "withdrawn"));
            }
            Assert.Contains(world.HouseholdLandUseRequests, request => request.Status == "pending" && request.Tiles.SequenceEqual([provider.SelectedPlot]));
            provider.Release.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitForDecisionAsync(Filer);
            SocietyCognitionDispatchResult? admitted = null;
            for (var tick = 0; tick < 8 && admitted is null; tick++)
            {
                var step = await world.TickModelWorkAsync();
                admitted = step.Decisions.SingleOrDefault(decision => decision.AgentId == Filer &&
                    decision.Admission.Intention?.CandidateId == offered.CandidateId);
            }
            Assert.NotNull(admitted);
            Assert.True(admitted.Admission.Accepted);
            Assert.False(admitted.Admission.FellBack);
            var plan = Assert.IsType<LandHearingFilingActionPlan>(admitted.ActionPlan);
            Assert.Equal([provider.SelectedPlot], plan.Filing!.Tiles);
            Assert.Null(admitted.PlanRefusal);
            Assert.Contains(world.Towns[0].LandRecords.Cases, item => item.Status == "pending" &&
                TownLandHearingRules.CurrentRevision(item).Tiles.SequenceEqual([provider.SelectedPlot]) &&
                item.Filings.Any(filing => filing.AgentId == Filer));
            Assert.Equal(permissions, Permissions(world.ExportState()));
            Assert.Equal(property, PrivateProperty(world.ExportState()));
            world.Validate();
            world.Pause();
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.True(world.CheckActionPlan(plan).Allowed);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ActionCoverageRecorder(chooseIdle: true));
            restored.Validate();
            Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally { provider.Release.TrySetResult(true); }
    }

    private sealed class HeldFilingProvider(bool withdrawSuggestions) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public List<GridPoint> Plots { get; } = [];
        public LandHearingFilingActionPlan? OfferedPlan { get; private set; }
        public GridPoint SelectedPlot { get; private set; }

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var selected = observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            var filing = observation.Candidates.FirstOrDefault(candidate => candidate.ActionPlan is LandHearingFilingActionPlan
            { Choice.Kind: LandHearingFilingKind.Household });
            if (observation.AgentId == Filer && !Started.Task.IsCompleted && filing is not null)
            {
                selected = filing;
                OfferedPlan = Assert.IsType<LandHearingFilingActionPlan>(filing.ActionPlan);
                SelectedPlot = Assert.Single(Plots.Except(OfferedPlan.NearbyAffectedTiles));
                Started.TrySetResult(true);
                await Release.Task;
            }
            else if (observation.AgentId == Waiver && withdrawSuggestions && Started.Task.IsCompleted)
            {
                selected = observation.Candidates.FirstOrDefault(candidate => candidate.ActionPlan is HouseholdLandResponseActionPlan response &&
                    response.Choice.Kind == HouseholdLandResponseKind.Withdraw && response.Request.Tiles.Any(OfferedPlan!.NearbyAffectedTiles.Contains)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal)) ?? selected;
            }
            return new(request.RequestId, observation.AgentId, Kind, ProviderEpoch, observation.RunEpoch,
                observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicLandTiles: selected == filing ? [new(SelectedPlot.X, SelectedPlot.Y)] : null,
                CivicLandHearing: selected == filing ? new(Statement: "Please hear this exact pending household plot.", RequestedOutcome: "confirm") : null);
        }
    }
}
