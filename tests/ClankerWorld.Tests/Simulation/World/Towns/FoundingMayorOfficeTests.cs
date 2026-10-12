using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using GodotSnapshot = ClankerWorld.GodotClient.Protocol.OwnerWorldSnapshot;

namespace ClankerWorld.Tests;

public sealed partial class TownGovernmentRuntimeTests
{
    private static readonly string[] FoundingMayorMandates = ["land", "non_land"];
    private static readonly JsonSerializerOptions FoundingSnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void FoundingJointElectionWaitsForTheScheduledCouncilElection()
    {
        string[] adults = ["a", "b", "c", "d", "e", "f", "g", "h"];
        var council = TownCouncilState.Create(adults);
        var government = TownGovernmentState.Create();
        foreach (var actor in adults.Take(3))
            council = TownCouncilRules.Register(council, actor, true, null, adults, 0);
        (council, government) = TownGovernmentRules.RegisterMayor(council, government, "a", "land+non_land", adults, 0);
        (council, government) = TownGovernmentRules.Advance(council, government, "town:queue", "Queue Town", "seed", adults, 0, Day);
        var councilElection = Assert.IsType<TownElection>(council.Election);
        Assert.Equal("main", councilElection.Stage);
        Assert.Equal("waiting", government.Contest!.Stage);
        Assert.Null(government.Contest.RoundDeadlineTick);
        council = TownCouncilRules.VoteElection(council, councilElection.Id, "a", adults.Take(3).ToArray(), 0);
        (council, government) = TownGovernmentRules.Advance(council, government, "town:queue", "Queue Town", "seed", adults, Day, Day);
        Assert.Equal("representative", council.Form);
        var mayorElection = Assert.IsType<TownMayoralContest>(government.Contest);
        Assert.Equal(("voting", "land+non_land"), (mayorElection.Stage, mayorElection.Mandates));
        Assert.All(government.Offices, office => Assert.Null(office.HolderId));
        government = TownGovernmentRules.VoteMayor(government, TownGovernmentRules.RoundToken(mayorElection), "a", "a", Day);
        (council, government) = TownGovernmentRules.Advance(council, government, "town:queue", "Queue Town", "seed", adults, Day * 2, Day);
        Assert.All(government.Offices, office => Assert.Equal("a", office.HolderId));
        Assert.Empty(government.Changes);
    }

    [Fact]
    public void NativeFoundingStartsWithVacantLandAndNonLandMayoralDuties()
    {
        using var world = NormalPathWorld.CreateGenerated("founding-mayor-office", _ => new RecordingProvider());
        var town = Assert.Single(world.Towns);
        var government = Assert.IsType<TownGovernmentState>(town.Government);
        Assert.Equal(new TownArrangement(TownArrangementRules.Council, TownArrangementRules.Mayor,
            TownArrangementRules.Mayor), government.Arrangement);
        Assert.Equal(FoundingMayorMandates, government.Offices.Select(office => office.Mandates));
        Assert.All(government.Offices, office =>
        {
            Assert.Null(office.HolderId);
            Assert.Null(office.ElectionId);
            Assert.Null(office.TermStartTick);
            Assert.Null(office.TermEndTick);
            Assert.Equal(town.FoundedTick, office.VacantSinceTick);
        });
        Assert.Empty(government.Changes);
        Assert.Empty(government.Consents);
        Assert.Null(government.Contest);
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    [Fact]
    public async Task FirstNativeMayorElectionNeedsOnlyPersonalJointConsentAndResidentBallots()
    {
        var provider = new FirstMayorProvider();
        using var world = NewWorld(_ => provider);
        Assert.Empty(world.Towns[0].Government!.Consents);
        for (var step = 0; step < 12 && world.Towns[0].Government!.Contest is not { Stage: "voting" }; step++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var election = Assert.IsType<TownMayoralContest>(world.Towns[0].Government!.Contest);
        Assert.Equal("land+non_land", election.Mandates);
        Assert.Equal("vacancy", election.Purpose);
        Assert.Equal("land+non_land", Assert.Single(world.Towns[0].Government!.Consents).Mandates);
        Assert.Empty(world.Towns[0].Government!.Changes);
        Assert.All(provider.Offered(Author).Where(id => id.Contains("|mayor_register|", StringComparison.Ordinal)),
            id => Assert.Contains("|mayor_register|land+non_land|", id, StringComparison.Ordinal));
        world.Pause();
        var paused = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(paused, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(paused), _ => new FirstMayorProvider());
        Assert.Equal(paused, WorldRuntimeCodec.Encode(replay.ExportState()));
        world.Resume(); replay.Resume();
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var step = 0; step < Day * 2 && world.Towns[0].Government!.Offices.Any(office => office.HolderId is null); step++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            world.Validate(); replay.Validate();
        }
        var government = world.Towns[0].Government!;
        Assert.Equal(FoundingMayorMandates, government.Offices.Select(office => office.Mandates));
        Assert.All(government.Offices, office =>
        {
            Assert.Equal(Author, office.HolderId);
            Assert.Equal(election.Id, office.ElectionId);
            Assert.Equal(office.TermStartTick + TownArrangementRules.MayorTermDays * Day, office.TermEndTick);
        });
        Assert.Single(government.Offices.Select(office => office.TermStartTick).Distinct());
        Assert.Empty(government.Changes);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "town_civic_mayor");
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, FoundingSnapshotJsonOptions),
            FoundingSnapshotJsonOptions)!;
        var visible = Assert.Single(client.Towns).Government!;
        Assert.Equal(2, visible.Offices.Count);
        Assert.All(visible.Offices, office => Assert.NotNull(office.HolderName));
        Assert.True(visible.NonLandAuthorized);
        Assert.NotNull(visible.NonLandAuthority);
        Assert.Equal("completed", visible.LatestElection!.Stage);
    }

    private sealed class FirstMayorProvider : IDecisionProvider
    {
        private readonly RecordingProvider recorder = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public string[] Offered(string id) => recorder.Offered(id);
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            await recorder.DecideAsync(request, cancellationToken);
            var observation = request.Observation;
            var choice = observation.Candidates.FirstOrDefault(item => item.Id.Contains("|read|", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(item => item.Id.Contains("|mayor_vote|", StringComparison.Ordinal) &&
                    item.Id.EndsWith("|" + Author, StringComparison.Ordinal));
            if (choice is null && observation.AgentId == Author)
                choice = observation.Candidates.FirstOrDefault(item => item.Id.Contains("|mayor_register|land+non_land|", StringComparison.Ordinal));
            choice ??= observation.Candidates.Single(item => item.Id == "safe_idle");
            return new(request.RequestId, observation.AgentId, Kind, ProviderEpoch, observation.RunEpoch,
                observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice.Id ? 1d : 0d, StringComparer.Ordinal));
        }
    }
}
