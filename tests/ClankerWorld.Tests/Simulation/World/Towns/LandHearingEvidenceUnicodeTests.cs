using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class LandHearingEvidenceUnicodeTests
{
    private static readonly Lazy<Task<byte[]>> Baseline = new(CreateBaseline);
    private const string Judge = NonLandRuntimeFixture.Judge;
    private const string Source = NonLandRuntimeFixture.Subject;

    [Theory]
    [InlineData(158)]
    [InlineData(159)]
    [InlineData(160)]
    public async Task NativeRulingChoicesKeepWholeCharactersAndInspectedSourceHistory(int prefix)
    {
        var state = WorldRuntimeCodec.Decode(await Baseline.Value);
        var text = new string('a', prefix) + "😀tail";
        var statement = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == Source
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|hearing_statement|", StringComparison.Ordinal)) : null,
            LandPayload = (_, candidate) => candidate.Id.Contains("|hearing_statement|", StringComparison.Ordinal) ? new(Statement: text) : null,
        };
        using var world = NonLandRuntimeFixture.Create(state, statement);
        NonLandRuntimeFixture.Wake(world, Source, "unicode-source-statement");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].LandRecords.Cases[0].Evidence.Any(evidence => evidence.Kind == "allegation"), 6);
        var evidence = Assert.Single(world.Towns[0].LandRecords.Cases[0].Evidence, item => item.Kind == "allegation");
        Assert.Equal(text, evidence.Text);
        Assert.Equal(Source, evidence.SourceAgentId);
        Assert.Equal(Source, evidence.SubmittedByAgentId);
        Assert.Equal("statement", evidence.Acquisition);
        var inspect = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == Judge
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|hearing_inspect|", StringComparison.Ordinal)) : null,
        };
        using var inspected = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(world.ExportState()), inspect);
        NonLandRuntimeFixture.Wake(inspected, Judge, "unicode-judge-inspection");
        await NonLandRuntimeFixture.UntilAsync(inspected, () => inspected.Towns[0].LandRecords.Cases[0].Reads.Any(read =>
            read.AgentId == Judge && read.EvidenceIds.Contains(evidence.Id)), 6);
        var idle = new NonLandTestProvider();
        using var captured = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(inspected.ExportState()), idle);
        NonLandRuntimeFixture.Wake(captured, Judge, "unicode-ruling-context");
        await NonLandRuntimeFixture.UntilAsync(captured, () => idle.Observations.Any(observation => observation.AgentId == Judge &&
            observation.Candidates.Any(candidate => candidate.Id.Contains("|hearing_rule|", StringComparison.Ordinal))), 6);
        var candidate = idle.Observations.Where(observation => observation.AgentId == Judge)
            .SelectMany(observation => observation.Candidates).First(candidate => candidate.Id.Contains("|hearing_rule|", StringComparison.Ordinal));
        var marker = evidence.Id + "=allegation from " + Source + ": ";
        var start = candidate.Description.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, candidate.Description);
        start += marker.Length;
        var end = candidate.Description.IndexOf("; inspected law versions:", start, StringComparison.Ordinal);
        Assert.True(end > start, candidate.Description);
        var excerpt = candidate.Description[start..end];
        var expected = prefix == 158 ? new string('a', 158) + "😀" : new string('a', Math.Min(prefix, 160));
        Assert.Equal(expected, excerpt);
        var final = captured.ExportState();
        Assert.Equal(JsonSerializer.Serialize(state.TownLandTitles), JsonSerializer.Serialize(final.TownLandTitles));
        Assert.Equal(JsonSerializer.Serialize(state.HouseholdLandUseRights), JsonSerializer.Serialize(final.HouseholdLandUseRights));
        Assert.Equal(state.Society.Society.Inventory.Lots.Select(lot =>
                (lot.Id, lot.OwnerId, lot.Quantity, lot.StorageBuildingId, lot.CarrierId)),
            final.Society.Society.Inventory.Lots.Select(lot =>
                (lot.Id, lot.OwnerId, lot.Quantity, lot.StorageBuildingId, lot.CarrierId)));
        Assert.Equal("pending", captured.Towns[0].LandRecords.Cases[0].Status);
        Assert.Empty(captured.Towns[0].LandRecords.Cases[0].Rulings);
        Assert.Equal(evidence, captured.Towns[0].LandRecords.Cases[0].Evidence.Single(item => item.Id == evidence.Id));
        captured.Validate();
        var bytes = WorldRuntimeCodec.Encode(captured.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var wire = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(excerpt))!;

        Assert.Equal(excerpt, wire);
        _ = new UTF8Encoding(false, true).GetBytes(excerpt);
    }

    private static async Task<byte[]> CreateBaseline()
    {
        var state = NonLandRuntimeFixture.Prepared();
        var town = state.Towns![0];
        var tick = state.Society.Society.WorldTick;
        var (council, government) = TownGovernmentRules.RegisterMayor(town.Council!, town.Government!, Judge, "land+non_land", town.ResidentIds, tick);
        (council, government) = TownGovernmentRules.Advance(council, government, town.Id, town.Name,
            state.WorldSeed, town.ResidentIds, tick, NonLandRuntimeFixture.Day);
        var contest = Assert.IsType<TownMayoralContest>(government.Contest);
        foreach (var voter in town.ResidentIds)
            government = TownGovernmentRules.VoteMayor(government, TownGovernmentRules.RoundToken(contest), voter, Judge, tick);
        state = state with { Towns = [town with { Council = council, Government = government }] };
        using (var clock = NonLandRuntimeFixture.Create(state, new NonLandTestProvider()))
        {
            await NonLandRuntimeFixture.UntilAsync(clock, () => clock.Towns[0].Government!.Offices.Any(office => office.HolderId == Judge && office.Mandates == "land"), NonLandRuntimeFixture.Day + 3);
            state = clock.ExportState();
        }
        var household = state.Society.Society.GetAgent(Source).HouseholdId;
        var permission = state.HouseholdLandUseRights!.First(right => right.HouseholdId == household);
        state = state with
        {
            HouseholdLandUseRights = state.HouseholdLandUseRights!.Select(right => right.Id == permission.Id ? right with { AgreedEndTick = state.Society.Society.WorldTick + 1 } : right).ToArray(),
            Agents = state.Agents.Select(person => person with
            {
                Position = state.Towns![0].OriginSite!.Value,
                FullnessBasisPoints = 10_000,
                Survival = person.Survival is { } survival ? survival with
                { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000, IllnessBasisPoints = 0 } : null,
                LastDecisionContext = null,
                Project = null,
                TravelCooldownTicks = 0,
            }).ToArray(),
        };
        using (var expiry = NonLandRuntimeFixture.Create(state, new NonLandTestProvider()))
        {
            Assert.True((await expiry.AdvanceOneTickAsync()).Advanced);
            Assert.Single(expiry.Towns[0].LandRecords.Cases);
            state = expiry.ExportState();
        }
        town = state.Towns![0];
        var item = Assert.Single(town.LandRecords.Cases);
        var revision = TownLandHearingRules.CurrentRevision(item);
        council = TownCouncilRules.LearnNotices(town.Council!, Judge, [revision.NoticeId], state.Society.Society.WorldTick);
        var hearings = town.LandRecords;
        foreach (var party in revision.Parties)
            foreach (var adult in party.AdultIds)
            {
                council = TownCouncilRules.LearnNotices(council, adult, [revision.NoticeId], state.Society.Society.WorldTick);
                hearings = TownLandHearingRules.Respond(hearings, item.Id, revision.Number, adult, "waive", "I waive only my own response opportunity.",
                    state.Society.Society.WorldTick, council.Knowledge, revision.Parties);
            }
        var inspect = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == Judge
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|hearing_inspect|", StringComparison.Ordinal)) : null,
        };
        using var prepared = NonLandRuntimeFixture.Create(state with { Towns = [town with { Council = council, LandRecords = hearings }] }, inspect);
        NonLandRuntimeFixture.Wake(prepared, Judge, "inspect-original-records");
        await NonLandRuntimeFixture.UntilAsync(prepared, () => prepared.Towns[0].LandRecords.Cases[0].Reads.Any(read => read.AgentId == Judge), 6);
        Assert.Equal("pending", prepared.Towns[0].LandRecords.Cases[0].Status);
        return WorldRuntimeCodec.Encode(NonLandRuntimeFixture.Strict(prepared.ExportState()));
    }
}
