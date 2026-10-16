using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownLandClaimHeldPlotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADelayedClaimChecksTheSelectedPlotAfterTheSuggestedPlotsAreClaimed(bool claimSuggestions)
    {
        using var source = NormalPathWorld.CreateGenerated("council-land-claim",
            _ => new ActionCoverageRecorder(chooseIdle: true), ticksPerDay: 80);
        var initial = source.ExportState();
        var town = initial.Towns![0];
        var actor = town.ResidentIds[0];
        var voter = town.ResidentIds[3];
        var origin = town.OriginSite!.Value;
        var nearby = TownLandClaimRules.ClaimableNear(initial.Map, town, initial.TownLandTitles!, origin, 7);
        Assert.Equal(7, nearby.Length);
        var suggestions = nearby.Take(6).ToArray();
        var selectedPlot = nearby[6];
        var council = town.Council!;
        Assert.Equal(4, council.Members.Count);
        foreach (var tile in suggestions)
        {
            council = TownCouncilRules.SubmitProposal(council, town.Id, voter, "land_claim", null,
                "Claim adjoining land.", "council:" + council.Revision, town.ResidentIds, source.WorldTick,
                initial.WorldSystems!.Config.TicksPerDay, landClaimTiles: [tile]);
            var proposal = council.Proposals[^1];
            Assert.Equal(3, proposal.RequiredYes);
            foreach (var yes in town.ResidentIds.Take(2))
                council = TownCouncilRules.VoteProposal(council, proposal.Id, yes, true, source.WorldTick);
        }
        foreach (var resident in town.ResidentIds)
            council = TownCouncilRules.LearnNotices(council, resident, council.Notices.Select(notice => notice.Id), source.WorldTick);
        var provider = new HeldClaimProvider(actor, voter, selectedPlot, claimSuggestions);
        using var world = WorldRuntime.Restore(initial with
        {
            Towns = [town with { Council = council }],
            Agents = initial.Agents.Select(person => person with { Position = origin, FullnessBasisPoints = 10_000 }).ToArray(),
        }, _ => provider);
        try
        {
            for (var tick = 0; tick < 5 && !provider.Started.Task.IsCompleted; tick++)
                await world.TickModelWorkAsync();
            await ModelWorkHarness.WaitForSignalAsync(provider.Started.Task);
            Assert.Equal(suggestions, provider.OfferedPlan!.OfferedTiles);
            Assert.DoesNotContain(selectedPlot, provider.OfferedPlan.OfferedTiles);
            var votes = new List<SocietyCognitionDispatchResult>();
            for (var tick = 0; claimSuggestions && tick < 35 && world.Towns[0].Council!.Proposals.Any(p => p.Status == "pending"); tick++)
            {
                world.SubmitInstruction(new OwnerInstructionRequest("claim-vote-" + tick, "owner:test", voter,
                    OwnerInstructionKind.Suggestion, "Consider the pending adjoining land claims."));
                var step = await world.TickModelWorkAsync();
                votes.AddRange(step.Decisions.Where(decision => decision.AgentId == voter &&
                    decision.Admission.Intention?.CandidateId.Contains("|yes|", StringComparison.Ordinal) == true));
            }
            if (claimSuggestions)
            {
                Assert.Equal(6, votes.Count);
                Assert.All(votes, vote =>
                {
                    Assert.True(vote.Admission.Accepted);
                    Assert.False(vote.Admission.FellBack);
                    Assert.Null(vote.PlanRefusal);
                });
                Assert.Equal(6, world.ExportState().Events.Count(entry => entry.Kind == "town_land_claimed"));
                Assert.All(suggestions, tile => Assert.False(TownLandClaimRules.CanClaim(
                    initial.Map, world.Towns[0], [tile], world.TownLandTitles)));
            }
            Assert.True(TownLandClaimRules.CanClaim(initial.Map, world.Towns[0], [selectedPlot], world.TownLandTitles));
            Assert.True(provider.FreshClaimAvailable);
            provider.Release.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitForDecisionAsync(actor);
            SocietyCognitionDispatchResult? admitted = null;
            for (var tick = 0; tick < 5 && admitted is null; tick++)
            {
                var step = await world.TickModelWorkAsync();
                admitted = step.Decisions.SingleOrDefault(decision => decision.AgentId == actor &&
                    decision.Admission.Intention?.CandidateId == provider.OfferedPlan.CandidateId);
            }
            Assert.NotNull(admitted);
            Assert.True(admitted.Admission.Accepted);
            Assert.False(admitted.Admission.FellBack);
            var plan = Assert.IsType<TownLandClaimActionPlan>(admitted.ActionPlan);
            Assert.Equal([selectedPlot], plan.SelectedTiles);
            Assert.True(world.CheckActionPlan(plan).Allowed);
            Assert.Null(admitted.PlanRefusal);
            Assert.Contains(world.Towns[0].Council!.Proposals, proposal => proposal.AuthorId == actor &&
                proposal.LandClaimTiles!.SequenceEqual([selectedPlot]));
            world.Validate();
        }
        finally
        {
            provider.Release.TrySetResult(true);
        }
    }

    private sealed class HeldClaimProvider(string actor, string voter, GridPoint plot, bool claimSuggestions) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public TownLandClaimActionPlan? OfferedPlan { get; private set; }
        public bool FreshClaimAvailable { get; private set; }

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var claim = observation.Candidates.FirstOrDefault(candidate => candidate.ActionPlan is TownLandClaimActionPlan);
            var selected = observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            if (observation.AgentId == actor && !Started.Task.IsCompleted && claim is not null)
            {
                selected = claim;
                OfferedPlan = Assert.IsType<TownLandClaimActionPlan>(claim.ActionPlan);
                Started.TrySetResult(true);
                await Release.Task;
            }
            else if (observation.AgentId == voter)
            {
                FreshClaimAvailable = claim is not null;
                if (claimSuggestions && Started.Task.IsCompleted)
                    selected = observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|yes|", StringComparison.Ordinal)) ?? selected;
            }
            return new(request.RequestId, observation.AgentId, Kind, ProviderEpoch, observation.RunEpoch,
                observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicLandTiles: selected == claim ? [new(plot.X, plot.Y)] : null);
        }
    }
}
