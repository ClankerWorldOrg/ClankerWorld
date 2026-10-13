using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.Logging.Abstractions;

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
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(plan.State));
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
            var forged = compacted with
            {
                Towns = [compacted.Towns[0] with
                { Council = retained with { ArchivedElectionCount = retained.ArchivedElectionCount + 1 } }]
            };
            var forgedBytes = WorldRuntimeCodec.Encode(forged);
            File.WriteAllBytes(file.Path, forgedBytes);
            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(state.WorldSeed));
            Assert.Equal(forgedBytes, File.ReadAllBytes(file.Path));
            File.WriteAllBytes(file.Path, bytes);
            var renewedCouncil = TownCouncilRules.Advance(retained with { RetryTick = 0 }, town.Id,
                state.WorldSeed, town.ResidentIds, 0, state.WorldSystems!.Config.TicksPerDay);
            using var renewed = WorldRuntime.Restore(compacted with { Towns = [town with { Council = renewedCouncil }] });
            Assert.True(file.Save(renewed));
            Assert.Equal(41, renewed.ExportState().Towns![0].Council!.ArchivedElectionCount);
            Assert.Equal(8, renewed.ExportState().Towns![0].Council!.ElectionHistory.Count);
            using var extended = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(renewed.ExportState()), WorldRuntimeCodec.Encode(extended.ExportState()));
            File.WriteAllBytes(file.Path, bytes);
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

    [Theory]
    [InlineData("streams")]
    [InlineData("entry")]
    [InlineData("name")]
    public void MalformedArchiveStreamsEnterRecoveryWithoutChangingSavedFiles(string damage)
    {
        using var generated = NormalPathWorld.CreateGenerated("council-malformed-history", _ => new ActionCoverageRecorder(chooseIdle: true));
        using var world = WorldRuntime.Restore(WithFailedAttempts(generated.ExportState()));
        var directory = Directory.CreateTempSubdirectory("clanker-council-malformed-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            Assert.True(file.Save(world));
            var saved = world.ExportState();
            var segment = JsonNode.Parse(File.ReadAllBytes(Path.Combine(file.Path + ".history", saved.HistoryArchiveHead + ".json")))!;
            if (damage == "streams") segment[nameof(WorldHistorySegment.Streams)] = null;
            else if (damage == "entry") segment[nameof(WorldHistorySegment.Streams)]!.AsArray()[0] = null;
            else segment[nameof(WorldHistorySegment.Streams)]!.AsArray()[0]![nameof(ArchivedEventBatch.Name)] = null;
            var archiveBytes = JsonSerializer.SerializeToUtf8Bytes(segment);
            var digest = Convert.ToHexStringLower(SHA256.HashData(archiveBytes));
            var archivePath = Path.Combine(file.Path + ".history", digest + ".json");
            File.WriteAllBytes(archivePath, archiveBytes);
            var damaged = saved with { HistoryArchiveHead = digest };
            var checkpointBytes = WorldRuntimeCodec.Encode(damaged);
            File.WriteAllBytes(file.Path, checkpointBytes);

            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(saved.WorldSeed));
            Assert.Throws<InvalidDataException>(() => file.VerifyRequiredHistory(damaged));
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));
            var recovery = new WorldStartupRecovery(file, new ManualWorldSaveStore(file.Path), providers,
                _ => new ActionCoverageRecorder(chooseIdle: true), saved.WorldSeed, Path.Combine(directory.FullName, "authority.json"),
                NullLogger<WorldStartupRecovery>.Instance);
            Assert.True(recovery.Pending);
            Assert.Equal(world.Society.WorldId, recovery.Capture().WorldId);
            Assert.Equal(checkpointBytes, File.ReadAllBytes(file.Path));
            Assert.Equal(archiveBytes, File.ReadAllBytes(archivePath));
        }
        finally { directory.Delete(recursive: true); }
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
