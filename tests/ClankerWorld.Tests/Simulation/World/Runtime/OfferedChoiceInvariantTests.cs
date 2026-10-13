using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OfferedChoiceInvariantTests
{
    [Fact]
    public async Task EveryOfferedChoiceIsSafeInAStrictTrial()
    {
        var seed = int.Parse(Environment.GetEnvironmentVariable("CLANKERWORLD_CHOICE_SEED") ?? "1815",
            System.Globalization.CultureInfo.InvariantCulture);
        var state = await OfferedChoiceWorld.CreateAsync(seed);
        using var baseline = WorldRuntime.Restore(state);
        var recorder = new SweepChoiceProvider();
        using var discovery = baseline.CopyForChoiceTrial(_ => recorder);
        var found = await discovery.AdvanceChoiceTrialAsync(StrictCheckpoint);
        Assert.True(found.Advanced);
        Assert.NotEmpty(recorder.Observations);
        foreach (var observation in recorder.Observations)
            foreach (var candidate in observation.Candidates)
            {
                var context = $"seed={seed}; scenario=stock; agent={observation.AgentId}; choice={candidate.Id}";
                try
                {
                    var provider = new SweepChoiceProvider(observation.AgentId, candidate.Id);
                    using var trial = baseline.CopyForChoiceTrial(_ => provider);
                    var result = await trial.AdvanceChoiceTrialAsync(StrictCheckpoint);
                    Assert.True(result.Advanced, result.Outcome);
                    var decision = Assert.Single(result.Decisions, decision => decision.AgentId == observation.AgentId);
                    Assert.True(decision.Admission.Accepted, decision.Admission.Outcome);
                    Assert.False(decision.Admission.FellBack);
                    Assert.Equal(candidate.Id, decision.Admission.Intention!.CandidateId);
                    Assert.Contains(provider.Observations, actual => actual.AgentId == observation.AgentId &&
                        actual.Candidates.Any(offered => offered.Id == candidate.Id));

                }
                catch (Exception exception)
                {
                    Assert.Fail(context + "\n" + exception);
                }
            }
    }

    private static Action StrictCheckpoint(WorldRuntime proposed)
    {
        var bytes = WorldRuntimeCodec.Encode(proposed.ExportState());
        WorldRuntimeCodec.Decode(bytes);
        return () => { };
    }
}
