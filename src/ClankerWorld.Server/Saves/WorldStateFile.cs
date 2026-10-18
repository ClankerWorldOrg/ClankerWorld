using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClankerWorld.Server.Saves;

internal sealed record CheckpointRestorePoint(string PreservedPath, byte[] CheckpointBytes, byte[]? BuildBytes);

/// <summary>
/// Atomic persistence for the integrated world alpha runtime. The
/// provider factory is supplied by the host and credentials never enter the
/// checkpoint bytes.
/// </summary>
public sealed class WorldStateFile
{
    private readonly object gate = new();
    private readonly Func<string, IDecisionProvider>? providerFactory;
    private readonly WorldStartPace newWorldPace;
    private readonly GeographyOptions? newWorldGeography;
    private readonly bool allowDifferentSavedSeed;
    private bool buildRecorded;
    private sealed record PreparedCheckpoint(WorldRuntimeState State, byte[] Bytes,
        string? HistoryDigest, byte[]? HistoryBytes);

    public WorldStateFile(string path, Func<string, IDecisionProvider>? providerFactory = null,
        WorldStartPace newWorldPace = WorldStartPace.Legacy, GeographyOptions? newWorldGeography = null,
        bool allowDifferentSavedSeed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        this.providerFactory = providerFactory;
        this.newWorldPace = newWorldPace;
        this.newWorldGeography = newWorldGeography;
        this.allowDifferentSavedSeed = allowDifferentSavedSeed;
    }

    public string Path { get; }

    public WorldRuntime LoadOrCreate(string worldSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        lock (gate)
        {
            if (!File.Exists(Path))
            {
                var created = new WorldRuntime(worldSeed, providerFactory, startPace: newWorldPace,
                    geographyOptions: newWorldGeography);
                SaveUnsafe(created.ExportState());
                return created;
            }

            var state = WorldRuntimeCodec.Decode(SavedFile.Read(Path, SavedFile.CheckpointLimit));
            VerifyHistory(state.HistoryArchiveHead, state.BoatTransport.RetiredRequestRanges, WorldHistory.RequiredRecordCounts(state));
            if (!allowDifferentSavedSeed && !string.Equals(state.WorldSeed, worldSeed, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The world save belongs to a different configured seed.");
            }

            var restored = WorldRuntime.Restore(state, providerFactory);
            SaveUnsafe(restored.ExportState());
            return restored;
        }
    }

    public bool Save(WorldRuntime runtime, bool resumeOnSuccess = false)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var compacted = false;
        runtime.PersistCheckpoint(state =>
        {
            lock (gate)
            {
                var saved = SaveUnsafe(state, compactHistory: true);
                compacted = saved.HistoryArchiveHead != state.HistoryArchiveHead;
                return saved;
            }
        }, resumeOnSuccess);
        return compacted;
    }

    public void RemoveQuarantinedContent(WorldRuntime runtime, string packageId) =>
        runtime.RemoveQuarantinedContent(packageId, state =>
        {
            lock (gate) _ = SaveUnsafe(state);
        });

    public WorldDeveloperEditResult ApplyMapEdit(WorldRuntime runtime, WorldMapEdit edit)
        => runtime.ApplyMapEdit(edit, state =>
        {
            lock (gate) _ = SaveUnsafe(state);
        });

    public WorldDeveloperEditResult ApplyDeveloperEdit(WorldRuntime runtime, WorldDeveloperEdit edit)
    {
        return runtime.ApplyDeveloperEdit(edit, state =>
        {
            lock (gate) _ = SaveUnsafe(state);
        });
    }

    /// <summary>Validate an isolated proposal without writing; publish its exact bytes only after admission.</summary>
    public Action PrepareSave(WorldRuntime runtime)
        => PrepareSave(runtime, out _);

    public Action PrepareSave(WorldRuntime runtime, out bool compacted)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var state = runtime.ExportState();
        var prepared = PrepareCheckpoint(state, compactHistory: true);
        compacted = prepared.State.HistoryArchiveHead != state.HistoryArchiveHead;
        return () =>
        {
            lock (gate) _ = WritePreparedCheckpoint(prepared);
            if (prepared.State.HistoryArchiveHead != state.HistoryArchiveHead)
                runtime.PersistCheckpoint(_ => prepared.State);
        };
    }

    private static PreparedCheckpoint PrepareCheckpoint(WorldRuntimeState state, bool compactHistory)
    {
        byte[]? historyBytes = null;
        string? historyDigest = null;
        if (compactHistory)
        {
            var plan = WorldHistory.Prepare(state);
            if (plan.Segment.Streams.Count > 0 || plan.Segment.ClosedBoatRequests is { Count: > 0 })
            {
                historyBytes = JsonSerializer.SerializeToUtf8Bytes(plan.Segment);
                historyDigest = Convert.ToHexStringLower(SHA256.HashData(historyBytes));
                state = plan.State with { HistoryArchiveHead = historyDigest };
            }
        }
        var checkpointBytes = WorldRuntimeCodec.Encode(state);
        SavedFile.CheckLength(checkpointBytes.LongLength, SavedFile.CheckpointLimit);
        if (historyBytes is not null) SavedFile.CheckLength(historyBytes.LongLength, SavedFile.HistoryLimit);
        // A valid in-memory checkpoint can still serialize into invalid source
        // evidence. Check the exact bytes before replacing the last good file.
        _ = WorldRuntimeCodec.Decode(checkpointBytes);
        return new(state, checkpointBytes, historyDigest, historyBytes);
    }

    private WorldRuntimeState WritePreparedCheckpoint(PreparedCheckpoint prepared)
    {
        if (prepared.HistoryDigest is { } digest)
            WriteHistorySegment(digest, prepared.HistoryBytes!);
        WriteCheckpointBytes(Path, prepared.Bytes);
        // Once per host run: every later checkpoint comes from the same build.
        if (!buildRecorded) buildRecorded = SavedBuild.TryWrite(Path);
        return prepared.State;
    }

    private WorldRuntimeState SaveUnsafe(WorldRuntimeState state, bool compactHistory = false) =>
        WritePreparedCheckpoint(PrepareCheckpoint(state, compactHistory));

    internal CheckpointRestorePoint PreserveDamagedCheckpoint(byte[] expectedBytes)
    {
        lock (gate)
        {
            if (!SavedFile.Read(Path, SavedFile.CheckpointLimit).AsSpan().SequenceEqual(expectedBytes))
                throw new InvalidDataException("The latest checkpoint changed. Restart before recovering it.");
            var preserved = Path + ".damaged." + Guid.NewGuid().ToString("N") + ".json";
            var buildBytes = SavedBuild.TryReadBytes(Path);
            WriteCheckpointBytes(preserved, expectedBytes, overwrite: false);
            return new(preserved, expectedBytes.ToArray(), buildBytes);
        }
    }

    internal void RestorePreservedCheckpoint(CheckpointRestorePoint restore)
    {
        lock (gate)
        {
            WriteCheckpointBytes(Path, restore.CheckpointBytes);
            _ = SavedBuild.TryRestore(Path, restore.BuildBytes);
            buildRecorded = false;
        }
    }

    private static void WriteCheckpointBytes(string destination, byte[] checkpointBytes, bool overwrite = true)
    {
        var directory = System.IO.Path.GetDirectoryName(destination) ??
            throw new InvalidOperationException("The world state path has no directory.");
        PrivateStorage.EnsureDirectory(directory);
        var temporaryPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = PrivateStorage.CreateNew(temporaryPath))
            {
                stream.Write(checkpointBytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, destination, overwrite);
            PrivateStorage.RestrictFile(destination);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string HistoryPath(string digest)
    {
        if (digest.Length != 64 || digest.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidDataException("The world history archive reference is invalid.");
        }
        return System.IO.Path.Combine(Path + ".history", digest + ".json");
    }

    private void WriteHistorySegment(string digest, byte[] bytes)
    {
        var destination = HistoryPath(digest);
        PrivateStorage.EnsureDirectory(Path + ".history");
        if (File.Exists(destination))
        {
            if (!SavedFile.Read(destination, SavedFile.HistoryLimit).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException("A world history segment failed content verification.");
            }
            return;
        }
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = PrivateStorage.CreateNew(temporary))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            PrivateStorage.RestrictFile(temporary);
            File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public void VerifyRequiredHistory(WorldRuntimeState checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        VerifyRequiredHistory(checkpoint.HistoryArchiveHead, checkpoint.BoatTransport.RetiredRequestRanges, WorldHistory.RequiredRecordCounts(checkpoint));
    }

    internal void VerifyRequiredHistory(string? historyArchiveHead, IReadOnlyList<RetiredBoatRequestRange> retiredRequests,
        IReadOnlyDictionary<string, long>? recordCounts = null)
    {
        // No archive means no disk work. Preserve the no-history path so
        // selection can prepare its transaction before checkpoint publication.
        if (historyArchiveHead is null && retiredRequests.Count == 0 && (recordCounts?.Count ?? 0) == 0) return;
        lock (gate) VerifyHistory(historyArchiveHead, retiredRequests, recordCounts);
    }

    /// <summary>Call while holding the installation world mutation gate.</summary>
    public void ReclaimUnreferencedHistory()
    {
        lock (gate)
        {
            var directory = Path + ".history";
            if (!Directory.Exists(directory)) return;
            var roots = new List<string> { Path };
            roots.AddRange(Directory.GetFiles(System.IO.Path.GetDirectoryName(Path)!,
                System.IO.Path.GetFileName(Path) + ".damaged.*.json"));
            foreach (var suffix in new[] { ".manual", ".worlds" })
                if (Directory.Exists(Path + suffix))
                    roots.AddRange(Directory.GetFiles(Path + suffix, "*.save"));
            var retained = new HashSet<string>(StringComparer.Ordinal);
            // Verify every root and chain before deleting anything. Unpublished
            // checkpoints are conservative roots too; corrupt roots fail closed.
            foreach (var root in roots)
            {
                var checkpoint = WorldRuntimeCodec.Decode(SavedFile.Read(root, SavedFile.CheckpointLimit));
                VerifyHistory(checkpoint.HistoryArchiveHead, checkpoint.BoatTransport.RetiredRequestRanges, WorldHistory.RequiredRecordCounts(checkpoint));
                var head = checkpoint.HistoryArchiveHead;
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (head is not null)
                {
                    if (!visited.Add(head)) throw new InvalidDataException("The history archive contains a cycle.");
                    var bytes = SavedFile.Read(HistoryPath(head), SavedFile.HistoryLimit);
                    if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != head)
                        throw new InvalidDataException("The history archive is corrupt.");
                    retained.Add(head);
                    head = (JsonSerializer.Deserialize<WorldHistorySegment>(bytes)
                        ?? throw new InvalidDataException("The history archive is empty.")).Parent;
                }
            }
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var digest = System.IO.Path.GetFileNameWithoutExtension(file);
                if (digest.Length == 64 && digest.All(char.IsAsciiHexDigit) && !retained.Contains(digest))
                    File.Delete(file);
            }
        }
    }

    private void VerifyHistory(string? head, IReadOnlyList<RetiredBoatRequestRange> retiredRequests,
        IReadOnlyDictionary<string, long>? recordCounts = null)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new SortedSet<long>();
        var remaining = recordCounts?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        while (head is not null)
        {
            if (!visited.Add(head))
            {
                throw new InvalidDataException("The world history archive contains a cycle.");
            }
            var bytes = SavedFile.Read(HistoryPath(head), SavedFile.HistoryLimit);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != head)
            {
                throw new InvalidDataException("The world history archive is corrupt.");
            }
            var segment = JsonSerializer.Deserialize<WorldHistorySegment>(bytes)
                ?? throw new InvalidDataException("The world history archive is empty.");
            if (segment.Streams is null ||
                segment.Streams.Any(batch => batch is null || string.IsNullOrWhiteSpace(batch.Name)))
                throw new InvalidDataException("The world history archive contains invalid streams.");
            foreach (var batch in segment.Streams.Where(batch => WorldHistory.IsRecordStream(batch.Name)))
            {
                if (batch.Events.ValueKind != JsonValueKind.Array || batch.Events.GetArrayLength() == 0 ||
                    batch.PreviousFloor < 0 || batch.ThroughEventId <= batch.PreviousFloor ||
                    batch.ThroughEventId - batch.PreviousFloor != batch.Events.GetArrayLength() ||
                    remaining is not null && (!remaining.TryGetValue(batch.Name, out var through) || through != batch.ThroughEventId))
                    throw new InvalidDataException("Retired records do not match their reachable history archive.");
                if (remaining is not null) remaining[batch.Name] = batch.PreviousFloor;
            }
            foreach (var request in segment.ClosedBoatRequests ?? [])
            {
                if (request is null || request.Sequence < 1 ||
                    request.Id != "boat-request:" + request.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    request.Status is not ("arrived" or "returned" or "cancelled") ||
                    request.SettledTick is not { } settled || settled < request.RequestedTick ||
                    !sequences.Add(request.Sequence))
                    throw new InvalidDataException("The boat history archive contains an invalid or duplicate closed request.");
            }
            head = segment.Parent;
        }
        if (remaining is not null && remaining.Values.Any(count => count != 0))
            throw new InvalidDataException("Retired records are missing from the reachable history archive.");
        // Compare ranges without expanding a potentially damaged high-water
        // mark. Only records reachable from this checkpoint count as authority.
        using var archived = sequences.GetEnumerator();
        foreach (var range in retiredRequests)
        {
            var expected = range.FirstSequence;
            while (true)
            {
                if (!archived.MoveNext() || archived.Current != expected)
                    throw new InvalidDataException("Retired boat requests do not match their reachable history archive.");
                if (expected == range.LastSequence) break;
                expected++;
            }
        }
        if (archived.MoveNext())
            throw new InvalidDataException("The history archive contains boat requests that this checkpoint has not retired.");
    }
}
