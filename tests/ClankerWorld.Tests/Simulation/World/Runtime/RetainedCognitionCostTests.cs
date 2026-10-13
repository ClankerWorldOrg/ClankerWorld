using System.Collections.Concurrent;
using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RetainedCognitionCostTests
{
    private const string TargetId = "founder:00000000000000000000000000000001";

    // Ordinary peers change the world while a reply is held, so some choices
    // disappear. That leaves the accepted reply valid and needs no second call.
    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public async Task ChoicesThatOnlyDisappearWhileAReplyIsHeldAskNoSecondCall(int heldTicks)
    {
        var provider = new FirstHeldProvider();
        using var world = NormalPathWorld.CreateGenerated("review-cost", id =>
            id == TargetId ? provider : new DeterministicDecisionProvider());
        for (var tick = 0; tick < 400 && provider.Requests.IsEmpty; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        await provider.Started.Task;
        for (var tick = 0; tick < heldTicks; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        var original = provider.Requests.First().Observation.Candidates.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal);
        var current = (List<CognitionCandidate>)typeof(WorldRuntime)
            .GetMethod("CreateCandidates", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [TargetId, world.Agents.Single(person => person.AgentId == TargetId), true])!;
        Assert.Contains(current, candidate => candidate.Id == "explore");
        Assert.True(original.Except(current.Select(candidate => candidate.Id)).Any(),
            "The held observation must actually lose an offered choice.");
        provider.Release.TrySetResult();
        await provider.Returned.Task;
        var admitted = false;
        for (var tick = 0; tick < 20 && !admitted; tick++)
        {
            await ModelWorkHarness.For(world).SettleAsync();
            var step = await world.TickModelWorkAsync();
            Assert.True(step.Advanced);
            admitted = step.Decisions.Any(decision => decision.AgentId == TargetId);
        }
        Assert.True(admitted);
        for (var tick = 0; tick < 6; tick++)
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).SettleAsync();
        }
        // A later call is justified only by a choice the first request lacked.
        var requests = provider.Requests.ToArray();
        var first = requests[0].Observation.Candidates.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(requests.Skip(1), request => Assert.True(
            request.Observation.Candidates.Any(candidate => !first.Contains(candidate.Id)),
            $"tick={request.Observation.WorldTick} {string.Join(",", request.Observation.Candidates.Select(c => c.Id).Order(StringComparer.Ordinal))}"));
    }

    private sealed class FirstHeldProvider : IDecisionProvider
    {
        private int calls;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<CognitionDecisionRequest> Requests { get; } = new();
        public InlineCompletionSource Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource Release { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource Returned { get; } = new(TaskCreationOptions.None);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref calls);
            Requests.Enqueue(request);
            if (call == 1)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                Returned.TrySetResult();
            }
            var observation = request.Observation;
            // The selected action must survive the shrinking choice set. A peer
            // taking the selected communal tool legitimately needs another call.
            var selected = call == 1 ? observation.Candidates.Single(c => c.Id == "explore").Id :
                observation.Candidates.OrderBy(c => c.DeterministicPriority).ThenBy(c => c.Id, StringComparer.Ordinal)
                .First(c => c.Id != "safe_idle" && !c.Id.StartsWith("conversation", StringComparison.Ordinal)).Id;
            return new(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected, 1,
                observation.Candidates.ToDictionary(c => c.Id, c => c.Id == selected ? 1d : 0d, StringComparer.Ordinal),
                ChosenName: observation.NeedsName ? "Reviewa" : null,
                ChosenPersonality: observation.NeedsPersonality ? "Calm" : null,
                ChosenAspiration: observation.NeedsAspiration ? "Build a home" : null);
        }
    }
}
