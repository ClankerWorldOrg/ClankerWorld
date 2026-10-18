using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace ClankerWorld.Server.Worlds;

/// <summary>Serializes paused-world selection against saves and provider routing.</summary>
public sealed class WorldSelectionCoordinator(
    WorldCatalogStore catalog,
    WorldRuntime runtime,
    WorldStateFile stateFile,
    ProviderConfigurationStore providers,
    WorldAutosaveStore autosave,
    WorldJevPolicy jevPolicy,
    ILogger<WorldSelectionCoordinator> logger,
    Func<string, IDecisionProvider> providerFactory,
    ManualWorldSaveStore saves)
{
    private readonly object gate = providers.WorldMutationGate;
    // Bound read-only generation without blocking the current world's transactions.
    private readonly object previewGate = new();
    private readonly Func<GeographyOptions, GeographyCandidateSelection> selectGeographyCandidates =
        GeographyCandidateSelector.Select;
    // Concurrent because WarmUp adds results without the world-mutation gate.
    private readonly ConcurrentDictionary<string, CachedCheckpoint> checkedCheckpoints = new(StringComparer.Ordinal);

    internal WorldSelectionCoordinator(
        WorldCatalogStore catalog,
        WorldRuntime runtime,
        WorldStateFile stateFile,
        ProviderConfigurationStore providers,
        WorldAutosaveStore autosave,
        WorldJevPolicy jevPolicy,
        ILogger<WorldSelectionCoordinator> logger,
        Func<string, IDecisionProvider> providerFactory,
        ManualWorldSaveStore saves,
        Func<GeographyOptions, GeographyCandidateSelection> selectCandidates)
        : this(catalog, runtime, stateFile, providers, autosave, jevPolicy, logger, providerFactory, saves)
    {
        ArgumentNullException.ThrowIfNull(selectCandidates);
        selectGeographyCandidates = selectCandidates;
    }

    private sealed record CachedCheckpoint(string WorldId, string Seed, string Digest,
        string? HistoryArchiveHead, IReadOnlyList<RetiredBoatRequestRange> RetiredRequests, IReadOnlyDictionary<string, long> RecordCounts,
        bool Restorable, WorldThumbnail? Thumbnail, RoutineHelperSettings? Helper = null);

    /// <summary>Browse a catalog world's saves without activating its checkpoint.</summary>
    public WorldSaveHistory ReadSaveHistory(string id)
    {
        lock (gate)
        {
            var world = catalog.Capture().Worlds.SingleOrDefault(world => world.Id == id)
                ?? throw new FileNotFoundException("The selected world does not exist.");
            var state = id == catalog.Active().Id ? runtime.ExportState() : catalog.Read(id);
            var config = state.WorldSystems?.Config;
            var calendar = config is null ? null : new OwnerWorldCalendarPace(config.TicksPerDay, config.DaysPerYear,
                config.SpringDays, config.SummerDays, config.AutumnDays, config.WinterDays, config.CalendarOffsetTicks);
            return new(world.WorldId, saves.List(world.WorldId), saves.CurrentPosition(world.WorldId), calendar, state.Society.Society.WorldTick);
        }
    }

    public WorldCatalogSnapshot List(CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        var (worldCount, cacheHits, scans) = (0, 0, 0);
        try
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = catalog.Capture();
                var currentIds = snapshot.Worlds.Select(world => world.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var id in checkedCheckpoints.Keys.Where(id => !currentIds.Contains(id)).ToArray())
                    checkedCheckpoints.TryRemove(id, out _);
                var worlds = new CatalogWorld[snapshot.Worlds.Count];
                worldCount = worlds.Length;
                for (var index = 0; index < worlds.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var world = snapshot.Worlds[index];
                    if (world.Id == snapshot.ActiveId)
                        worlds[index] = WithThumbnail(world, () => runtime.ExportState().Map) with
                        {
                            Compatibility = "compatible",
                            CompatibilityReason = null
                        };
                    else
                    {
                        worlds[index] = Assess(world, out var cacheHit);
                        if (cacheHit) cacheHits++;
                        else scans++;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                WorldSelectionTelemetry.Listed(logger, worldCount, cacheHits, scans, elapsed.ElapsedMilliseconds);
                return snapshot with { Worlds = worlds };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WorldSelectionTelemetry.ListCanceled(logger, worldCount, cacheHits, scans, elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Checks each inactive checkpoint once after the host starts, so the first
    /// Load World list can reuse the results. The world-mutation gate is held
    /// only while the catalog reads its index and each checkpoint file, never
    /// while decoding or restoring, so ticks, saves and owner actions do not wait
    /// for the checks. A result belongs to the exact checkpoint bytes it was made
    /// from, and List checks a checkpoint again if its bytes have changed since.
    /// A list opened meanwhile checks any world not done yet itself; both reach
    /// the same result, so the duplicate work only costs time.
    /// </summary>
    public void WarmUp(CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        var (checks, unrestorable, skipped, failed) = (0, 0, 0, 0);
        var outcome = "finished";
        WorldSelectionTelemetry.WarmUpStarted(logger);
        var snapshot = catalog.Capture();
        foreach (var world in snapshot.Worlds.Where(world => world.Id != snapshot.ActiveId))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                outcome = "canceled";
                break;
            }
            // Skip worlds deleted or opened since the warm-up began, and worlds a
            // list already checked: its result is at least as recent.
            var current = catalog.Capture();
            if (current.ActiveId == world.Id || current.Worlds.All(entry => entry.Id != world.Id) ||
                checkedCheckpoints.ContainsKey(world.Id))
            {
                skipped++;
                continue;
            }
            try
            {
                var bytes = catalog.ReadSnapshotBytes(world.Id);
                var result = CheckCheckpoint(world, bytes, Digest(bytes));
                if (!checkedCheckpoints.TryAdd(world.Id, result)) skipped++;
                else
                {
                    checks++;
                    if (!result.Restorable) unrestorable++;
                }
            }
            catch (Exception exception)
            {
                // Never stop the host over one save. Load World checks this world
                // itself and reports it as it would without the warm-up.
                failed++;
                WorldSelectionTelemetry.WarmUpFailed(logger, world.Id, exception.GetType().Name);
                if (exception is OutOfMemoryException)
                {
                    outcome = "stopped";
                    break;
                }
            }
        }
        WorldSelectionTelemetry.WarmUpEnded(logger, outcome, checks, unrestorable, skipped, failed,
            elapsed.ElapsedMilliseconds);
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// Worlds catalogued before thumbnails existed get one the first time they
    /// are listed, from the map already at hand, and the catalog keeps it.
    /// </summary>
    private CatalogWorld WithThumbnail(CatalogWorld world, Func<WorldMap> map) =>
        world.Thumbnail is not null ? world : WithThumbnail(world, WorldThumbnail.From(map()));

    private CatalogWorld WithThumbnail(CatalogWorld world, WorldThumbnail? thumbnail)
    {
        if (world.Thumbnail is not null || thumbnail is null) return world;
        try
        {
            catalog.RememberThumbnail(world.Id, thumbnail);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The list still shows it; the catalog can keep it next time.
        }
        return world with { Thumbnail = thumbnail };
    }

    private CatalogWorld Assess(CatalogWorld world, out bool cacheHit)
    {
        cacheHit = false;
        try
        {
            var bytes = catalog.ReadSnapshotBytes(world.Id);
            var digest = Digest(bytes);
            if (!checkedCheckpoints.TryGetValue(world.Id, out var checkedCheckpoint) ||
                checkedCheckpoint.WorldId != world.WorldId || checkedCheckpoint.Seed != world.Seed ||
                checkedCheckpoint.Digest != digest)
            {
                checkedCheckpoint = CheckCheckpoint(world, bytes, digest);
                checkedCheckpoints[world.Id] = checkedCheckpoint;
            }
            else cacheHit = true;
            world = WithThumbnail(world, checkedCheckpoint.Thumbnail);
            if (!checkedCheckpoint.Restorable)
                return Unrestorable(world);
            // History files and provider credentials can change without a
            // checkpoint rewrite; never reuse their previous assessment.
            stateFile.VerifyRequiredHistory(checkedCheckpoint.HistoryArchiveHead, checkedCheckpoint.RetiredRequests, checkedCheckpoint.RecordCounts);
            if (!providers.CanRestoreWorldAssignments(world.Assignments, checkedCheckpoint.Helper))
                return world with
                {
                    Compatibility = "incompatible",
                    CompatibilityReason = "A model setting or saved key it needs is missing."
                };
            return world with { Compatibility = "compatible", CompatibilityReason = null };
        }
        catch (SavedFileLimitException exception)
        { return world with { Compatibility = "incompatible", CompatibilityReason = exception.Message }; }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or
            FileNotFoundException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            return Unrestorable(world);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return world with
            {
                Compatibility = "unknown",
                CompatibilityReason = "Its save couldn't be checked right now."
            };
        }
    }

    private static CachedCheckpoint CheckCheckpoint(CatalogWorld world, byte[] bytes, string digest)
    {
        WorldThumbnail? thumbnail = null;
        try
        {
            var checkpoint = WorldRuntimeCodec.Decode(bytes);
            if (checkpoint.Society.Society.WorldId != world.WorldId || checkpoint.WorldSeed != world.Seed)
                throw new InvalidDataException("The selected world checkpoint does not match its catalog entry.");
            // Keep the picture even if the restore below fails, as a full read did.
            if (world.Thumbnail is null) thumbnail = WorldThumbnail.From(checkpoint.Map);
            // Structural validation is independent of the installation's mutable
            // provider routing. Credentials and assignments are checked in Assess.
            using var verified = WorldRuntime.Restore(checkpoint);
            return new CachedCheckpoint(world.WorldId, world.Seed, digest,
                checkpoint.HistoryArchiveHead, checkpoint.BoatTransport.RetiredRequestRanges, WorldHistory.RequiredRecordCounts(checkpoint), true, thumbnail,
                checkpoint.RoutineHelper);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or
            System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            return new CachedCheckpoint(world.WorldId, world.Seed, digest, null, [], new Dictionary<string, long>(), false, thumbnail);
        }
    }

    /// <summary>
    /// A world this build can't restore. When another game version last saved
    /// it, the player can open it there (#1566), so it is told apart from a
    /// damaged save. A missing model setup stays plain "incompatible".
    /// </summary>
    internal static CatalogWorld Unrestorable(CatalogWorld world) =>
        world.GameVersion is { Length: > 0 } version && version != BuildInformation.Version
            ? world with
            {
                Compatibility = OtherVersion,
                CompatibilityReason = $"ClankerWorld {version} saved this world, and this version can't open it.",
            }
            : world with
            {
                Compatibility = "incompatible",
                CompatibilityReason = "This version can't open its save or the game content it needs."
            };

    /// <summary>Compatibility of a world that another game version can open.</summary>
    public const string OtherVersion = "other_version";

    private static OwnerWorldCandidateReport CandidateReport(GeographyCandidateReport report) => new(
        report.Attempt, report.DryLandTiles, report.ForestTiles, report.MountainTiles,
        report.ForestPercent, report.MountainPercent, report.ForestRegionCount,
        report.LargestForestRegion, report.MountainRegionCount, report.LargestMountainRegion,
        report.ForestTargetApplicable, report.MountainTargetApplicable,
        report.ForestTargetMet, report.MountainTargetMet);

    public WorldPreviewView Preview(GeographyOptions geography)
    {
        ArgumentNullException.ThrowIfNull(geography);
        if (geography.Size is not (WorldSizePreset.Small or WorldSizePreset.Medium))
            throw new ArgumentException("Only Small and Medium are playable yet.", nameof(geography));
        lock (previewGate)
        {
            // Preview is read-only. The title screen can preview a new map while
            // the currently selected world is running or waiting for a client;
            // Create and Select still require a confirmed pause.
            var selection = selectGeographyCandidates(geography);
            var map = selection.Map;
            // The preview contract retains a suggested passable area for older
            // clients, but fresh maps have no placed objects or Town at this site.
            var startingSite = map.Resources.First(item => item.Id == "berry-patch").Position;
            WorldSelectionTelemetry.Previewed(logger, map.Width, map.Height);
            return new WorldPreviewView(OwnerWorldObservationStore.PackTerrain(map),
                new PositionView(startingSite.X, startingSite.Y), map.ManifestDigest, map.Resources.Count)
            {
                PackedMapLayers = OwnerWorldObservationStore.PackMapLayers(map, geography.Seed),
                MapLayersDigest = MapLayerManifestCodec.Digest(map),
                Coverage = CandidateReport(selection.Selected),
                Candidates = selection.Candidates.Select(CandidateReport).ToArray(),
                FailedCandidates = selection.FailedCandidates.Select(item => new OwnerWorldCandidateFailure(item.Attempt, item.Reason)).ToArray(),
            };
        }
    }

    public CatalogWorld Create(string name, GeographyOptions geography, int candidateAttempt,
        string expectedManifestDigest, string expectedMapLayersDigest, bool acceptUnmetTargets)
    {
        ArgumentNullException.ThrowIfNull(geography);
        if (geography.Size is not (WorldSizePreset.Small or WorldSizePreset.Medium))
            throw new ArgumentException("Large, Huge and Mega need compact persistent terrain before they can be played.", nameof(geography));
        lock (gate)
        {
            RequirePaused();
            // Regenerate the bounded selection once so the signed create action
            // can be checked against the exact preview identity.
            var selection = selectGeographyCandidates(geography with { CandidateAttempt = 0 });
            if (candidateAttempt != selection.Map.GenerationAttempt ||
                !string.Equals(expectedManifestDigest, selection.Map.ManifestDigest, StringComparison.Ordinal) ||
                !string.Equals(expectedMapLayersDigest, MapLayerManifestCodec.Digest(selection.Map), StringComparison.Ordinal))
                throw new InvalidOperationException("The preview is out of date. Preview the map again before creating it.");
            if (!selection.Selected.MeetsTargets && !acceptUnmetTargets)
                throw new InvalidOperationException("The selected map misses the displayed trial targets. Accept its coverage explicitly or choose a new seed.");

            var chosenOptions = geography with { CandidateAttempt = candidateAttempt };
            using var created = WorldRuntime.CreateFromGeneratedGeography(geography.Seed,
                chosenOptions, selection.Map, providerFactory);
            created.InitializeFirstTownContent();
            var entry = catalog.Add(name, created.ExportState());
            SelectCore(entry, created.ExportState(), discardFailedCreation: true);
            if (logger.IsEnabled(LogLevel.Information))
            {
                var sizeName = geography.Size.ToString().ToLowerInvariant();
                WorldSelectionTelemetry.Created(logger, entry.Id, sizeName);
            }
            return entry;
        }
    }

    public OwnerModelSettingsPreview PreviewModels(string id)
    {
        lock (gate)
        {
            var entry = catalog.Capture().Worlds.SingleOrDefault(world => world.Id == id)
                ?? throw new FileNotFoundException("The selected world does not exist.");
            return WorldModelSettings.Preview(id,
                entry.Id == catalog.Active().Id ? runtime.ExportState() : catalog.Read(id),
                entry.Id == catalog.Active().Id ? providers.CaptureRuntimeConfiguration().Assignments ?? [] : entry.Assignments,
                runtime, providers, targetKind: "world");
        }
    }

    public CatalogWorld Select(string id, string? modelSettingsApproval = null)
    {
        lock (gate)
        {
            RequirePaused();
            var entry = catalog.Capture().Worlds.SingleOrDefault(world => world.Id == id)
                ?? throw new FileNotFoundException("The selected world does not exist.");
            if (entry.Id == catalog.Capture().ActiveId) return entry;
            var assessed = Assess(entry, out _);
            if (assessed.Compatibility is "incompatible" or OtherVersion)
            {
                WorldSelectionTelemetry.Failed(logger, entry.Id, "incompatible_checkpoint");
                throw new InvalidDataException(assessed.CompatibilityReason);
            }
            WorldModelSettings.RequireApproval(PreviewModels(id), modelSettingsApproval);
            SelectCore(entry, catalog.Read(id));
            WorldSelectionTelemetry.Selected(logger, entry.Id);
            return entry;
        }
    }

    private void SelectCore(CatalogWorld entry, WorldRuntimeState target, bool discardFailedCreation = false)
    {
        var old = runtime.ExportState();
        var oldEntry = catalog.Active();
        var oldAssignments = providers.CaptureRuntimeConfiguration().Assignments ?? [];
        var oldAutosave = autosave.Capture();
        var switched = false;
        var checkpointSaved = false;
        var assignmentsChanged = false;
        var autosaveChanged = false;
        var policyChanged = false;
        try
        {
            stateFile.VerifyRequiredHistory(target);
            catalog.ArchiveActive(old, oldAssignments, oldAutosave);
            runtime.SwitchPausedWorld(target);
            switched = true;
            stateFile.Save(runtime);
            checkpointSaved = true;
            providers.RestoreWorldAssignments(entry.Assignments);
            assignmentsChanged = true;
            autosave.SelectWorld(entry.WorldId, entry.AutosaveSettings);
            autosaveChanged = true;
            jevPolicy.Initialize(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
            policyChanged = true;
            catalog.Select(entry.Id);
        }
        catch
        {
            WorldSelectionTelemetry.Failed(logger, entry.Id, "commit_failed");
            if (switched) runtime.SwitchPausedWorld(old);
            if (checkpointSaved) stateFile.Save(runtime);
            if (assignmentsChanged) providers.RestoreWorldAssignments(oldAssignments);
            if (autosaveChanged) autosave.SelectWorld(oldEntry.WorldId, oldAutosave);
            if (policyChanged) jevPolicy.Initialize(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
            catalog.Select(oldEntry.Id);
            // Only discard the new entry after rollback succeeds. A second
            // failure must retain its checkpoint and catalog recovery evidence.
            if (discardFailedCreation) catalog.Delete(entry.Id, entry.WorldId, _ => { });
            throw;
        }
    }

    private void RequirePaused()
    {
        saves.RequireNoPendingLoad();
        if (!runtime.Society.IsPaused)
            throw new InvalidOperationException("Pause the current world before switching worlds.");
    }
}
