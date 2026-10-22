using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using GodotSnapshot = ClankerWorld.Contracts.OwnerWorldSnapshot;

namespace ClankerWorld.Tests;

public sealed class TownCouncilProjectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task OwnerAndClientReceiveLatestEightProposalsWithoutChangingSavedHistory()
    {
        using var generated = NormalPathWorld.CreateGenerated("council-projection-proposal-history",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 12; tick++) await generated.AdvanceOneTickAsync();
        var initial = generated.ExportState();
        var town = initial.Towns![0];
        var adults = town.ResidentIds.ToArray();
        Assert.Equal(4, adults.Length);
        var day = initial.WorldSystems!.Config.TicksPerDay;
        var council = TownCouncilState.Create(adults);
        for (var index = 0; index < 12; index++)
        {
            council = TownCouncilRules.SubmitProposal(council, town.Id, adults[0], "law", null,
                $"Publish harvest report number {index + 1}.", "unchanged", adults, index, day);
            var proposalId = council.Proposals[^1].Id;
            var yes = index % 3;
            foreach (var voter in adults.Take(yes))
                council = TownCouncilRules.VoteProposal(council, proposalId, voter, true, index);
            if (index % 2 == 1)
                council = TownCouncilRules.VoteProposal(council, proposalId, adults[yes], false, index);
            council = TownCouncilRules.WithdrawProposal(council, proposalId, adults[0], index);
        }
        using var world = WorldRuntime.Restore(initial with
        {
            Towns = [town with { Council = council }],
        });
        world.Validate();
        var before = WorldRuntimeCodec.Encode(world.ExportState());

        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;

        var expected = council.Proposals.Skip(4).Select(proposal =>
            (proposal.Id, proposal.Kind, proposal.Text, proposal.Status,
                Yes: proposal.Votes.Count(vote => vote.Yes), No: proposal.Votes.Count(vote => !vote.Yes),
                proposal.RequiredYes, proposal.DeadlineTick)).ToArray();
        var projected = Assert.Single(snapshot.Towns).Council!.Proposals;
        var received = Assert.Single(client.Towns).Council!.Proposals;
        foreach (var proposals in new[] { projected, received })
        {
            Assert.Equal(8, proposals.Count);
            Assert.Equal(expected, proposals.Select(proposal =>
                (proposal.Id, proposal.Kind, proposal.Text, proposal.Status, proposal.Yes, proposal.No,
                    proposal.RequiredYes, proposal.DeadlineTick)));
        }
        Assert.Equal(12, world.ExportState().Towns![0].Council!.Proposals.Count);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task ActiveAndLatestArchivedElectionRemainSeparateAcrossOwnerClientJson(string archivedStage)
    {
        using var generated = NormalPathWorld.CreateGenerated("council-projection-election-history",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var ordinal = 5; ordinal <= 8; ordinal++)
        {
            var setup = generated.ExportState();
            var site = setup.Towns![0].BorderTiles.First(point => setup.Map.IsBuildable(point) &&
                !setup.Map.Resources.Any(resource => resource.Position == point) &&
                !setup.Map.MapObjects.Any(item => item.Position == point) &&
                !setup.Agents.Any(person => person.Position == point));
            generated.AddAgent($"agent:{ordinal:D32}", site);
        }
        var initial = generated.ExportState();
        const int day = 10;
        var oldDay = initial.Society.Society.Config.TicksPerWorldDay;
        var initialTown = initial.Towns![0];
        using var clock = WorldRuntime.Restore(initial with
        {
            // AddAgent can record an unsuccessful election at the original day
            // length. The shorter clock starts with its own civic history.
            Towns = [initialTown with { Council = TownCouncilState.Create(initialTown.ResidentIds) }],
            WorldSystems = RegionalWeatherRules.Initialize(initial.WorldSystems! with
            {
                Config = initial.WorldSystems.Config with { TicksPerDay = day, CalendarOffsetTicks = 0 },
                RegionalWeather = null,
            }, initial.Map),
            Society = initial.Society with
            {
                Society = initial.Society.Society with
                {
                    Config = initial.Society.Society.Config with { TicksPerWorldDay = day },
                    Agents = initial.Society.Society.Agents.Select(person => person with
                    {
                        BirthTick = person.BirthTick / oldDay * day,
                        BirthLifeTick = person.BirthLifeTick is { } birth ? birth / oldDay * day : null,
                    }).ToArray(),
                },
            },
        }, _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 22; tick++) await clock.AdvanceOneTickAsync();
        var checkpoint = clock.ExportState();
        var town = checkpoint.Towns![0];
        var adults = town.ResidentIds.ToArray();
        Assert.Equal(8, adults.Length);
        var council = TownCouncilState.Create(adults);
        foreach (var candidate in adults.Take(3))
            council = TownCouncilRules.Register(council, candidate, true, null, adults, 0);
        council = Advance(council, adults, 0);
        if (archivedStage == "completed")
            council = TownCouncilRules.VoteElection(council, council.Election!.Id,
                adults[0], adults.Take(3).ToArray(), 0);
        council = Advance(council, adults, day);
        if (archivedStage == "completed")
        {
            // The recorded demographic fallback ends representation, then a return
            // to eight adults opens a fresh initial election with the same register.
            council = Advance(council, adults.Take(3).ToArray(), 11);
            council = Advance(council, adults, 22);
        }
        else
        {
            council = Advance(council, adults, 20);
            if (archivedStage == "cancelled")
            {
                council = Advance(council, adults.Take(3).ToArray(), 21);
                council = Advance(council, adults, 22);
            }
        }
        council = TownCouncilRules.VoteElection(council, council.Election!.Id,
            adults[0], adults.Take(2).ToArray(), 22);
        council = TownCouncilRules.VoteElection(council, council.Election!.Id,
            adults[1], [adults[0]], 22);
        using var world = WorldRuntime.Restore(checkpoint with
        {
            Towns = [town with { Council = council }],
        });
        world.Validate();
        var before = WorldRuntimeCodec.Encode(world.ExportState());

        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<GodotSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;

        var archived = council.ElectionHistory[^1];
        var active = council.Election!;
        Assert.Equal(archivedStage, archived.Stage);
        Assert.NotEqual(active.Id, archived.Id);
        var projected = Assert.Single(snapshot.Towns).Council!;
        var received = Assert.Single(client.Towns).Council!;
        var people = checkpoint.Society.Society.Agents.ToDictionary(person => person.Id, person => person.Name);
        var activeCandidates = active.Candidates.Select(id => (id, people[id],
            Votes: active.Ballots.Count(ballot => ballot.Choices.Contains(id, StringComparer.Ordinal)))).ToArray();
        var archivedCandidates = archived.Candidates.Select(id => (id, people[id],
            Votes: archived.Ballots.Count(ballot => ballot.Choices.Contains(id, StringComparer.Ordinal)))).ToArray();
        foreach (var visible in new[] { projected, received })
        {
            Assert.NotNull(visible.Election);
            Assert.NotNull(visible.LatestElection);
            Assert.Equal((active.Id, active.Kind, active.Stage, active.Seats, active.DeadlineTick),
                (visible.Election.Id, visible.Election.Kind, visible.Election.Stage,
                    visible.Election.Seats, visible.Election.DeadlineTick));
            Assert.Equal((archived.Id, archived.Kind, archived.Stage, archived.Seats, archived.DeadlineTick),
                (visible.LatestElection.Id, visible.LatestElection.Kind, visible.LatestElection.Stage,
                    visible.LatestElection.Seats, visible.LatestElection.DeadlineTick));
            Assert.Equal(activeCandidates, visible.Election.Candidates.Select(candidate => (candidate.Id, candidate.Name, candidate.Votes)));
            Assert.Equal(archivedCandidates, visible.LatestElection.Candidates.Select(candidate => (candidate.Id, candidate.Name, candidate.Votes)));
            Assert.Equal(archived.SettledSeats.Select(id => people[id]), visible.LatestElection.SettledNames);
        }
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));

        TownCouncilState Advance(TownCouncilState state, string[] residents, long tick) =>
            TownCouncilRules.Advance(state, town.Id, checkpoint.WorldSeed, residents, tick, day);
    }
}
