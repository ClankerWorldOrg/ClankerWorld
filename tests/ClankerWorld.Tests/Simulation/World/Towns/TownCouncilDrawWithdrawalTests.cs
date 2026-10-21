using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownGovernmentHandoverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithdrawalAfterADrawKeepsItsRecordedSupportAndReplaysBeforeHandover(bool winner)
    {
        var town = AllAdultTown();
        town.RegisterCouncil("a", "b", "c", "d", "e");
        town.Yes(town.Propose(ElectedMayor), "a", "b", "c");
        town.RegisterMayor("a", "land");
        var electionId = town.Council.Election!.Id;
        town.Council = TownCouncilRules.VoteElection(town.Council, electionId, "a", FirstCouncil, 0);
        town.Council = TownCouncilRules.VoteElection(town.Council, electionId, "b", ["a", "d"], 0);
        town.Advance(Day);
        Assert.Equal("runoff", town.Council.Election!.Stage);
        town.Advance(2 * Day);
        var ready = town.Council.Election!;
        Assert.Equal("ready", ready.Stage);
        Assert.Equal(3, ready.DrawOrder.Count);
        var actor = winner ? ready.DrawOrder[0] : ready.DrawOrder[^1];
        Assert.Equal(winner, ready.SettledSeats.Contains(actor));
        var restored = town.RoundTrip();

        foreach (var run in new[] { town, restored })
        {
            var system = new TownSystem(CivicState.Empty);
            var publishedTown = new TownRuntimeState(run.Id, "Test Town", "founded", 0,
                run.Adults, [], [new(0, 0)], Council: run.Council);
            system.RestoreTowns([publishedTown]);
            var before = system.State;
            var priorElection = run.Council.Election;
            run.Council = TownCouncilRules.WithdrawCandidate(run.Council, actor, 2 * Day + 1);
            run.Advance(2 * Day + 1);
            var contest = winner
                ? Assert.Single(run.Council.ElectionHistory, item => item.Id == electionId)
                : Assert.IsType<TownElection>(run.Council.Election);
            Assert.Equal(winner ? "failed" : "ready", contest.Stage);
            Assert.Equal(ready.DrawOrder, contest.DrawOrder);
            Assert.Equal(ready.Supported, contest.Supported);
            Assert.DoesNotContain(actor, contest.Candidates);
            Assert.DoesNotContain(actor, contest.SettledSeats);
            Assert.DoesNotContain(run.Council.Candidates, candidate => candidate.AgentId == actor);
            Assert.All(contest.Ballots, ballot => Assert.DoesNotContain(actor, ballot.Choices));
            if (!winner) Assert.Equal(ready.SettledSeats, contest.SettledSeats);
            system.SetTownState(publishedTown with { Council = run.Council });
            Assert.Same(run.Council, Assert.Single(system.State.Towns).Council);
            Assert.Same(priorElection, Assert.Single(before.Towns).Council!.Election);
            run.RoundTrip();
        }
        Assert.Equal(town.Save(), restored.Save());
    }
}
