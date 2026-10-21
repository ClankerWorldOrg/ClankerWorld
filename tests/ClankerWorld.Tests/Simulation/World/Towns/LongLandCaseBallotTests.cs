using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownLandHearingRuntimeTests
{
    [Fact]
    public async Task AFoundedTownsLongCaseBallotVotesForItsExactRoundAcrossReloadAndReplay()
    {
        var provider = new HearingProvider();
        using var world = await NewFoundedBallotTownAsync(provider);
        var townId = world.Towns.Single(town => town.Id != TownBorderRules.FirstTownId).Id;
        TownRuntimeState Town() => world.Towns.Single(town => town.Id == townId);
        await UntilAsync(world, () => Town().Government!.Offices.All(office => office.HolderId == Judge), Day * 3, provider);
        provider.Plot = world.HouseholdLandUseRights.Single(right => right.TownId == townId).Tiles.ToArray();
        provider.CaseFiler = Judge;
        provider.ActingJudge = Filer;
        provider.HearingsEnabled = true;
        Prompt(world, Judge, "Consider filing the disagreement on your household's plot.");
        await UntilAsync(world, () => Town().LandRecords.Cases.Any(item => item.Contest is { Stage: "voting" }), Day, provider);
        var item = Assert.Single(Town().LandRecords.Cases);
        var contest = Assert.IsType<TownLandCaseJudgeContest>(item.Contest);
        var revision = TownLandHearingRules.RevisionToken(item);
        var round = TownLandCaseJudgeRules.RoundToken(contest);
        Assert.True((revision + "#" + round).Length > DerivedId.MaximumLength);
        Assert.Contains(Judge, contest.Voters);
        Assert.Contains(Filer, contest.Candidates);
        Assert.DoesNotContain(Judge, contest.Candidates);
        var property = PrivateProperty(world.ExportState());
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var replayProvider = new HearingProvider
        { Plot = provider.Plot, CaseFiler = Judge, ActingJudge = Filer, HearingsEnabled = true };
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => replayProvider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        world.Resume(); replay.Resume();
        Prompt(world, Judge, "Read the election notice and choose an independent case judge.");
        Prompt(replay, Judge, "Read the election notice and choose an independent case judge.");
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        LandHearingJudgeActionPlan? ballot = null;
        for (var step = 0; step < 12 && ballot is null; step++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            foreach (var decision in result.Decisions.Where(decision => decision.AgentId == Judge &&
                         decision.ActionPlan is LandHearingJudgeActionPlan { Choice.Kind: LandHearingJudgeKind.Vote }))
            {
                Assert.True(decision.Admission.Accepted);
                Assert.False(decision.Admission.FellBack);
                Assert.Null(decision.PlanRefusal);
                ballot = Assert.IsType<LandHearingJudgeActionPlan>(decision.ActionPlan);
            }
        }
        Assert.NotNull(ballot);
        Assert.Equal(item.Id, ballot.CaseId);
        Assert.Equal(Filer, ballot.Candidate);
        Assert.Equal(round, TownLandCaseJudgeRules.RoundToken(ballot.Contest!));
        Assert.Equal(contest.RoundDeadlineTick, ballot.Contest!.RoundDeadlineTick);
        Assert.Equal(ballot.Choice, LandHearingJudgeChoice.Decode(ballot.CandidateId));
        Assert.InRange(ballot.Choice.RevisionOrRoundToken.Length, 1, DerivedId.MaximumLength);
        var voted = Assert.IsType<TownLandCaseJudgeContest>(Town().LandRecords.Cases.Single(file => file.Id == item.Id).Contest);
        Assert.Equal(round, TownLandCaseJudgeRules.RoundToken(voted));
        Assert.Equal(Filer, Assert.Single(voted.Ballots, vote => vote.AgentId == Judge).CandidateId);
        Assert.Equal(property, PrivateProperty(world.ExportState()));
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final));
        Assert.Equal(final, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.False(world.CheckActionPlan(ballot with
        { Choice = ballot.Choice with { RevisionOrRoundToken = revision } }).Allowed);
        Assert.Equal(final, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static async Task<WorldRuntime> NewFoundedBallotTownAsync(HearingProvider provider)
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path", _ => new ActionCoverageRecorder(chooseIdle: true), ticksPerDay: Day);
        var state = generated.ExportState();
        var claimed = state.TownLandTitles!.SelectMany(title => title.Tiles).ToHashSet();
        var occupied = state.Map.Resources.Select(resource => resource.Position)
            .Concat(state.Map.MapObjects.Select(item => item.Position)).ToHashSet();
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            TownBorderRules.Around(state.Map, [point]).All(tile => !claimed.Contains(tile) && !occupied.Contains(tile)));
        var founder = new ScriptedProvider();
        var founding = founder.Hold();
        using var placement = WorldRuntime.Restore(state with
        { Agents = state.Agents.Select(person => person.AgentId == Judge ? person with { Position = site, LastDecisionContext = null } : person).ToArray() },
            actor => actor == Judge ? founder : new ActionCoverageRecorder(chooseIdle: true));
        var harness = ModelWorkHarness.For(placement);
        await harness.TickAsync();
        Assert.True(founding.IsEntered);
        founding.Reply(Assert.Single(founding.Request.Observation.Candidates,
            candidate => candidate.Id.StartsWith("civic|town:site:", StringComparison.Ordinal) && candidate.Id.Contains("|found|", StringComparison.Ordinal)).Id);
        await harness.TickUntilAdmittedAsync(Judge, 4);
        placement.Pause();
        state = placement.ExportState();
        var town = Assert.Single(state.Towns!, town => town.Id != TownBorderRules.FirstTownId);
        var residents = state.Agents.Select(person => person.AgentId).Order(StringComparer.Ordinal).ToArray();
        var household = state.Society.Society.GetAgent(Judge).HouseholdId!;
        // Controlled membership, comfort and one titled household plot isolate the
        // ballot boundary. The Town, case and election IDs are generated natively.
        state = state with
        {
            Towns = state.Towns!.Select(record => record with
            {
                ResidentIds = record.Id == town.Id ? residents : [],
                Council = TownCouncilState.Create(record.Id == town.Id ? residents : []),
            }).ToArray(),
            HouseholdLandUseRights = state.HouseholdLandUseRights!.Concat(TownLandRightsRules.InitialUseRights(
                state.Map, town.Id, [(household, new[] { site })], placement.WorldTick)).OrderBy(right => right.Id, StringComparer.Ordinal).ToArray(),
            Agents = state.Agents.Select(person => person with
            { Position = site, FullnessBasisPoints = 9_000, LastDecisionContext = null }).ToArray(),
        };
        var result = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        result.Resume();
        return result;
    }
}
