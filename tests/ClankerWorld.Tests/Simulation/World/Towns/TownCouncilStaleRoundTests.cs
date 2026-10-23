using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownCouncilStaleRoundTests
{
    private const int Day = TownCouncilTestWorld.Day;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedMainRoundChoiceCannotBecomeRunoffConsentAfterAnActualReadAndRelay(bool structured)
    {
        var initial = TownCouncilTestWorld.Prepare("council-held-main-round");
        var town = initial.Towns![0];
        var ids = town.ResidentIds.ToArray();
        var reader = ids[0];
        var heldActor = ids[2];
        var target = ids[1];
        var council = TownCouncilState.Create(ids);
        foreach (var id in ids.Take(5))
            council = TownCouncilRules.Register(council, id, true, null, ids, 0);
        council = TownCouncilRules.Advance(council, town.Id, initial.WorldSeed, ids, 0, Day);
        var contestId = council.Election!.Id;
        council = TownCouncilRules.VoteElection(council, contestId, reader, [ids[0], ids[1], ids[2]], 0);
        council = TownCouncilRules.VoteElection(council, contestId, ids[1], [ids[0], ids[3]], 0);
        foreach (var actor in ids)
            council = TownCouncilRules.LearnNotices(council, actor, council.Notices.Select(n => n.Id), 0);
        var shortened = initial with
        {
            Towns = [town with { Council = council }],
            Agents = initial.Agents.Select(p => p with
            {
                Position = town.OriginSite!.Value,
                FullnessBasisPoints = 8_000,
            }).ToArray(),
        };
        var control = new RoundControl(heldActor, reader, target, structured);
        var providers = new ConcurrentDictionary<string, RoundProvider>(StringComparer.Ordinal);
        using var world = WorldRuntime.Restore(shortened,
            id => providers.GetOrAdd(id, actor => new RoundProvider(actor, control)));
        try
        {
            for (var tick = 0; tick < 5 && !control.Started.Task.IsCompleted; tick++)
            {
                await world.TickModelWorkAsync();
                await ModelWorkHarness.For(world).SettleAsync();
            }
            await ModelWorkHarness.WaitForSignalAsync(control.Started.Task);
            while (world.WorldTick < Day + 5 && !HasRunoffRelay(world, heldActor, reader))
            {
                await world.TickModelWorkAsync();
                await ModelWorkHarness.For(world).SettleAsync();
            }
            Assert.Equal("runoff", world.Towns[0].Council!.Election!.Stage);
            Assert.Equal(contestId, world.Towns[0].Council!.Election!.Id);
            Assert.True(HasRunoffRelay(world, heldActor, reader), "The held voter must actually learn the runoff from an informed reader.");
            var heldPlan = Assert.IsType<CouncilBallotActionPlan>(control.MainPlan);
            Assert.Equal("main", heldPlan.Stage);
            var beforeRefusal = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(heldPlan).Refusal!.Reason);
            Assert.Equal(beforeRefusal, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Contains(world.Towns[0].Council!.Knowledge, receipt => receipt.AgentId == reader &&
                receipt.SourceAgentId is null && world.Towns[0].Council!.Notices.Any(n => n.Id == receipt.NoticeId && n.Kind == "runoff"));
            control.Release.TrySetResult(true);
            while (world.WorldTick < Day + 8)
            {
                await world.TickModelWorkAsync();
                await ModelWorkHarness.For(world).SettleAsync();
            }
            var runoff = world.Towns[0].Council!.Election!;
            Assert.Equal("runoff", runoff.Stage);
            Assert.DoesNotContain(runoff.Ballots, ballot => ballot.AgentId == heldActor);
            var fresh = Assert.Single(runoff.Ballots, ballot => ballot.AgentId == reader);
            Assert.Equal([target], fresh.Choices);
            Assert.NotNull(control.MainAction);
            Assert.NotNull(control.FreshAction);
            Assert.NotEqual(control.MainAction, control.FreshAction);
            world.Validate();
        }
        finally
        {
            control.Release.TrySetResult(true);
        }
    }

    private static bool HasRunoffRelay(WorldRuntime world, string actor, string source)
    {
        var state = world.Towns[0].Council!;
        return state.Knowledge.Any(receipt => receipt.AgentId == actor && receipt.SourceAgentId == source &&
            state.Notices.Any(n => n.Id == receipt.NoticeId && n.Kind == "runoff"));
    }

    private sealed class RoundControl(string heldActor, string reader, string target, bool structured)
    {
        public string HeldActor { get; } = heldActor;
        public string Reader { get; } = reader;
        public string Target { get; } = target;
        public bool Structured { get; } = structured;
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public string? MainAction { get; set; }
        public CouncilBallotActionPlan? MainPlan { get; set; }
        public string? FreshAction { get; set; }
    }

    private sealed class RoundProvider(string actor, RoundControl control) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var o = request.Observation;
            var vote = o.Candidates.FirstOrDefault(c => control.Structured
                ? c.Id.Contains("|ballot|", StringComparison.Ordinal)
                : c.Id.Contains("|single|", StringComparison.Ordinal) && c.Id.Split('|')[4] == control.Target);
            var selected = o.Candidates.Single(c => c.Id == "safe_idle");
            if (actor == control.HeldActor && !control.Started.Task.IsCompleted && vote is not null)
            {
                selected = vote;
                control.MainAction = selected.Id;
                control.MainPlan = Assert.IsType<CouncilBallotActionPlan>(selected.ActionPlan);
                control.Started.TrySetResult(true);
                // Deliberately ignore cancellation: authority must reject a reply after the round changes.
                await control.Release.Task;
            }
            else if (actor == control.Reader && o.WorldTick >= Day)
            {
                selected = o.Candidates.FirstOrDefault(c => c.Id.Contains("|read|", StringComparison.Ordinal)) ??
                    o.Candidates.FirstOrDefault(c => c.Id.Contains("|relay|", StringComparison.Ordinal) && c.Id.Split('|')[3] == control.HeldActor) ??
                    vote ?? selected;
                if (selected == vote) control.FreshAction = selected.Id;
            }
            return new(request.RequestId, o.AgentId, Kind, ProviderEpoch, o.RunEpoch,
                o.DecisionGeneration, o.ObservationDigest, selected.Id, 1,
                o.Candidates.ToDictionary(c => c.Id, c => c.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicBallot: control.Structured && selected.Id.Contains("|ballot|", StringComparison.Ordinal) ? [control.Target] : null);
        }
    }
}
