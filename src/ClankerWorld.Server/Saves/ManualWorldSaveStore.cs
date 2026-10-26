using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Saves;

/// <remarks>
/// <see cref="GameVersion"/> and <see cref="SourceRevision"/> name the host build
/// that wrote the save; saves from before they were recorded have neither.
/// </remarks>



/// <summary>
/// One version of a world's history. Playing on from an older save starts a new
/// branch, so later saves of the original branch stay where they were. Saves
/// made before branches existed have no branch.
/// </summary>


/// <summary>The branch record to put back if loading a save fails part-way.</summary>
public sealed record SaveTimelineRestorePoint(string WorldId, byte[]? Bytes);

/// <summary>
/// Where the running world's history continues: the save it was last loaded from
/// or saved as, that save's branch, and whether the next save starts a new branch.
/// </summary>


/// <summary>
/// Owner-only named checkpoints for the currently active world. Opaque IDs,
/// atomic writes, and private files keep names out of paths and credentials
/// out of world saves. History segments remain alongside the active save.
/// </summary>
public sealed partial class ManualWorldSaveStore
{
    private sealed record Metadata(ManualWorldSave Save, IReadOnlyList<AgentProviderAssignment> Assignments,
        WorldAutosaveSettings? AutosaveSettings, string? WorldId = null, string? Generation = null,
        [property: JsonIgnore] bool HasInvalidBranchMetadata = false, RecoveryProvenance? Recovery = null);
    private sealed record BranchMetadata(SaveBranch? Branch = null, string? ContinuedFromId = null,
        DateTimeOffset? ContinuedFromCreatedUtc = null, long BranchPosition = 0);
    // Where the running world's history continues from: its branch (null when the
    // next save must start one) and the last save it was loaded from or saved as.
    private sealed record Timeline(string WorldId, SaveBranch? Branch, string? ContinuedFromId,
        string? ContinuedFromName, long ContinuedFromTick, int LastBranchNumber,
        DateTimeOffset? ContinuedFromCreatedUtc = null, long ContinuedFromBranchPosition = 0,
        string? LoadedStateFingerprint = null, string? SavedCheckpointFingerprint = null);
    private const string LegacyBranchKey = "";
    private readonly object gate;
    private readonly string directory;
    private readonly WorldStateFile stateFile;
    private readonly ILogger<ManualWorldSaveStore>? logger;
    private readonly HashSet<string> reportedInvalidMetadata = new(StringComparer.Ordinal);

    public ManualWorldSaveStore(string activeSavePath, ILogger<ManualWorldSaveStore>? logger = null, object? mutationGate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeSavePath);
        directory = Path.GetFullPath(activeSavePath) + ".manual";
        stateFile = new WorldStateFile(activeSavePath);
        this.logger = logger;
        gate = mutationGate ?? new object();
    }

    public static string NormalizeName(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 80 || normalized.Any(char.IsControl))
            throw new ArgumentException("Save name must be 1–80 printable characters.", nameof(name));
        return normalized;
    }

    public ManualWorldSave Create(string name, WorldRuntime runtime,
        IReadOnlyList<AgentProviderAssignment> assignments, WorldAutosaveSettings? autosaveSettings = null)
        => CreateCore(name, runtime, assignments, autosaveSettings, isAutosave: false);

    public ManualWorldSave CreateAutosave(WorldRuntime runtime,
        IReadOnlyList<AgentProviderAssignment> assignments, WorldAutosaveSettings autosaveSettings)
        => CreateCore("Autosave", runtime, assignments, autosaveSettings, isAutosave: true);

    /// <summary>Replace one selected named checkpoint, preserving its previous bytes in a new recovery checkpoint.</summary>
    public ManualSaveOverwriteReceipt Overwrite(string id, WorldRuntime runtime,
        IReadOnlyList<AgentProviderAssignment> assignments, WorldAutosaveSettings? autosaveSettings = null)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(assignments);
        lock (gate)
        {
            RequireNoPendingLoad();
            var state = runtime.ExportState();
            if (!state.Society.Society.IsPaused)
                throw new InvalidOperationException("Pause the world before overwriting a manual save.");
            var bytes = EncodeReadableCheckpoint(state);
            if (!File.Exists(MetadataPath(id)))
                throw new FileNotFoundException("The selected manual save no longer exists.");
            var previousMetadata = ReadMetadata(id);
            if (previousMetadata.Save.Id != id || previousMetadata.Save.IsAutosave ||
                previousMetadata.WorldId != state.Society.Society.WorldId)
                throw new InvalidOperationException("Only a named save from this world can be overwritten.");
            var previousPath = CommittedStatePath(id, previousMetadata);
            var previousBytes = SavedFile.Read(previousPath, SavedFile.CheckpointLimit);
            var oldCheckpoint = WorldRuntimeCodec.Decode(previousBytes);
            if (oldCheckpoint.Society.Society.WorldId != previousMetadata.WorldId)
                throw new InvalidDataException("The selected checkpoint does not match its metadata.");
            var worldId = state.Society.Society.WorldId;
            var timeline = ReadTimeline(worldId);

            var backupName = "Before overwriting: " + previousMetadata.Save.Name;
            if (backupName.Length > 80)
                backupName = backupName[..StringInfo.ParseCombiningCharacters(backupName).Last(start => start <= 80)];
            var backup = previousMetadata.Save with
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = backupName,
                CreatedUtc = DateTimeOffset.UtcNow
            };
            WriteAtomic(StatePath(backup.Id), previousBytes);
            WriteAtomic(MetadataPath(backup.Id), JsonSerializer.SerializeToUtf8Bytes(
                previousMetadata with { Save = backup, Generation = null, Recovery = NextRecovery(id) }));

            // The backup keeps the old version in its original branch. The chosen
            // slot now holds the running world, so it joins the running world's branch.
            // Decide against the actual loaded version before its ID is rebound
            // to the recovery copy. Positions survive deleted intermediate saves.
            var branch = ResolveBranch(timeline, List(worldId));
            if (timeline?.ContinuedFromId == id)
            {
                timeline = timeline with
                {
                    ContinuedFromId = backup.Id,
                    ContinuedFromName = backup.Name,
                    ContinuedFromCreatedUtc = backup.CreatedUtc
                };
                if (branch.Id != timeline.Branch?.Id)
                    branch = branch with { StartedFromId = backup.Id, StartedFromName = backup.Name };
            }
            var saved = previousMetadata.Save with
            {
                CreatedUtc = DateTimeOffset.UtcNow,
                WorldTick = state.Society.Society.WorldTick,
                Branch = branch,
                ContinuedFromId = timeline?.ContinuedFromId,
                ContinuedFromCreatedUtc = timeline?.ContinuedFromCreatedUtc,
                BranchPosition = NextBranchPosition(branch, timeline),
                GameVersion = BuildInformation.Version,
                SourceRevision = BuildInformation.SourceRevision,
            };
            var generation = Guid.NewGuid().ToString("N");
            // Only metadata publishes the new immutable generation. A failed metadata
            // replacement leaves the prior checkpoint and routing/settings selected.
            WriteAtomic(GenerationPath(id, generation), bytes);
            AdvanceTimeline(worldId, timeline, saved, bytes);
            WriteAtomic(MetadataPath(id), JsonSerializer.SerializeToUtf8Bytes(new Metadata(
                saved, assignments, autosaveSettings, worldId, generation)), overwrite: true);
            // Publication has committed. The independent recovery copy preserves
            // these old bytes; cleanup failure must not fail the new save.
            try { File.Delete(previousPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (logger is not null)
                    ManualWorldSaveTelemetry.SupersededCheckpointRetained(logger, id, exception.GetType().Name);
            }
            return new ManualSaveOverwriteReceipt(saved, backup.Id);
        }
    }

    private ManualWorldSave CreateCore(string name, WorldRuntime runtime,
        IReadOnlyList<AgentProviderAssignment> assignments,
        WorldAutosaveSettings? autosaveSettings, bool isAutosave, RecoveryProvenance? recovery = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(assignments);
        name = NormalizeName(name);
        lock (gate)
        {
            RequireNoPendingLoad();
            var state = runtime.ExportState();
            if (!isAutosave && !state.Society.Society.IsPaused)
                throw new InvalidOperationException("Pause the world before making a manual save.");
            var bytes = EncodeReadableCheckpoint(state);
            var worldId = state.Society.Society.WorldId;
            var timeline = ReadTimeline(worldId);
            var branch = ResolveBranch(timeline, List(worldId));
            var entry = new ManualWorldSave(Guid.NewGuid().ToString("N"), name, DateTimeOffset.UtcNow,
                state.Society.Society.WorldTick, isAutosave, branch, timeline?.ContinuedFromId,
                timeline?.ContinuedFromCreatedUtc, NextBranchPosition(branch, timeline),
                BuildInformation.Version, BuildInformation.SourceRevision);
            PrivateStorage.EnsureDirectory(directory);
            WriteAtomic(StatePath(entry.Id), bytes);
            // The branch record moves before the save is published. A failure in
            // between leaves a record pointing at an unlisted save, whose
            // position still keeps the next save on this branch.
            AdvanceTimeline(worldId, timeline, entry, bytes);
            WriteAtomic(MetadataPath(entry.Id), JsonSerializer.SerializeToUtf8Bytes(
                new Metadata(entry, assignments, autosaveSettings, worldId, Recovery: recovery)));
            return entry;
        }
    }

    private static byte[] EncodeReadableCheckpoint(WorldRuntimeState state)
    {
        var bytes = WorldRuntimeCodec.Encode(state);
        // Validate the serialized representation before publishing files or moving
        // the timeline: JSON can change text that was valid only in memory.
        _ = WorldRuntimeCodec.Decode(bytes);
        return bytes;
    }

    /// <summary>
    /// Record that the running world now continues from one of its saves, as
    /// after loading it. The next save continues that save's branch only if the
    /// branch has nothing later; otherwise it starts a new branch.
    /// </summary>
    public SaveTimelineRestorePoint ContinueFrom(string id)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            if (!File.Exists(MetadataPath(id)))
                throw new FileNotFoundException("The manual save does not exist.");
            var metadata = ReadMetadata(id);
            var worldId = metadata.WorldId ?? throw new InvalidDataException("The manual save has no world.");
            var path = TimelinePath(worldId);
            var restore = new SaveTimelineRestorePoint(worldId, File.Exists(path) ? SavedFile.Read(path) : null);
            var previous = ReadTimeline(worldId);
            var save = WithValidBranch(metadata.Save);
            var checkpointFingerprint = Fingerprint(SavedFile.Read(CommittedStatePath(id, metadata), SavedFile.CheckpointLimit));
            WriteTimeline(new Timeline(worldId, save.Branch, save.Id, save.Name, save.WorldTick,
                Math.Max(previous?.LastBranchNumber ?? 0, save.Branch?.Number ?? 0), save.CreatedUtc,
                save.BranchPosition, SavedCheckpointFingerprint: checkpointFingerprint));
            return restore;
        }
    }

    public void RestoreTimeline(SaveTimelineRestorePoint restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        lock (gate)
        {
            var path = TimelinePath(restore.WorldId);
            if (restore.Bytes is null) File.Delete(path);
            else WriteAtomic(path, restore.Bytes, overwrite: true);
        }
    }

    /// <summary>Remember the loaded state after the host has applied its required pause.</summary>
    public void RecordLoadedState(WorldRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        lock (gate)
        {
            var state = runtime.ExportState();
            var timeline = ReadTimeline(state.Society.Society.WorldId)
                ?? throw new InvalidDataException("The loaded save has no branch record.");
            WriteTimeline(timeline with { LoadedStateFingerprint = Fingerprint(WorldRuntimeCodec.Encode(state)) });
        }
    }

    /// <summary>
    /// The save the running world continues from, when nothing has changed since:
    /// the same world state, model routing and autosave choices. Loading another
    /// save then needs no extra copy of the world being left.
    /// </summary>
    public ManualWorldSave? FindUnchangedSave(WorldRuntime runtime,
        IReadOnlyList<AgentProviderAssignment> assignments, WorldAutosaveSettings? autosaveSettings)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(assignments);
        lock (gate)
        {
            var state = runtime.ExportState();
            var worldId = state.Society.Society.WorldId;
            var timeline = ReadTimeline(worldId);
            if (timeline?.ContinuedFromId is not { } id || !File.Exists(MetadataPath(id)))
                return null;
            try
            {
                var metadata = ReadMetadata(id);
                if (metadata.WorldId != worldId || metadata.Save.CreatedUtc != timeline.ContinuedFromCreatedUtc ||
                    !metadata.Assignments.SequenceEqual(assignments) ||
                    !SameAutosaveChoices(metadata.AutosaveSettings, autosaveSettings))
                    return null;
                var bytes = WorldRuntimeCodec.Encode(state);
                // A remembered runtime baseline cannot stand in for a missing
                // or damaged recovery checkpoint. Verify the stored bytes too.
                var checkpointBytes = SavedFile.Read(CommittedStatePath(id, metadata), SavedFile.CheckpointLimit);
                var unchanged = timeline.SavedCheckpointFingerprint is { } checkpointFingerprint &&
                    timeline.LoadedStateFingerprint is { } fingerprint
                    ? checkpointFingerprint == Fingerprint(checkpointBytes) && fingerprint == Fingerprint(bytes)
                    : checkpointBytes.AsSpan().SequenceEqual(bytes);
                return unchanged ? metadata.Save : null;
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Where the running world continues, for the Load Save timeline. Reading it
    /// changes nothing; the next save makes the same branch choice it reports.
    /// </summary>
    public SaveTimelinePosition CurrentPosition(string worldId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);
        lock (gate)
        {
            var timeline = ReadTimeline(worldId);
            var worldSaves = List(worldId);
            var continuing = ContinuingBranch(timeline, worldSaves);
            return new SaveTimelinePosition(timeline?.ContinuedFromId, timeline?.Branch?.Id,
                continuing is null, continuing?.Number ?? NextBranchNumber(timeline, worldSaves),
                timeline?.ContinuedFromTick);
        }
    }

    private static bool SameAutosaveChoices(WorldAutosaveSettings? left, WorldAutosaveSettings? right) =>
        left is null || right is null
            ? left is null && right is null
            : left.Enabled == right.Enabled && left.IntervalMinutes == right.IntervalMinutes &&
              left.RotationCount == right.RotationCount;

    private SaveBranch ResolveBranch(Timeline? timeline, IReadOnlyList<ManualWorldSave> worldSaves)
    {
        if (ContinuingBranch(timeline, worldSaves) is { } current)
            return current;
        var number = NextBranchNumber(timeline, worldSaves);
        var started = timeline?.ContinuedFromId is { } fromId
            ? new SaveBranch(Guid.NewGuid().ToString("N"), number, fromId, timeline.ContinuedFromName,
                timeline.ContinuedFromTick)
            : new SaveBranch(Guid.NewGuid().ToString("N"), number);
        if (logger is not null && started.StartedFromId is { } from)
            ManualWorldSaveTelemetry.BranchStarted(logger, number, from);
        return started;
    }

    // The running world's branch, when nothing later has been saved on it.
    private static SaveBranch? ContinuingBranch(Timeline? timeline, IReadOnlyList<ManualWorldSave> worldSaves) =>
        // Positions order snapshots even when the world tick stays unchanged or
        // an intermediate checkpoint has been deleted. Recovery copies retain
        // the original position and do not themselves advance the history.
        timeline?.Branch is { } current && !worldSaves.Any(save => save.Branch?.Id == current.Id &&
            save.BranchPosition > timeline.ContinuedFromBranchPosition)
            ? current
            : null;

    private static int NextBranchNumber(Timeline? timeline, IReadOnlyList<ManualWorldSave> worldSaves) =>
        Math.Max(timeline?.LastBranchNumber ?? 0,
            worldSaves.Select(save => save.Branch?.Number ?? 0).DefaultIfEmpty(0).Max()) + 1;

    private static long NextBranchPosition(SaveBranch branch, Timeline? timeline) =>
        branch.Id == timeline?.Branch?.Id ? checked(timeline.ContinuedFromBranchPosition + 1) : 1;

    private static string Fingerprint(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private void AdvanceTimeline(string worldId, Timeline? previous, ManualWorldSave saved, byte[] bytes)
    {
        var fingerprint = Fingerprint(bytes);
        WriteTimeline(new Timeline(worldId, saved.Branch, saved.Id, saved.Name, saved.WorldTick,
            Math.Max(previous?.LastBranchNumber ?? 0, saved.Branch?.Number ?? 0), saved.CreatedUtc,
            saved.BranchPosition, fingerprint, fingerprint));
    }

    private Timeline? ReadTimeline(string worldId)
    {
        var path = TimelinePath(worldId);
        if (!File.Exists(path)) return null;
        try
        {
            var timeline = JsonSerializer.Deserialize<Timeline>(SavedFile.Read(path));
            if (timeline is not null && timeline.WorldId == worldId && IsValidBranch(timeline.Branch) &&
                (timeline.ContinuedFromId is null || IsId(timeline.ContinuedFromId)) &&
                timeline.LastBranchNumber is >= 0 and < int.MaxValue &&
                (timeline.Branch is null ? timeline.ContinuedFromBranchPosition == 0
                    : timeline.ContinuedFromBranchPosition is > 0 and < long.MaxValue) &&
                IsValidFingerprint(timeline.LoadedStateFingerprint) && IsValidFingerprint(timeline.SavedCheckpointFingerprint))
                return timeline;
        }
        catch (Exception exception) when (exception is not SavedFileLimitException &&
            exception is JsonException or IOException or UnauthorizedAccessException)
        { }
        // Ordinary damaged records start a new branch; oversized originals stay intact.
        if (logger is not null) ManualWorldSaveTelemetry.InvalidTimeline(logger);
        return null;
    }

    private void WriteTimeline(Timeline timeline)
    {
        PrivateStorage.EnsureDirectory(directory);
        WriteAtomic(TimelinePath(timeline.WorldId), JsonSerializer.SerializeToUtf8Bytes(timeline), overwrite: true);
    }

    private static bool IsValidFingerprint(string? fingerprint) => fingerprint is null ||
        fingerprint is { Length: 64 } && fingerprint.All(char.IsAsciiHexDigit);

    private string TimelinePath(string worldId) => Path.Combine(directory, "timeline-" +
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(worldId)))[..32] + ".json");

    private static bool IsValidBranch(SaveBranch? branch) => branch is null ||
        IsId(branch.Id) && branch.Number is >= 1 and < int.MaxValue &&
        (branch.StartedFromId is null || IsId(branch.StartedFromId)) &&
        (branch.StartedFromName is null || branch.StartedFromName.Length <= 80 &&
            !branch.StartedFromName.Any(char.IsControl)) &&
        branch.StartedFromTick is null or >= 0;

    // A damaged branch record only loses the save's branch; the save itself stays listed.
    private static ManualWorldSave WithValidBranch(ManualWorldSave save) =>
        IsValidBranch(save.Branch) && (save.Branch is null ? save.BranchPosition == 0
                : save.BranchPosition is > 0 and < long.MaxValue) &&
            (save.ContinuedFromId is null || IsId(save.ContinuedFromId))
            ? save : save with { Branch = null, ContinuedFromId = null, ContinuedFromCreatedUtc = null, BranchPosition = 0 };

    // The metadata rename is the durable point of deletion. A partial cleanup
    // stays hidden from List/Read and can be resumed without reviving the save.
    public void Delete(string id, string worldId, DateTimeOffset expectedCreatedUtc)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            RequireNoPendingLoad();
            var intent = Path.Combine(directory, id + ".deleting.json");
            var source = File.Exists(intent) ? intent : MetadataPath(id);
            var metadata = ReadDeletionMetadata(source, id);
            if (metadata.WorldId != worldId || metadata.Save.CreatedUtc != expectedCreatedUtc)
                throw new InvalidOperationException("The selected save changed. Refresh the list before deleting.");
            if (source != intent) File.Move(source, intent);
            DeleteCheckpointFiles(id);
            File.Delete(intent);
        }
    }

    public void DeleteWorldSnapshots(string worldId)
    {
        lock (gate)
        {
            RequireNoPendingLoad();
            if (!Directory.Exists(directory)) return;
            var foreignSaveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(directory, "*.json"))
            {
                var name = Path.GetFileName(path);
                if (!name.EndsWith(".meta.json", StringComparison.Ordinal) &&
                    !name.EndsWith(".deleting.json", StringComparison.Ordinal)) continue;
                var id = name.Split('.')[0];
                if (!IsId(id)) continue;
                var metadata = ReadDeletionMetadata(path, id);
                if (metadata.WorldId == worldId)
                    Delete(id, worldId, metadata.Save.CreatedUtc);
                else if (!string.IsNullOrWhiteSpace(metadata.WorldId))
                    foreignSaveIds.Add(id);
            }
            // A failed create/overwrite may leave unpublished generations. Decode
            // ownership before removal; never guess from a filename or seed.
            foreach (var path in Directory.GetFiles(directory, "*.save"))
            {
                var parts = Path.GetFileName(path).Split('.');
                if (parts.Length is not (2 or 3) || !IsId(parts[0]) ||
                    parts.Length == 3 && !IsId(parts[1])) continue;
                // Preserve another world's recorded save and all its generations
                // without requiring that unrelated checkpoint to remain readable.
                if (foreignSaveIds.Contains(parts[0])) continue;
                var state = WorldRuntimeCodec.Decode(SavedFile.Read(path, SavedFile.CheckpointLimit));
                if (state.Society.Society.WorldId == worldId) File.Delete(path);
            }
            File.Delete(TimelinePath(worldId));
        }
    }

    public void RecoverDeletions()
    {
        lock (gate)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.GetFiles(directory, "*.deleting.json"))
            {
                var id = Path.GetFileName(path)[..^".deleting.json".Length];
                var metadata = ReadDeletionMetadata(path, id);
                if (metadata.WorldId is null)
                    throw new InvalidDataException("The pending save deletion identity is invalid.");
                Delete(metadata.Save.Id, metadata.WorldId, metadata.Save.CreatedUtc);
            }
        }
    }

    private static Metadata ReadDeletionMetadata(string path, string id)
    {
        var metadata = DeserializeMetadata(SavedFile.Read(path));
        // Deletion needs a verified identity, not playable routing/settings.
        // Malformed records must enter the existing pending-cleanup path.
        if (!IsId(id) || metadata?.Save is null || metadata.Save.Id != id)
            throw new InvalidDataException("The save deletion identity is invalid.");
        return metadata;
    }

    private void DeleteCheckpointFiles(string id)
    {
        File.Delete(StatePath(id));
        foreach (var path in Directory.GetFiles(directory, id + ".*.save"))
        {
            var parts = Path.GetFileName(path).Split('.');
            if (parts.Length == 3 && IsId(parts[1])) File.Delete(path);
        }
    }

    public IReadOnlyList<ManualWorldSave> List(string? worldId = null)
    {
        lock (gate)
        {
            if (!Directory.Exists(directory)) return [];
            var entries = new List<ManualWorldSave>();
            var invalidPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(directory, "*.meta.json"))
            {
                // Metadata cannot redirect rotation onto a different checkpoint.
                var fileId = Path.GetFileName(path)[..^".meta.json".Length];
                try
                {
                    var item = DeserializeMetadata(SavedFile.Read(path));
                    if (!IsValidMetadata(item, fileId))
                    {
                        ReportInvalid(path, fileId, "invalid_metadata", invalidPaths);
                        continue;
                    }
                    if (!File.Exists(CommittedStatePath(fileId, item)))
                    {
                        ReportInvalid(path, fileId, "missing_checkpoint", invalidPaths);
                        continue;
                    }
                    reportedInvalidMetadata.Remove(path);
                    if (worldId is null || item.WorldId == worldId) entries.Add(WithValidBranch(item.Save));
                }
                catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
                {
                    ReportInvalid(path, fileId, exception is JsonException ? "invalid_json" : "unreadable_metadata", invalidPaths);
                }
            }
            reportedInvalidMetadata.IntersectWith(invalidPaths);
            return entries
                .OrderByDescending(item => item.CreatedUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private void ReportInvalid(string path, string fileId, string reason, HashSet<string> invalidPaths)
    {
        invalidPaths.Add(path);
        if (reportedInvalidMetadata.Add(path) && logger is not null)
            ManualWorldSaveTelemetry.InvalidMetadata(logger, IsId(fileId) ? fileId : "invalid", reason);
    }

    public void KeepNewestAutosaves(int count, ProviderConfigurationStore providers,
        string? preserveId = null, string? worldId = null)
    {
        if (count is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(count));
        lock (gate)
        {
            RequireNoPendingLoad();
            // Each branch keeps its own newest autosaves, so playing one branch
            // never retires another branch's history. Unknown histories stay
            // recoverable rather than being merged into the pre-branch group.
            var branches = List(worldId).Where(item => item.IsAutosave && !ReadMetadata(item.Id).HasInvalidBranchMetadata)
                .GroupBy(item => item.Branch?.Id ?? LegacyBranchKey, StringComparer.Ordinal);
            foreach (var branch in branches)
            {
                var verifiedCount = 0;
                foreach (var save in branch
                    // Branch positions keep creation order when the clock moves backwards.
                    // Genuine pre-branch saves have position zero and keep their UTC ordering.
                    .OrderByDescending(item => item.BranchPosition)
                    .ThenByDescending(item => item.CreatedUtc)
                    .ThenBy(item => item.Id, StringComparer.Ordinal))
                {
                    var metadata = ReadMetadata(save.Id);
                    try
                    {
                        VerifyAutosaveForRecovery(save, metadata.WorldId!, providers);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                        InvalidDataException or JsonException or ArgumentException)
                    {
                        if (logger is not null)
                            ManualWorldSaveTelemetry.UnverifiedAutosaveRetained(logger, save.Id, exception.GetType().Name);
                        continue;
                    }
                    // Copies recovery refuses neither rotate nor displace a usable predecessor.
                    // A pinned older copy may exceed the count, just as in recovery cleanup.
                    if (++verifiedCount > count && save.Id != preserveId)
                        Delete(save.Id, metadata.WorldId!, save.CreatedUtc);
                }
            }
        }
    }

    public WorldRuntimeState Read(string id)
    {
        return ReadCommitted(id).Checkpoint;
    }

    /// <summary>Autosaves on the active history, including its last loaded point when a new branch has not saved yet.</summary>
    public IReadOnlyList<ManualWorldSave> StartupRecoveryCandidates(string worldId)
    {
        lock (gate)
        {
            var timeline = ReadTimeline(worldId);
            if (timeline is null && File.Exists(TimelinePath(worldId))) return [];
            var all = List(worldId);
            var candidates = all.Where(save => save.IsAutosave && !ReadMetadata(save.Id).HasInvalidBranchMetadata);
            if (timeline is not null)
            {
                candidates = candidates.Where(save => save.Branch?.Id == timeline.Branch?.Id);
                if (ContinuingBranch(timeline, all) is null && timeline.ContinuedFromId is not null)
                    candidates = candidates.Where(save => timeline.Branch is not null
                        ? save.BranchPosition <= timeline.ContinuedFromBranchPosition
                        : save.CreatedUtc <= timeline.ContinuedFromCreatedUtc);
            }
            return candidates.OrderByDescending(save => save.BranchPosition)
                .ThenByDescending(save => save.CreatedUtc).ThenBy(save => save.Id, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Read the checkpoint and its routing/settings from one published generation.</summary>
    public (WorldRuntimeState Checkpoint, IReadOnlyList<AgentProviderAssignment> Assignments,
        WorldAutosaveSettings? AutosaveSettings) ReadCommitted(string id)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            if (!File.Exists(MetadataPath(id)))
                throw new FileNotFoundException("The manual save does not exist.");
            var metadata = ReadMetadata(id);
            var checkpoint = WorldRuntimeCodec.Decode(SavedFile.Read(CommittedStatePath(id, metadata), SavedFile.CheckpointLimit));
            return (checkpoint, metadata.Assignments, metadata.AutosaveSettings);
        }
    }

    public IReadOnlyList<AgentProviderAssignment> ReadAssignments(string id)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            if (!File.Exists(MetadataPath(id)))
                throw new FileNotFoundException("The manual save does not exist.");
            return ReadMetadata(id).Assignments;
        }
    }

    public WorldAutosaveSettings? ReadAutosaveSettings(string id)
    {
        if (!IsId(id)) throw new ArgumentException("Invalid save ID.", nameof(id));
        lock (gate)
        {
            if (!File.Exists(MetadataPath(id)))
                throw new FileNotFoundException("The manual save does not exist.");
            return ReadMetadata(id).AutosaveSettings;
        }
    }

    private Metadata ReadMetadata(string id)
    {
        Metadata? metadata;
        try
        {
            metadata = DeserializeMetadata(SavedFile.Read(MetadataPath(id)));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The manual save metadata is invalid.", exception);
        }
        if (!IsValidMetadata(metadata, id))
            throw new InvalidDataException("The manual save metadata is invalid.");
        return metadata;
    }

    private static Metadata? DeserializeMetadata(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Save", out var save) ||
            save.ValueKind != JsonValueKind.Object)
            return JsonSerializer.Deserialize<Metadata>(bytes);

        // Parse required identity/checkpoint/routing fields strictly. Optional
        // branch and recovery fields cannot make a playable checkpoint disappear.
        using var core = new MemoryStream();
        using (var writer = new Utf8JsonWriter(core))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "Recovery") continue;
                if (property.Name != "Save" || property.Value.ValueKind != JsonValueKind.Object)
                {
                    property.WriteTo(writer);
                    continue;
                }
                writer.WritePropertyName(property.Name);
                writer.WriteStartObject();
                foreach (var field in property.Value.EnumerateObject())
                    if (field.Name is not ("Branch" or "ContinuedFromId" or "ContinuedFromCreatedUtc" or "BranchPosition"))
                        field.WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        var metadata = JsonSerializer.Deserialize<Metadata>(core.ToArray());
        if (metadata?.Save is null) return metadata;
        if (root.TryGetProperty("Recovery", out var recovery))
        {
            try
            {
                var provenance = recovery.Deserialize<RecoveryProvenance>();
                if (provenance is not null && IsId(provenance.SourceSaveId) && provenance.Sequence > 0)
                    metadata = metadata with { Recovery = provenance };
            }
            catch (JsonException) { }
        }
        try
        {
            var branch = JsonSerializer.Deserialize<BranchMetadata>(save.GetRawText());
            if (branch is not null)
            {
                var parsedSave = metadata.Save with
                {
                    Branch = branch.Branch,
                    ContinuedFromId = branch.ContinuedFromId,
                    ContinuedFromCreatedUtc = branch.ContinuedFromCreatedUtc,
                    BranchPosition = branch.BranchPosition
                };
                var validSave = WithValidBranch(parsedSave);
                return metadata with
                {
                    Save = validSave,
                    HasInvalidBranchMetadata = validSave != parsedSave
                };
            }
        }
        catch (JsonException) { }
        return metadata with { HasInvalidBranchMetadata = true };
    }

    private static bool IsValidMetadata([NotNullWhen(true)] Metadata? metadata, string id) =>
        metadata?.Save is { } save && IsId(id) && save.Id == id &&
        !string.IsNullOrWhiteSpace(save.Name) && save.Name.Length <= 80 &&
        !save.Name.Any(char.IsControl) && save.WorldTick >= 0 &&
        metadata.Assignments is not null && metadata.Assignments.All(item => item is not null) &&
        (metadata.Generation is null || IsId(metadata.Generation));

    private string CommittedStatePath(string id, Metadata metadata) => metadata.Generation is { } generation
        ? GenerationPath(id, generation) : StatePath(id);

    private string GenerationPath(string id, string generation)
    {
        if (!IsId(generation)) throw new InvalidDataException("The manual save generation is invalid.");
        return Path.Combine(directory, id + "." + generation + ".save");
    }

    private string StatePath(string id) => Path.Combine(directory, id + ".save");
    private string MetadataPath(string id) => Path.Combine(directory, id + ".meta.json");
    private static bool IsId(string? id) => id is { Length: 32 } && id.All(char.IsAsciiHexDigit);

    private static void WriteAtomic(string destination, byte[] bytes, bool overwrite = false)
    {
        SavedFile.CheckLength(bytes.LongLength, destination.EndsWith(".save", StringComparison.Ordinal)
            ? SavedFile.CheckpointLimit : SavedFile.MetadataLimit);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = PrivateStorage.CreateNew(temporary))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite);
            PrivateStorage.RestrictFile(destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record PendingLoad(int SchemaVersion, string SaveId, string BackupId,
        SaveTimelineRestorePoint Timeline, WorldAutosaveSettings Settings, bool Rollback = false);
    private string PendingLoadPath => Path.Combine(directory, "pending-load.json");
    public bool HasPendingLoad { get { lock (gate) return File.Exists(PendingLoadPath); } }

    internal void RequireNoPendingLoad()
    {
        if (HasPendingLoad)
            throw new InvalidOperationException("Finish recovering the interrupted save load before changing saves or worlds.");
    }

    /// <summary>Load all world-owned settings together, retaining a durable intent until completion.</summary>
    public void Load(string id, string backupId, WorldRuntime runtime, WorldStateFile stateFile,
        ProviderConfigurationStore providers, WorldAutosaveStore autosave)
    {
        lock (gate)
        {
            BeginLoad(id, backupId);
            try { ApplyPendingLoad(runtime, stateFile, providers, autosave); }
            catch
            {
                // Publish the rollback direction before its first write, so a
                // second interruption recovers the previous complete version.
                MarkLoadRollback();
                ApplyPendingLoad(runtime, stateFile, providers, autosave);
                throw;
            }
        }
    }

    internal void BeginLoad(string id, string backupId)
    {
        lock (gate)
        {
            RequireNoPendingLoad();
            var saved = ReadCommitted(id);
            var backup = ReadCommitted(backupId);
            var worldId = saved.Checkpoint.Society.Society.WorldId;
            if (backup.Checkpoint.Society.Society.WorldId != worldId || backup.AutosaveSettings is null)
                throw new InvalidDataException("A load needs a complete backup from the same world.");
            var path = TimelinePath(worldId);
            var restore = new SaveTimelineRestorePoint(worldId, File.Exists(path) ? SavedFile.Read(path) : null);
            var settings = saved.AutosaveSettings ?? backup.AutosaveSettings;
            if (settings.WorldId != worldId) throw new InvalidDataException("Autosave settings belong to another world.");
            WorldAutosaveStore.Validate(settings.IntervalMinutes, settings.RotationCount);
            WriteAtomic(PendingLoadPath, JsonSerializer.SerializeToUtf8Bytes(new PendingLoad(1, id, backupId, restore, settings)));
        }
    }

    internal void MarkLoadRollback()
    {
        lock (gate)
        {
            var pending = ReadPendingLoad() ?? throw new InvalidOperationException("No save load is pending.");
            WriteAtomic(PendingLoadPath, JsonSerializer.SerializeToUtf8Bytes(pending with { Rollback = true }), overwrite: true);
        }
    }

    // The callback observes completed durable writes; tests interrupt the exact
    // production sequence without timer guesses or terminating the test host.
    internal void ApplyPendingLoad(WorldRuntime runtime, WorldStateFile stateFile,
        ProviderConfigurationStore providers, WorldAutosaveStore autosave, Action<string>? afterWrite = null)
    {
        lock (gate)
        {
            var pending = ReadPendingLoad() ?? throw new InvalidOperationException("No save load is pending.");
            var saved = ReadCommitted(pending.Rollback ? pending.BackupId : pending.SaveId);
            if (saved.Checkpoint.Society.Society.WorldId != pending.Timeline.WorldId ||
                runtime.Society.WorldId != pending.Timeline.WorldId)
                throw new InvalidDataException("The pending load belongs to another world.");
            stateFile.VerifyRequiredHistory(saved.Checkpoint);
            if (!pending.Rollback) ContinueFrom(pending.SaveId);
            afterWrite?.Invoke("timeline");
            runtime.LoadPausedCheckpoint(saved.Checkpoint);
            stateFile.Save(runtime);
            afterWrite?.Invoke("checkpoint");
            providers.RestoreWorldAssignments(saved.Assignments);
            afterWrite?.Invoke("assignments");
            autosave.RestoreFromCheckpoint(pending.Rollback
                ? saved.AutosaveSettings ?? throw new InvalidDataException("The load backup has no autosave settings.")
                : pending.Settings);
            afterWrite?.Invoke("autosave");
            if (pending.Rollback) RestoreTimeline(pending.Timeline);
            else RecordLoadedState(runtime);
            afterWrite?.Invoke("loaded");
            File.Delete(PendingLoadPath);
            afterWrite?.Invoke("complete");
        }
    }

    /// <summary>Finish an interrupted load before opening the active checkpoint or serving the world.</summary>
    public void RecoverInterruptedLoad(WorldStateFile stateFile, ProviderConfigurationStore providers,
        Func<string, IDecisionProvider> providerFactory)
    {
        lock (gate)
        {
            var pending = ReadPendingLoad();
            if (pending is null) return;
            var saved = ReadCommitted(pending.Rollback ? pending.BackupId : pending.SaveId);
            stateFile.VerifyRequiredHistory(saved.Checkpoint);
            using var restored = WorldRuntime.Restore(saved.Checkpoint, providerFactory);
            restored.Pause();
            var autosave = new WorldAutosaveStore(stateFile.Path, pending.Timeline.WorldId,
                mutationGate: providers.WorldMutationGate);
            ApplyPendingLoad(restored, stateFile, providers, autosave);
            if (logger is not null) ManualWorldSaveTelemetry.LoadRecovered(logger,
                pending.Rollback ? "rollback" : "load", pending.SaveId, restored.WorldTick);
        }
    }

    private PendingLoad? ReadPendingLoad()
    {
        if (!File.Exists(PendingLoadPath)) return null;
        var pending = JsonSerializer.Deserialize<PendingLoad>(SavedFile.Read(PendingLoadPath));
        if (pending is not { SchemaVersion: 1, Timeline.WorldId.Length: > 0, Settings: not null } ||
            !IsId(pending.SaveId) || !IsId(pending.BackupId) || pending.Settings.WorldId != pending.Timeline.WorldId)
            throw new InvalidDataException("The pending save load is invalid.");
        WorldAutosaveStore.Validate(pending.Settings.IntervalMinutes, pending.Settings.RotationCount);
        return pending;
    }

    // Only copies made explicitly as recovery checkpoints carry this provenance.
    // Names, age and legacy files never confer deletion authority.
    private sealed record RecoveryProvenance(string SourceSaveId, long Sequence);

    private RecoveryProvenance NextRecovery(string sourceId)
    {
        var latest = List().Select(save => ReadMetadata(save.Id).Recovery)
            .Where(item => item?.SourceSaveId == sourceId && item.Sequence > 0)
            .Select(item => item!.Sequence).DefaultIfEmpty(0).Max();
        return new RecoveryProvenance(sourceId, checked(latest + 1));
    }

    public ManualWorldSave CreateLoadRecovery(WorldRuntime runtime,
        IReadOnlyList<AgentProviderAssignment> assignments, WorldAutosaveSettings? settings)
    {
        lock (gate)
        {
            var worldId = runtime.Society.WorldId;
            var source = ReadTimeline(worldId)?.ContinuedFromId;
            // Without a named source, retain the checkpoint as an ordinary save.
            var named = List(worldId).FirstOrDefault(save => save.Id == source && !save.IsAutosave &&
                ReadMetadata(save.Id).Recovery is null);
            return CreateCore("Before loading", runtime, assignments, settings, false,
                named is null ? null : NextRecovery(named.Id));
        }
    }

    public RecoveryCleanupPreview PreviewRecoveryCleanup(string worldId, int keepCount, WorldStateFile stateFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);
        ArgumentNullException.ThrowIfNull(stateFile);
        if (keepCount is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(keepCount));
        lock (gate)
        {
            List<ManualWorldSave> keep = [];
            List<(ManualWorldSave Save, RecoveryProvenance Provenance)> verified = [];
            List<string> inventory = [worldId, keepCount.ToString(CultureInfo.InvariantCulture)];
            var timeline = ReadTimeline(worldId);
            var timelinePath = TimelinePath(worldId);
            if (timeline is null && File.Exists(timelinePath))
                throw new InvalidDataException("Recovery cleanup cannot verify the active save's timeline.");
            if (File.Exists(timelinePath)) inventory.Add(Fingerprint(SavedFile.Read(timelinePath)));
            foreach (var save in List(worldId).OrderBy(save => save.Id, StringComparer.Ordinal))
            {
                var metadata = ReadMetadata(save.Id);
                inventory.Add(save.Id + ":" + Fingerprint(SavedFile.Read(MetadataPath(save.Id))));
                var provenance = metadata.Recovery;
                if (provenance is null || save.IsAutosave || !IsId(provenance.SourceSaveId) || provenance.Sequence <= 0)
                {
                    keep.Add(save);
                    continue;
                }
                try
                {
                    var bytes = SavedFile.Read(CommittedStatePath(save.Id, metadata), SavedFile.CheckpointLimit);
                    inventory.Add(save.Id + ":" + Fingerprint(bytes));
                    VerifyCheckpoint(bytes, worldId, stateFile);
                    verified.Add((save, provenance));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
                {
                    // Unverifiable checkpoints remain available for recovery.
                    inventory.Add(save.Id + ":unverified");
                    keep.Add(save);
                }
            }
            List<ManualWorldSave> remove = [];
            foreach (var group in verified.GroupBy(item => item.Provenance.SourceSaveId, StringComparer.Ordinal))
            {
                var ordered = group.OrderByDescending(item => item.Provenance.Sequence)
                    .ThenBy(item => item.Save.Id, StringComparer.Ordinal).ToArray();
                // At least one verified recovery survives for every source, even
                // when another (newer) copy is damaged or belongs to another schema.
                var retained = ordered.Take(keepCount).Select(item => item.Save.Id)
                    .ToHashSet(StringComparer.Ordinal);
                if (timeline?.ContinuedFromId is { } activeId) retained.Add(activeId);
                keep.AddRange(ordered.Where(item => retained.Contains(item.Save.Id)).Select(item => item.Save));
                remove.AddRange(ordered.Where(item => !retained.Contains(item.Save.Id)).Select(item => item.Save));
            }
            var kept = keep.OrderBy(save => save.Id, StringComparer.Ordinal).ToArray();
            var removed = remove.OrderBy(save => save.Id, StringComparer.Ordinal).ToArray();
            inventory.AddRange(kept.Select(save => "keep:" + save.Id));
            inventory.AddRange(removed.Select(save => "remove:" + save.Id));
            var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', inventory))));
            return new RecoveryCleanupPreview(worldId, keepCount, digest, removed, kept);
        }
    }

    public IReadOnlyList<string> CleanRecoveryHistory(string worldId, int keepCount, string expectedDigest,
        WorldStateFile stateFile)
    {
        lock (gate)
        {
            var preview = PreviewRecoveryCleanup(worldId, keepCount, stateFile);
            if (!string.Equals(preview.Digest, expectedDigest, StringComparison.Ordinal))
                throw new InvalidOperationException("Recovery history changed. Preview it again before deleting.");
            foreach (var save in preview.Remove) Delete(save.Id, worldId, save.CreatedUtc);
            return preview.Remove.Select(save => save.Id).ToArray();
        }
    }

    internal void VerifyAutosaveForRecovery(ManualWorldSave candidate, string worldId,
        ProviderConfigurationStore providers)
    {
        var saved = ReadCommitted(candidate.Id);
        if (saved.Checkpoint.Society.Society.WorldId != worldId ||
            candidate.WorldTick != saved.Checkpoint.Society.Society.WorldTick ||
            saved.AutosaveSettings is not { WorldId: var savedWorld } || savedWorld != worldId ||
            !providers.CanRestoreWorldAssignments(saved.Assignments, saved.Checkpoint.RoutineHelper))
            throw new InvalidDataException("Autosave is unavailable for startup recovery.");
        WorldAutosaveStore.Validate(saved.AutosaveSettings.IntervalMinutes, saved.AutosaveSettings.RotationCount);
        stateFile.VerifyRequiredHistory(saved.Checkpoint);
        using var verified = WorldRuntime.Restore(saved.Checkpoint);
    }

    private static void VerifyCheckpoint(byte[] bytes, string? worldId, WorldStateFile stateFile)
    {
        var checkpoint = WorldRuntimeCodec.Decode(bytes);
        if (checkpoint.Society.Society.WorldId != worldId)
            throw new InvalidDataException("Checkpoint identity does not match.");
        stateFile.VerifyRequiredHistory(checkpoint);
        using var restored = WorldRuntime.Restore(checkpoint);
    }
}
