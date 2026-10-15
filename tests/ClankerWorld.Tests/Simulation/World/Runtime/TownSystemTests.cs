using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class TownSystemTests
{
    [Fact]
    public void MajorityAdoptsPolicyWithoutChangingTheDiscardedBallotSnapshot()
    {
        var world = new FakeWorld();
        var system = new TownSystem(CivicState.Empty with { FoodStewardship = new("agent-a", "open", 0) });
        system.ApplyFoodPolicyCandidate(world, "agent-a", "food_policy_propose:essential_first");
        var proposed = system.State;
        var discarded = new TownSystem(proposed);
        world.WorldTick = 301;
        system.ApplyFoodPolicyCandidate(world, "agent-b", "food_policy_vote_yes");
        Assert.Equal("essential_first", system.State.FoodStewardship!.FoodPolicy);
        Assert.Null(system.State.FoodStewardship.Ballot);
        Assert.Equal(301, system.State.FoodStewardship.LastResolutionTick);
        Assert.Same(proposed, discarded.State);
        Assert.Equal("open", discarded.State.FoodStewardship!.FoodPolicy);
        Assert.Equal("agent-a", Assert.Single(discarded.State.FoodStewardship.Ballot!.Approvals));
        Assert.Collection(world.Events,
            item => Assert.Equal(("food_policy_proposed", "essential_first"), item),
            item => Assert.Equal(("food_policy_vote_recorded", "agent-b"), item),
            item => Assert.Equal(("food_policy_adopted", "essential_first"), item));
    }

    [Fact]
    public void ExpiryAndUrgentWarmthUseTheCurrentWorld()
    {
        var world = new FakeWorld();
        var system = new TownSystem(CivicState.Empty with { FoodStewardship = new("agent-a", "open", 0) });
        world.Urgent.Add("agent-a");
        Assert.False(system.HasFoodPolicyDecision(world, "agent-a"));
        world.Urgent.Clear();
        Assert.True(system.HasFoodPolicyDecision(world, "agent-a"));
        system.ApplyFoodPolicyCandidate(world, "agent-a", "food_policy_propose:essential_first");
        world.WorldTick = 420;
        system.ResolveFoodPolicyBallot(world);
        Assert.NotNull(system.State.FoodStewardship!.Ballot);
        world.WorldTick = 421;
        system.ResolveFoodPolicyBallot(world);
        Assert.Null(system.State.FoodStewardship.Ballot);
        Assert.Equal("open", system.State.FoodStewardship.FoodPolicy);
        Assert.Equal(("food_policy_rejected", "essential_first"), world.Events[^1]);
    }

    [Fact]
    public void TownAndCivicWritesReplaceOnlyTheirOwnedSnapshot()
    {
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], [new(0, 0)])
        { Council = TownCouncilState.Create(["agent-a"]) };
        var system = new TownSystem(CivicState.Empty);
        system.RestoreTowns([town]);
        var original = system.State;
        var members = town with { ResidentIds = ["agent-a", "agent-b"] };
        system.SetTownState(members);
        Assert.NotSame(original, system.State);
        Assert.Same(members.ResidentIds, system.State.Towns[0].ResidentIds);
        Assert.Same(town.ResidentIds, original.Towns[0].ResidentIds);
        var proposal = new TownSystem(original);
        var revised = members with { Council = town.Council with { Revision = 1 } };
        proposal.SetTownState(revised);
        Assert.Equal(0, original.Towns[0].Council!.Revision);
        Assert.Equal(1, proposal.State.Towns[0].Council!.Revision);
        var checkpoint = Assert.Single(proposal.State.Towns);
        Assert.Same(members.ResidentIds, checkpoint.ResidentIds);
        Assert.Same(members.BorderTiles, checkpoint.BorderTiles);
        Assert.Same(revised.Council, checkpoint.Council);
    }

    [Fact]
    public void AbandonmentAndRevivalLeaveTheDiscardedTownAndLandRecordsAlone()
    {
        var world = new FakeWorld();
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], [new(0, 0)]);
        var committed = new TownSystem(CivicState.Empty);
        committed.RestoreTowns([town]);
        committed.RestoreTownLandTitles([new("title", "town", [new(0, 0)], 0)]);
        committed.RestoreHouseholdLandUseRights([new("right", "town", "household", [new(0, 0)], 0, "initial")]);
        var original = committed.State;
        var proposed = new TownSystem(original);

        proposed.SetTown(world, town with { ResidentIds = [] });
        var abandoned = Assert.Single(proposed.State.Towns);
        Assert.Equal(300, abandoned.AbandonedSinceTick);
        Assert.Equal(("town_abandoned", "town"), Assert.Single(world.Events));
        world.WorldTick = 400;
        proposed.SetTown(world, abandoned with { Name = "Empty Town" });
        Assert.Equal(300, Assert.Single(proposed.State.Towns).AbandonedSinceTick);
        Assert.Single(world.Events);
        proposed.SetTown(world, abandoned with { ResidentIds = ["agent-b"] });
        Assert.Null(Assert.Single(proposed.State.Towns).AbandonedSinceTick);
        Assert.Equal(("town_revived", "town"), world.Events[^1]);
        Assert.Same(original.TownLandTitles, proposed.State.TownLandTitles);
        Assert.Same(original.HouseholdLandUseRights, proposed.State.HouseholdLandUseRights);
        Assert.Same(original, committed.State);
        Assert.Same(town, Assert.Single(committed.State.Towns));
        var beforeRefusal = proposed.State;
        Assert.Throws<InvalidOperationException>(() => proposed.SetTown(world, town with { Id = "missing" }));
        Assert.Same(beforeRefusal, proposed.State);
        Assert.Equal(2, world.Events.Count);
    }

    [Fact]
    public void LandConsentAndWithdrawalReplaceOnlyTheProposedRequest()
    {
        var world = new FakeWorld();
        var source = new List<HouseholdLandUseRequest>
        {
            new("request", "town", "household", "agent-a", [new(0, 0)], 0),
        };
        var committed = new TownSystem(CivicState.Empty);
        committed.RestoreHouseholdLandUseRequests(source);
        var original = committed.State;
        source.Clear();
        var proposed = new TownSystem(original);
        var council = TownCouncilState.Create(["agent-a", "agent-b"]);

        Assert.Same(council, proposed.DecideHouseholdLandUse(world, council, "agent-b", "request", "accept_land_use"));
        world.WorldTick = 301;
        proposed.DecideHouseholdLandUse(world, council, "agent-a", "request", "decline_land_use");
        Assert.Collection(Assert.Single(proposed.State.HouseholdLandUseRequests).Consents,
            consent => Assert.Equal(new("agent-a", false, 301), consent),
            consent => Assert.Equal(new("agent-b", true, 300), consent));
        var beforeRefusal = proposed.State;
        Assert.Throws<InvalidOperationException>(() => proposed.DecideHouseholdLandUse(world, council, "agent-a", "request", "accept_land_use"));
        Assert.Throws<InvalidOperationException>(() => proposed.DecideHouseholdLandUse(world, council, "outsider", "request", "accept_land_use"));
        Assert.Same(beforeRefusal, proposed.State);
        world.WorldTick = 302;
        var withdrawn = proposed.DecideHouseholdLandUse(world, council, "agent-a", "request", "withdraw_land_use");
        Assert.Equal("withdrawn", Assert.Single(proposed.State.HouseholdLandUseRequests).Status);
        Assert.Equal(302, Assert.Single(proposed.State.HouseholdLandUseRequests).SettledTick);
        Assert.Single(withdrawn.Notices);
        Assert.Empty(proposed.State.HouseholdLandUseRights);
        Assert.Same(original, committed.State);
        Assert.Equal("pending", Assert.Single(original.HouseholdLandUseRequests).Status);
        Assert.Empty(Assert.Single(original.HouseholdLandUseRequests).Consents);
        Assert.Empty(council.Notices);
    }

    [Fact]
    public void PreparationOrdersNewRecordsWithoutChangingTheLiveActionOrder()
    {
        var system = new TownSystem(CivicState.Empty);
        system.RestoreTowns([new("z", "First", "founded", 0, [], [], [new(0, 0)]), new("a", "Second", "founded", 0, [], [], [new(1, 0)])]);
        system.RestoreTownLandTitles([new("z", "z", [new(0, 0)], 0), new("a", "a", [new(1, 0)], 0)]);
        system.RestoreHouseholdLandUseRights([new("z", "z", "household", [new(0, 0)], 0, "initial"), new("a", "a", "household", [new(1, 0)], 0, "initial")]);
        system.RestoreHouseholdLandUseRequests([new("z", "z", "household", "agent-a", [new(0, 0)], 0), new("a", "a", "household", "agent-a", [new(1, 0)], 0)]);
        var live = system.State;
        var canonicalOrder = new[] { "a", "z" };
        var actionOrder = new[] { "z", "a" };

        var proposal = new TownSystem(system.CheckpointState);

        Assert.Equal(canonicalOrder, proposal.State.Towns.Select(town => town.Id));
        Assert.Equal(canonicalOrder, proposal.State.TownLandTitles.Select(title => title.Id));
        Assert.Equal(canonicalOrder, proposal.State.HouseholdLandUseRights.Select(right => right.Id));
        Assert.Equal(canonicalOrder, proposal.State.HouseholdLandUseRequests.Select(request => request.Id));
        Assert.Same(proposal.State, proposal.CheckpointState);
        Assert.Same(live, system.State);
        Assert.Equal(actionOrder, system.State.Towns.Select(town => town.Id));
        Assert.Equal(actionOrder, system.State.TownLandTitles.Select(title => title.Id));
        Assert.Equal(actionOrder, system.State.HouseholdLandUseRights.Select(right => right.Id));
        Assert.Equal(actionOrder, system.State.HouseholdLandUseRequests.Select(request => request.Id));
    }

    [Fact]
    public void CivicHistoryIsSharedBySnapshotAndInvalidatedByAReplacement()
    {
        var council = TownCouncilState.Create(["agent-a"]) with
        {
            Notices = [new("notice", "result", "proposal", "Adopted", 10)],
            Knowledge = [new("agent-a", "notice", 11)],
        };
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], [new(0, 0)]) { Council = council };
        var history = TownSystem.CivicHistory(town);
        Assert.Same(history, TownSystem.CivicHistory(town with { Name = "Renamed" }));
        Assert.True(history.Knows("agent-a", "result", "proposal"));
        Assert.Equal("notice", Assert.Single(history.Recent("agent-a")).NoticeId);
        var replacement = TownSystem.CivicHistory(town with { Council = council with { Knowledge = [] } });
        Assert.NotSame(history, replacement);
        Assert.False(replacement.Knows("agent-a", "proposal"));
        Assert.True(history.Knows("agent-a", "proposal"));
    }

    [Fact]
    public void NoticeContextUsesKnownReceiptsAndTheCurrentNamesAndCalendar()
    {
        var world = new FakeWorld();
        var council = TownCouncilState.Create(["agent-a"]) with
        {
            Notices = [new("known", "result", "proposal", "agent-b voted at tick 10", 10),
                new("unread", "result", "other", "Unread notice", 12)],
            Knowledge = [new("agent-a", "known", 11, "agent-b")],
        };
        world.Towns = [new("town", "Town", "founded", 0, ["agent-a"], [], [new(0, 0)]) { Council = council }];
        var day = WorldCalendarRules.FromTick(10, world.CalendarConfig).DayIndex + 1;
        Assert.Equal($"Heard from Rowan Reed: Town: Rowan Reed voted at world day {day}", TownSystem.CivicNote(world, "agent-a"));
        Assert.Null(TownSystem.CivicNote(world, "agent-b"));
    }

    [Fact]
    public void HistoryVerificationDetectsChangedNoticeContentBehindTheSameSnapshot()
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var notices = new List<TownCivicNotice> { new("notice", "result", "proposal", "Original", 10) };
        var council = TownCouncilState.Create(["agent-a"]) with { Notices = notices };
        var town = new TownRuntimeState("town", "Town", "founded", 0, ["agent-a"], [], [new(0, 0)]) { Council = council };
        TownSystem.CivicHistory(town);
        notices[0] = notices[0] with { Text = "Changed" };
        Assert.Throws<InvalidOperationException>(() => TownSystem.CivicHistory(town));
    }

    private sealed class FakeWorld : ICivicHistoryWorld, ICivicLandConsentWorld
    {
        public IReadOnlyList<TownRuntimeState> Towns { get; set; } = [];
        public WorldSystemsConfig CalendarConfig { get; } = new();
        public long WorldTick { get; set; } = 300;
        public bool HasSurvivalState => true;
        public SocietyCheckpoint Society { get; } = SocietyRules.CreateHousehold(
            SocietyRules.CreateGenesis("civic-system-test",
                [SocietyRules.CreateFounder("agent-a", "Aster Ash"), SocietyRules.CreateFounder("agent-b", "Rowan Reed")]),
            "household", "Home", ["agent-a", "agent-b"]).Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents { get; } =
            new Dictionary<string, AgentState>(StringComparer.Ordinal)
            {
                ["agent-a"] = new("agent-a", new(0, 0), 10_000, 0, "patient", "build a home"),
                ["agent-b"] = new("agent-b", new(1, 0), 10_000, 0, "kind", "share a life"),
            };
        public HashSet<string> Urgent { get; } = new(StringComparer.Ordinal);
        public List<(string Kind, string Detail)> Events { get; } = [];
        public ActorCapabilityState? CapabilitiesFor(string actor) => Agents.ContainsKey(actor)
            ? new(Society.GetAgent(actor).AgeBand, false, false, false, Urgent.Contains(actor), false) : null;
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => throw new InvalidOperationException(refusal.Explanation);
        public bool IsEdibleFood(string kind) => false;
        public int AvailableLotQuantity(InventoryLot lot) => lot.Quantity;
        public bool NeedsUrgentWarmth(AgentState person) => Urgent.Contains(person.AgentId);
        public void AppendEvent(string kind, string detail) => Events.Add((kind, detail));
        public string[] HouseholdAdults(string householdId) => Society.Agents
            .Where(person => person.HouseholdId == householdId && person.Status == SocietyAgentStatus.Active &&
                person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
    }
}
