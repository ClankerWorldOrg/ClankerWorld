using System.Text.Json;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Saves;

public sealed partial class ManualWorldSaveStore
{
    private sealed record PendingLoad(int SchemaVersion, string SaveId, string BackupId,
        SaveTimelineRestorePoint Timeline, WorldAutosaveSettings Settings, bool Rollback = false);
    private string PendingLoadPath => Path.Combine(directory, "pending-load.json");
    public bool HasPendingLoad { get { lock (gate) return File.Exists(PendingLoadPath); } }

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
            var saved = ReadCommitted(id);
            var backup = ReadCommitted(backupId);
            var worldId = saved.Checkpoint.Society.Society.WorldId;
            if (backup.Checkpoint.Society.Society.WorldId != worldId || backup.AutosaveSettings is null)
                throw new InvalidDataException("A load needs a complete backup from the same world.");
            var path = TimelinePath(worldId);
            var restore = new SaveTimelineRestorePoint(worldId, File.Exists(path) ? File.ReadAllBytes(path) : null);
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
        var pending = JsonSerializer.Deserialize<PendingLoad>(File.ReadAllBytes(PendingLoadPath));
        if (pending is not { SchemaVersion: 1, Timeline.WorldId.Length: > 0, Settings: not null } ||
            !IsId(pending.SaveId) || !IsId(pending.BackupId) || pending.Settings.WorldId != pending.Timeline.WorldId)
            throw new InvalidDataException("The pending save load is invalid.");
        WorldAutosaveStore.Validate(pending.Settings.IntervalMinutes, pending.Settings.RotationCount);
        return pending;
    }
}
