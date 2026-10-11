using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Tests;

public sealed class CivicSystemTests
{
    [Fact]
    public void MajorityAdoptsPolicyWithoutChangingTheDiscardedBallotSnapshot()
    {
        var world = new FakeWorld();
        var system = new CivicSystem(CivicState.Empty with { Council = new("agent-a", "open", 0) });
        system.ApplyCouncilCandidate(world, "agent-a", "council_propose:essential_first");
        var proposed = system.State;
        var discarded = new CivicSystem(proposed);
        world.WorldTick = 301;
        system.ApplyCouncilCandidate(world, "agent-b", "council_vote_yes");
        Assert.Equal("essential_first", system.State.Council!.FoodPolicy);
        Assert.Null(system.State.Council.Ballot);
        Assert.Equal(301, system.State.Council.LastResolutionTick);
        Assert.Same(proposed, discarded.State);
        Assert.Equal("open", discarded.State.Council!.FoodPolicy);
        Assert.Equal("agent-a", Assert.Single(discarded.State.Council.Ballot!.Approvals));
        Assert.Collection(world.Events,
            item => Assert.Equal(("council_policy_proposed", "essential_first"), item),
            item => Assert.Equal(("council_vote_recorded", "agent-b"), item),
            item => Assert.Equal(("council_policy_adopted", "essential_first"), item));
    }

    [Fact]
    public void ExpiryAndUrgentWarmthUseTheCurrentWorld()
    {
        var world = new FakeWorld();
        var system = new CivicSystem(CivicState.Empty with { Council = new("agent-a", "open", 0) });
        world.Urgent.Add("agent-a");
        Assert.False(system.HasCouncilDecision(world, "agent-a"));
        world.Urgent.Clear();
        Assert.True(system.HasCouncilDecision(world, "agent-a"));
        system.ApplyCouncilCandidate(world, "agent-a", "council_propose:essential_first");
        world.WorldTick = 420;
        system.ResolveCouncilBallot(world);
        Assert.NotNull(system.State.Council!.Ballot);
        world.WorldTick = 421;
        system.ResolveCouncilBallot(world);
        Assert.Null(system.State.Council.Ballot);
        Assert.Equal("open", system.State.Council.FoodPolicy);
        Assert.Equal(("council_policy_rejected", "essential_first"), world.Events[^1]);
    }

    [Fact]
    public void CivicSnapshotDoesNotReplaceTownLandOrMembership()
    {
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], [])
        { Governance = TownGovernanceState.Create(["agent-a"]) };
        var system = new CivicSystem(CivicState.Empty);
        system.RestoreTowns([town]);
        var original = system.State;
        var members = town with { ResidentIds = ["agent-a", "agent-b"] };
        system.SetTownState(members);
        Assert.Same(original, system.State);
        var proposal = new CivicSystem(original);
        var revised = members with { Governance = town.Governance with { Revision = 1 } };
        proposal.SetTownState(revised);
        Assert.Equal(0, original.Towns["town"].Governance!.Revision);
        Assert.Equal(1, proposal.State.Towns["town"].Governance!.Revision);
        var checkpoint = proposal.CheckpointTown(members);
        Assert.Same(members.ResidentIds, checkpoint.ResidentIds);
        Assert.Same(members.BorderTiles, checkpoint.BorderTiles);
        Assert.Same(revised.Governance, checkpoint.Governance);
    }

    [Fact]
    public void CivicHistoryIsSharedBySnapshotAndInvalidatedByAReplacement()
    {
        var governance = TownGovernanceState.Create(["agent-a"]) with
        {
            Notices = [new("notice", "result", "proposal", "Adopted", 10)],
            Knowledge = [new("agent-a", "notice", 11)],
        };
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], []) { Governance = governance };
        var history = CivicSystem.CivicHistory(town);
        Assert.Same(history, CivicSystem.CivicHistory(town with { Name = "Renamed" }));
        Assert.True(history.Knows("agent-a", "result", "proposal"));
        Assert.Equal("notice", Assert.Single(history.Recent("agent-a")).NoticeId);
        var replacement = CivicSystem.CivicHistory(town with { Governance = governance with { Knowledge = [] } });
        Assert.NotSame(history, replacement);
        Assert.False(replacement.Knows("agent-a", "proposal"));
        Assert.True(history.Knows("agent-a", "proposal"));
    }

    [Fact]
    public void NoticeContextUsesKnownReceiptsAndTheCurrentNamesAndCalendar()
    {
        var world = new FakeWorld();
        var governance = TownGovernanceState.Create(["agent-a"]) with
        {
            Notices = [new("known", "result", "proposal", "agent-b voted at tick 10", 10),
                new("unread", "result", "other", "Unread notice", 12)],
            Knowledge = [new("agent-a", "known", 11, "agent-b")],
        };
        world.Towns = [new("town", "Town", "founded", 0, ["agent-a"], [], []) { Governance = governance }];
        var day = WorldCalendarRules.FromTick(10, world.CalendarConfig).DayIndex + 1;
        Assert.Equal($"Heard from Rowan Reed: Town: Rowan Reed voted at world day {day}", CivicSystem.CivicNote(world, "agent-a"));
        Assert.Null(CivicSystem.CivicNote(world, "agent-b"));
    }

    [Fact]
    public void HistoryVerificationDetectsChangedNoticeContentBehindTheSameSnapshot()
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var notices = new List<TownCivicNotice> { new("notice", "result", "proposal", "Original", 10) };
        var governance = TownGovernanceState.Create(["agent-a"]) with { Notices = notices };
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], []) { Governance = governance };
        CivicSystem.CivicHistory(town);
        notices[0] = notices[0] with { Text = "Changed" };
        Assert.Throws<InvalidOperationException>(() => CivicSystem.CivicHistory(town));
    }

    private sealed class FakeWorld : ICivicHistoryWorld
    {
        public IReadOnlyList<TownRuntimeState> Towns { get; set; } = [];
        public WorldSystemsConfig CalendarConfig { get; } = new();
        public long WorldTick { get; set; } = 300;
        public bool HasSurvivalState => true;
        public SocietyCheckpoint Society { get; } = SocietyFixture.CreateHousehold(
            SocietyFixture.CreateGenesis("civic-system-test",
                [SocietyFixture.CreateFounder("agent-a", "Aster Ash"), SocietyFixture.CreateFounder("agent-b", "Rowan Reed")]),
            "household", "Home", ["agent-a", "agent-b"]).Checkpoint;
        public IReadOnlyDictionary<string, PlaytestInhabitantState> Inhabitants { get; } =
            new Dictionary<string, PlaytestInhabitantState>(StringComparer.Ordinal)
            {
                ["agent-a"] = new("agent-a", new(0, 0), 10_000, 0, "patient", "build a home"),
                ["agent-b"] = new("agent-b", new(1, 0), 10_000, 0, "kind", "share a life"),
            };
        public HashSet<string> Urgent { get; } = new(StringComparer.Ordinal);
        public List<(string Kind, string Detail)> Events { get; } = [];
        public bool IsEdibleFood(string kind) => false;
        public int AvailableLotQuantity(InventoryLot lot) => lot.Quantity;
        public bool NeedsUrgentWarmth(PlaytestInhabitantState person) => Urgent.Contains(person.InhabitantId);
        public void AppendEvent(string kind, string detail) => Events.Add((kind, detail));
    }
}
