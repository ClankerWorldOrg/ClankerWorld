using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownLandHearingRuntimeTests
{
    [Fact]
    public async Task FoundingMayorConflictedByTheirOwnPlotQueuesAnIndependentCaseElectionAcrossReplay()
    {
        var provider = new HearingProvider();
        using var world = NewWorld(provider);
        await UntilAsync(world, () => world.Towns[0].Government!.Offices.Count == 2 &&
            world.Towns[0].Government!.Offices.All(office => office.HolderId == Judge), Day * 3, provider);
        Assert.Equal(2, world.Towns[0].Government!.Offices.Count);
        Assert.All(world.Towns[0].Government!.Offices, office => Assert.Equal(Judge, office.HolderId));
        var household = world.Society.GetAgent(Judge).HouseholdId!;
        provider.Plot = world.ExportState().HouseholdLandUseRights!.Where(right => right.HouseholdId == household)
            .OrderBy(right => right.Id, StringComparer.Ordinal).First().Tiles.ToArray();
        provider.CaseFiler = Judge;
        provider.ActingJudge = Filer;
        provider.HearingsEnabled = true;
        Prompt(world, Judge, "Consider filing your own household's land disagreement.");
        await UntilAsync(world, () => world.Towns[0].LandHearings.Cases.Any(item => item.Contest is { Stage: "voting" }), Day, provider);
        var pending = Assert.Single(world.Towns[0].LandHearings.Cases);
        Assert.Null(pending.Judge);
        Assert.Contains(pending.JudgeConsents, consent => consent.AgentId == Filer);
        Assert.DoesNotContain(pending.Contest!.Candidates, id => id == Judge);
        world.Pause();
        var paused = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(paused, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(paused), _ =>
            new HearingProvider { Plot = provider.Plot, CaseFiler = Judge, ActingJudge = Filer, HearingsEnabled = true });
        world.Resume(); replay.Resume();
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var step = 0; step < Day * 2 && world.Towns[0].LandHearings.Cases[0].Judge is null; step++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        }
        var assigned = Assert.IsType<TownLandCaseJudge>(world.Towns[0].LandHearings.Cases[0].Judge);
        Assert.Equal((Filer, "case_elected"), (assigned.AgentId, assigned.Kind));
        Assert.All(world.Towns[0].Government!.Offices, office => Assert.Equal(Judge, office.HolderId));
        Assert.Empty(world.Towns[0].Government!.Changes);
        world.Validate(); replay.Validate();
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
    }
}
