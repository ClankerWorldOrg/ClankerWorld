using System.Text.Json;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CouncilHistoryTests
{
    [Fact]
    public void RepeatedCandidateLessAttemptsHaveABoundedCompactionPlan()
    {
        using var world = NormalPathWorld.CreateGenerated("council-history", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = WithFailedAttempts(world.ExportState());
        var plan = WorldHistory.Prepare(state);
        Assert.Equal(8, plan.State.Towns![0].Council!.ElectionHistory.Count);
        Assert.Equal(32, plan.State.Towns[0].Council!.Notices.Count);
        Assert.Equal(48, state.Towns![0].Council!.ElectionHistory.Count);
    }

    [Fact]
    public async Task ArchivedAttemptsKeepLearnedRelaysReloadAndReplay()
    {
        using var generated = NormalPathWorld.CreateGenerated("council-history-replay", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = WithFailedAttempts(generated.ExportState());
        var town = state.Towns![0];
        var council = town.Council!;
        var notice = council.Notices[0];
        council = TownCouncilRules.LearnNotice(council, town.ResidentIds[0], notice.Id, 0);
        council = TownCouncilRules.LearnNotice(council, town.ResidentIds[1], notice.Id, 0, town.ResidentIds[0]);
        state = state with { Towns = [town with { Council = council }] };
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        var directory = Directory.CreateTempSubdirectory("clanker-council-history-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => new ActionCoverageRecorder(chooseIdle: true));
            var visible = JsonSerializer.Serialize(new OwnerWorldObservationStore(world).GetSnapshot().Towns[0].Council);
            Assert.True(file.Save(world));
            var compacted = world.ExportState();
            var retained = compacted.Towns![0].Council!;
            Assert.Equal(8, retained.ElectionHistory.Count);
            Assert.Equal(33, retained.Notices.Count);
            Assert.Contains(notice, retained.Notices);
            Assert.Equal(council.Knowledge, retained.Knowledge);
            Assert.Equal(visible, JsonSerializer.Serialize(new OwnerWorldObservationStore(world).GetSnapshot().Towns[0].Council));
            var bytes = WorldRuntimeCodec.Encode(compacted);
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            using var loaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
            var next = TownCouncilRules.Register(retained, town.ResidentIds[0], true, null, town.ResidentIds, 0);
            Assert.Equal("notice:49", next.Notices[^1].Id);
            var head = Path.Combine(file.Path + ".history", compacted.HistoryArchiveHead + ".json");
            var segment = JsonSerializer.Deserialize<WorldHistorySegment>(File.ReadAllBytes(head))!;
            Assert.Equal(40, segment.Streams.Single(batch => batch.Name.StartsWith("council-elections:", StringComparison.Ordinal)).Events.GetArrayLength());
            Assert.Equal(15, segment.Streams.Single(batch => batch.Name.StartsWith("council-notices:", StringComparison.Ordinal)).Events.GetArrayLength());
            File.WriteAllText(head, "corrupt");
            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(state.WorldSeed));
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedWritesKeepAllAttemptsAndThePreviousCheckpoint(bool failCheckpoint)
    {
        using var generated = NormalPathWorld.CreateGenerated("council-history-failure", _ => new ActionCoverageRecorder(chooseIdle: true));
        using var world = WorldRuntime.Restore(WithFailedAttempts(generated.ExportState()));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var directory = Directory.CreateTempSubdirectory("clanker-council-write-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            File.WriteAllBytes(file.Path, bytes);
            if (failCheckpoint)
            {
                File.Move(file.Path, file.Path + ".previous");
                Directory.CreateDirectory(file.Path);
            }
            else File.WriteAllText(file.Path + ".history", "block archive creation");
            Assert.Throws<IOException>(() => file.Save(world));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(bytes, File.ReadAllBytes(failCheckpoint ? file.Path + ".previous" : file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void AForcedElectionRemainsAvailableBeyondTheRecentWindow()
    {
        using var generated = NormalPathWorld.CreateGenerated("council-history-forced", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = WithFailedAttempts(generated.ExportState());
        var town = state.Towns![0];
        var first = town.Council!.ElectionHistory[0];
        // Controlled dependency: the government validator requires this exact
        // original contest even after its handover has settled.
        var change = new TownGovernmentChange("change", "request", "arrangement", town.Government!.Arrangement,
            town.ResidentIds[0], "same", 0, "completed", 0, 360, town.ResidentIds, [], 0, 1080, 0)
            { ForcedCouncilElectionId = first.Id };
        state = state with { Towns = [town with { Government = town.Government with { Changes = [change] } }] };
        var plan = WorldHistory.Prepare(state);
        Assert.Contains(first, plan.State.Towns![0].Council!.ElectionHistory);
        Assert.Equal(9, plan.State.Towns[0].Council!.ElectionHistory.Count);
        Assert.Contains(town.Council.Notices[0], plan.State.Towns[0].Council!.Notices);
    }

    private static WorldRuntimeState WithFailedAttempts(WorldRuntimeState state)
    {
        var town = state.Towns![0];
        var council = TownCouncilState.Create(town.ResidentIds);
        for (var attempt = 0; attempt < 48; attempt++)
        {
            // Mature the retry in this controlled ledger so retention needs no
            // multi-season survival simulation. Every outcome uses the native rule.
            council = TownCouncilRules.Advance(council with { RetryTick = 0 }, town.Id,
                state.WorldSeed, town.ResidentIds, 0, state.WorldSystems!.Config.TicksPerDay);
        }
        return state with { Towns = [town with { Council = council }] };
    }
}
