using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Worlds;

namespace ClankerWorld.Server.Saves;

/// <param name="SavedByVersion">The game version that last saved a refused checkpoint, when it was recorded.</param>



/// <summary>Keep a refused active checkpoint untouched until an authenticated owner accepts a verified autosave.</summary>
public sealed partial class WorldStartupRecovery
{
    private readonly WorldStateFile stateFile;
    private readonly ManualWorldSaveStore saves;
    private readonly ProviderConfigurationStore providers;
    private readonly Func<string, IDecisionProvider> providerFactory;
    private readonly ILogger<WorldStartupRecovery> logger;
    private readonly byte[]? damagedBytes;
    private readonly string? reason;
    private readonly string? savedByVersion;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WorldRuntime? runtime;
    private readonly string identity;

    public WorldStartupRecovery(WorldStateFile stateFile, ManualWorldSaveStore saves,
        ProviderConfigurationStore providers, Func<string, IDecisionProvider> providerFactory,
        string seed, string authorityPath, ILogger<WorldStartupRecovery> logger)
    {
        this.stateFile = stateFile;
        this.saves = saves;
        this.providers = providers;
        this.providerFactory = providerFactory;
        this.logger = logger;
        // A pending manual load owns the checkpoint, routing, schedule and
        // timeline until it is complete. Failure must not expose a mixed world.
        saves.RecoverInterruptedLoad(stateFile, providers, providerFactory);
        try
        {
            runtime = stateFile.LoadOrCreate(seed);
            identity = runtime.Society.WorldId;
            ready.TrySetResult();
        }
        catch (Exception exception) when (File.Exists(stateFile.Path) &&
            exception is InvalidDataException or IOException or JsonException)
        {
            damagedBytes = File.ReadAllBytes(stateFile.Path);
            reason = RefusalReason(damagedBytes);
            savedByVersion = SavedBuild.TryRead(stateFile.Path)?.GameVersion;
            identity = ReadCheckpointIdentity(damagedBytes)
                ?? WorldCatalogStore.ReadActiveIdentity(stateFile.Path)?.WorldId
                ?? ReadAutosaveIdentity(stateFile.Path)
                ?? (File.Exists(authorityPath)
                    ? JsonSerializer.Deserialize<OwnerAuthorityState>(File.ReadAllBytes(authorityPath))?.Authority.WorldId : null)
                ?? "checkpoint-recovery";
            LogRecovery(logger, "waiting_for_owner", "checkpoint_refused", -1);
        }
    }

    public bool Pending { get { lock (providers.WorldMutationGate) return runtime is null; } }
    public string WorldId => identity;
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => ready.Task.WaitAsync(cancellationToken);

    public WorldRuntime Runtime
    {
        get { lock (providers.WorldMutationGate) return runtime ?? throw new InvalidOperationException("Choose recovery before opening the world."); }
    }

    public StartupRecoveryStatus Capture()
    {
        lock (providers.WorldMutationGate)
            return new(Pending, identity, Pending ? FindAutosave() : null, Pending ? reason : null,
                Pending ? savedByVersion : null);
    }

    private ManualWorldSave? FindAutosave()
    {
        foreach (var candidate in saves.StartupRecoveryCandidates(identity))
        {
            try
            {
                var saved = saves.ReadCommitted(candidate.Id);
                if (saved.Checkpoint.Society.Society.WorldId != identity ||
                    candidate.WorldTick != saved.Checkpoint.Society.Society.WorldTick ||
                    saved.AutosaveSettings is not { WorldId: var savedWorld } || savedWorld != identity ||
                    !providers.CanRestoreWorldAssignments(saved.Assignments, saved.Checkpoint.RoutineHelper)) continue;
                WorldAutosaveStore.Validate(saved.AutosaveSettings.IntervalMinutes, saved.AutosaveSettings.RotationCount);
                stateFile.VerifyRequiredHistory(saved.Checkpoint);
                using var verified = WorldRuntime.Restore(saved.Checkpoint);
                return candidate;
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or
                UnauthorizedAccessException or JsonException or ArgumentException)
            { }
        }
        return null;
    }

    public StartupRecoveryReceipt Recover(string id, WorldAutosaveStore autosave, OwnerAuthorityStore authority)
    {
        lock (providers.WorldMutationGate)
        {
            if (!Pending || FindAutosave()?.Id != id)
                throw new InvalidOperationException("Read recovery again and choose the offered autosave.");
            var saved = saves.ReadCommitted(id);
            stateFile.VerifyRequiredHistory(saved.Checkpoint);
            var recovered = WorldRuntime.Restore(saved.Checkpoint, providerFactory);
            recovered.Pause();
            var oldAssignments = providers.CaptureRuntimeConfiguration().Assignments ?? [];
            var oldSettings = autosave.Capture();
            SaveTimelineRestorePoint? timeline = null;
            CheckpointRestorePoint? checkpointRestore = null;
            var checkpointAttempted = false;
            var assignmentsAttempted = false;
            var settingsAttempted = false;
            try
            {
                checkpointRestore = stateFile.PreserveDamagedCheckpoint(damagedBytes!);
                timeline = saves.ContinueFrom(id);
                assignmentsAttempted = true;
                providers.RestoreWorldAssignments(saved.Assignments);
                settingsAttempted = true;
                autosave.RestoreFromCheckpoint(saved.AutosaveSettings!);
                checkpointAttempted = true;
                stateFile.Save(recovered);
                saves.RecordLoadedState(recovered);
                authority.DiscardChallengesForRecovery();
                runtime = recovered;
                LogRecovery(logger, "recovered", "owner_selected_autosave", recovered.WorldTick);
                ready.TrySetResult();
                return new(id, recovered.WorldTick);
            }
            catch
            {
                recovered.Dispose();
                if (checkpointAttempted) stateFile.RestorePreservedCheckpoint(checkpointRestore!);
                if (assignmentsAttempted) providers.RestoreWorldAssignments(oldAssignments);
                if (settingsAttempted) autosave.RestoreFromCheckpoint(oldSettings);
                if (timeline is not null) saves.RestoreTimeline(timeline);
                LogRecovery(logger, "refused", "storage_or_configuration_failed", -1);
                throw;
            }
        }
    }

    private static string? ReadCheckpointIdentity(byte[] bytes)
    {
        try
        {
            // An interrupted selection can publish the target checkpoint before
            // its catalog entry becomes active. A valid current-format identity wins.
            return WorldRuntimeCodec.Decode(bytes).Society.Society.WorldId;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException or FormatException)
        {
            return null;
        }
    }

    private static string? ReadAutosaveIdentity(string path)
    {
        var settingsPath = path + ".autosave.json";
        if (!File.Exists(settingsPath)) return null;
        var settings = JsonSerializer.Deserialize<WorldAutosaveSettings>(File.ReadAllBytes(settingsPath))
            ?? throw new InvalidDataException("Autosave settings are empty.");
        WorldAutosaveStore.Validate(settings.IntervalMinutes, settings.RotationCount);
        return string.IsNullOrWhiteSpace(settings.WorldId) ? null : settings.WorldId;
    }

    private static string RefusalReason(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number &&
                version.TryGetInt32(out var schema) && schema != WorldRuntime.StateSchemaVersion)
                return "different_save_format";
        }
        catch (JsonException) { }
        return "checkpoint_refused";
    }

    [LoggerMessage(EventId = 2320, Level = LogLevel.Warning,
        Message = "startup_recovery outcome={Outcome} reason={Reason} tick={WorldTick}")]
    private static partial void LogRecovery(ILogger logger, string outcome, string reason, long worldTick);
}
