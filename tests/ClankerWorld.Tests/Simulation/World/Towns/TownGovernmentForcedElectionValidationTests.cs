using System.Text;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class TownGovernmentHandoverTests
{
    private static readonly TownArrangement RepresentativeOnly =
        new(TownArrangementRules.ElectedCouncil, TownArrangementRules.NoOffice);

    [Fact]
    public void AForcedElectionReferenceCannotOutliveItsElectionRecord()
    {
        var town = ForcedCouncilHandover().RoundTrip();
        var change = town.Government.Changes[^1];
        var election = Assert.IsType<TownElection>(town.Council.Election);
        Assert.Equal(election.Id, change.ForcedCouncilElectionId);

        town.Council = town.Council with { Election = null };
        town.Government = WithForcedChanges(town.Government, change with { ForcedCouncilElectionId = null });
        town.Validate(); // The missing election alone is legal; claiming it is not.
        town.Government = WithForcedChanges(town.Government, change);

        Assert.Throws<InvalidDataException>(() => town.RoundTrip());
    }

    [Fact]
    public void AForcedElectionReferenceCannotPointIntoAnotherTownsNativeHistory()
    {
        var town = ForcedCouncilHandover().RoundTrip();
        var other = ForcedCouncilHandover("town:other").RoundTrip();
        var foreignElection = Assert.IsType<TownElection>(other.Council.Election);
        var change = town.Government.Changes[^1];
        Assert.NotEqual(change.ForcedCouncilElectionId, foreignElection.Id);
        town.Government = WithForcedChanges(town.Government, change with { ForcedCouncilElectionId = foreignElection.Id });

        Assert.Throws<InvalidDataException>(() => town.RoundTrip());
    }

    [Fact]
    public void AHandoverCannotClaimAnOrdinaryElectionOpenedBeforeItsApproval()
    {
        var town = new Town("a", "b", "c", "d", "e", "f", "g", "h");
        town.RegisterCouncil("a", "b", "c");
        town.Advance(0);
        var ordinary = Assert.IsType<TownElection>(town.Council.Election);
        town.Advance(1);
        town.Yes(town.Propose(RepresentativeOnly), "a", "b", "c", "d", "e");
        town = town.RoundTrip();
        var change = town.Government.Changes[^1];
        Assert.Equal("handover", change.Status);
        Assert.Null(change.ForcedCouncilElectionId);
        Assert.True(ordinary.OpenedTick < change.ApprovedTick);
        town.Government = WithForcedChanges(town.Government, change with { ForcedCouncilElectionId = ordinary.Id });

        Assert.Throws<InvalidDataException>(() => town.RoundTrip());
    }

    [Fact]
    public void ACompletedArrangementCannotClaimAGenuineRegularCouncilElection()
    {
        var town = new Town("a", "b", "c", "d", "e", "f", "g", "h");
        town.RegisterCouncil("a", "b", "c");
        town.Advance(0);
        town.VoteEverything(0);
        town.Advance(Day);
        var termEnd = town.Council.TermEndTick!.Value;
        town.Advance(termEnd - Day);
        var regular = Assert.IsType<TownElection>(town.Council.Election);
        Assert.Equal("regular", regular.Kind);
        town.Yes(town.Propose(RepresentativeOnly), "a", "b", "c", "d", "e");
        town.VoteEverything(town.Tick);
        town.Advance(termEnd);
        town = town.RoundTrip();
        var change = town.Government.Changes[^1];
        Assert.Equal("completed", change.Status);
        Assert.Null(change.ForcedCouncilElectionId);
        Assert.Equal(regular.OpenedTick, change.ApprovedTick);
        Assert.Equal(regular.OpenedTick, change.SettledTick);
        Assert.Contains(town.Council.ElectionHistory,
            election => election.Id == regular.Id && election.Stage == "completed");
        town.Government = WithForcedChanges(town.Government, change with { ForcedCouncilElectionId = regular.Id });

        Assert.Throws<InvalidDataException>(() => town.RoundTrip());
    }

    [Fact]
    public void TwoApprovedHandoversCannotOwnTheSameFailedElection()
    {
        var town = ConsecutiveCouncilHandovers(registerSecondCandidates: false).RoundTrip();
        var first = town.Government.Changes[^2];
        var second = town.Government.Changes[^1];
        var failed = Assert.Single(town.Council.ElectionHistory,
            election => election.Id == first.ForcedCouncilElectionId);
        Assert.Equal("failed", failed.Stage);
        Assert.Equal(second.ApprovedTick, failed.OpenedTick);
        Assert.Null(second.ForcedCouncilElectionId);
        var claimingSecond = second with { ForcedCouncilElectionId = failed.Id };
        town.Government = WithForcedChanges(town.Government, first with { ForcedCouncilElectionId = null }, claimingSecond);
        town.Validate(); // Each reference is otherwise valid on its own.
        town.Government = WithForcedChanges(town.Government, first, claimingSecond);

        Assert.Throws<InvalidDataException>(() => town.RoundTrip());
    }

    [Fact]
    public void ACancelledHandoverCannotOwnALaterLiveElection()
    {
        var town = ConsecutiveCouncilHandovers(registerSecondCandidates: true).RoundTrip();
        var first = town.Government.Changes[^2];
        var second = town.Government.Changes[^1];
        var live = Assert.IsType<TownElection>(town.Council.Election);
        Assert.Equal(first.SettledTick, live.OpenedTick);
        Assert.Equal(live.Id, second.ForcedCouncilElectionId);
        town.Government = WithForcedChanges(town.Government,
            first with { ForcedCouncilElectionId = null }, second with { ForcedCouncilElectionId = null });
        town.Validate(); // The live election and both native changes remain structurally valid.
        town.Government = WithForcedChanges(town.Government,
            first with { ForcedCouncilElectionId = live.Id }, second with { ForcedCouncilElectionId = null });

        Assert.Throws<InvalidDataException>(() => town.RoundTrip());
    }

    [Fact]
    public void PrivateSavesRequireExplicitForcedElectionProvenanceAndPreserveRefusedBytes()
    {
        using var world = new WorldRuntime("forced-council-save", startPace: WorldStartPace.FounderSetup);
        world.PlaceFounder("founder:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", new GridPoint(0, 0));
        world.PlaceFounder("founder:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", new GridPoint(1, 2));
        world.PlaceFounder("founder:cccccccccccccccccccccccccccccccc", new GridPoint(2, 2));
        world.PlaceFounder("founder:dddddddddddddddddddddddddddddddd", new GridPoint(3, 2));
        world.StartWorld(resume: false);
        var initial = world.ExportState();
        Assert.Equal(0, initial.Society.Society.WorldTick);
        Assert.Equal(4, Assert.Single(initial.Towns!).ResidentIds.Count);
        var forced = WithNativeGovernmentHandover(initial, ElectedMayor, initiallyAllAdult: true);
        var ordinary = WithNativeGovernmentHandover(initial,
            new(TownArrangementRules.Council, TownArrangementRules.Mayor));
        var directory = Directory.CreateTempSubdirectory("forced-council-provenance-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            var file = new WorldStateFile(path);
            foreach (var state in new[] { ordinary, forced })
            {
                var expected = Assert.Single(state.Towns!).Government!.Changes[^1].ForcedCouncilElectionId;
                using var current = WorldRuntime.Restore(state);
                file.Save(current);
                var bytes = File.ReadAllBytes(path);
                var change = SavedGovernmentChange(JsonNode.Parse(bytes)!);
                Assert.True(change.TryGetPropertyValue("forcedCouncilElectionId", out var marker));
                Assert.Equal(expected, marker?.GetValue<string>());
                using var restored = file.LoadOrCreate(state.WorldSeed);
                Assert.Equal(expected, Assert.Single(restored.Towns).Government!.Changes[^1].ForcedCouncilElectionId);
                Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            Assert.Null(Assert.Single(ordinary.Towns!).Government!.Changes[^1].ForcedCouncilElectionId);
            var forcedTown = Assert.Single(forced.Towns!);
            Assert.Equal(Assert.IsType<TownElection>(forcedTown.Council!.Election).Id,
                forcedTown.Government!.Changes[^1].ForcedCouncilElectionId);

            var currentBytes = WorldRuntimeCodec.Encode(forced);
            var missing = JsonNode.Parse(currentBytes)!;
            Assert.True(SavedGovernmentChange(missing).Remove("forcedCouncilElectionId"));
            AssertRefusedFile(Encoding.UTF8.GetBytes(missing.ToJsonString()));

            var older = JsonNode.Parse(currentBytes)!;
            older["state"]!["schemaVersion"] = WorldRuntime.StateSchemaVersion - 1;
            Assert.NotNull(SavedGovernmentChange(older)["forcedCouncilElectionId"]);
            var error = AssertRefusedFile(Encoding.UTF8.GetBytes(older.ToJsonString()));
            Assert.Contains($"minimum supported schema {WorldRuntime.StateSchemaVersion}", error.Message,
                StringComparison.Ordinal);

            InvalidDataException AssertRefusedFile(byte[] refused)
            {
                Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(refused));
                File.WriteAllBytes(path, refused);
                var exception = Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(initial.WorldSeed));
                Assert.Equal(refused, File.ReadAllBytes(path));
                return exception;
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private static Town ForcedCouncilHandover(string id = "town:test", bool registerCandidates = true)
    {
        var town = AllAdultTown(id);
        if (registerCandidates) town.RegisterCouncil("a", "b", "c");
        town.Yes(town.Propose(RepresentativeOnly), "a", "b", "c");
        Assert.Equal("handover", town.Government.Changes[^1].Status);
        return town;
    }

    private static Town ConsecutiveCouncilHandovers(bool registerSecondCandidates)
    {
        var town = ForcedCouncilHandover(registerCandidates: false);
        town.Advance(TownArrangementRules.HandoverDays * Day);
        Assert.Equal("cancelled", town.Government.Changes[^1].Status);
        if (registerSecondCandidates) town.RegisterCouncil("a", "b", "c");
        town.Yes(town.Propose(RepresentativeOnly), "a", "b", "c");
        Assert.Equal(4, town.Government.Changes.Count);
        Assert.Equal("handover", town.Government.Changes[^1].Status);
        return town;
    }

    // Keep both resident-approved office removal and all-adult arrangement while damaging only the subsequent handovers.
    private static TownGovernmentState WithForcedChanges(TownGovernmentState state, params TownGovernmentChange[] changes) =>
        state with { Changes = [.. state.Changes.Take(state.Changes.Count - changes.Length), .. changes] };

    private static WorldRuntimeState WithNativeGovernmentHandover(WorldRuntimeState state, TownArrangement target,
        bool initiallyAllAdult = false)
    {
        var town = Assert.Single(state.Towns!);
        var adults = town.ResidentIds;
        var day = state.WorldSystems!.Config.TicksPerDay;
        var council = town.Council!;
        var startingGovernment = town.Government!;
        if (initiallyAllAdult)
        {
            (council, startingGovernment) = TownGovernmentRules.Propose(council, startingGovernment, town.Id,
                adults[0], AllAdult, false, adults, 0, day);
            foreach (var voter in adults.Take(3))
                startingGovernment = TownGovernmentRules.Vote(startingGovernment, startingGovernment.Changes[^1].Id, voter, true, 0);
            (council, startingGovernment) = TownGovernmentRules.Advance(council, startingGovernment, town.Id, town.Name,
                state.WorldSeed, adults, 0, day);
            Assert.Equal(AllAdult, startingGovernment.Arrangement);
        }
        foreach (var actor in adults.Take(3))
            council = TownCouncilRules.Register(council, actor, true, null, adults, 0);
        var (proposedCouncil, government) = TownGovernmentRules.Propose(council, startingGovernment, town.Id,
            adults[0], target, false, adults, 0, day);
        var change = government.Changes[^1];
        foreach (var voter in adults.Take(3)) government = TownGovernmentRules.Vote(government, change.Id, voter, true, 0);
        (council, government) = TownGovernmentRules.Advance(proposedCouncil, government, town.Id, town.Name,
            state.WorldSeed, adults, 0, day);
        return state with { Towns = [town with { Council = council, Government = government }] };
    }

    private static JsonObject SavedGovernmentChange(JsonNode document) =>
        document["state"]!["towns"]![0]!["government"]!["changes"]!.AsArray()[^1]!.AsObject();
}
