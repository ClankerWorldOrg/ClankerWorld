using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapSaves(WebApplication app)
    {
        app.MapPost("/api/v1/owner/saves/disk-status", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            SaveDiskSpaceMonitor disk) =>
        {
            if (!IsControl(request, "save-disk-status"))
                return Results.BadRequest(new { error = "A save-space status action is required." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/disk-status",
                OwnerHttpBinding.EmptyPayload("save-disk-status"));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            return Results.Ok(disk.Capture());
        });

        app.MapPost("/api/v1/owner/saves/list", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            ManualWorldSaveStore saves,
            WorldRuntime runtime) =>
        {
            if (!IsControl(request, "list-saves"))
                return Results.BadRequest(new { error = "A save-list action is required." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/list",
                OwnerHttpBinding.EmptyPayload("list-saves"));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            return Results.Ok(saves.List(runtime.Society.WorldId));
        });

        app.MapPost("/api/v1/owner/saves/timeline", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            ManualWorldSaveStore saves,
            WorldRuntime runtime) =>
        {
            if (!IsControl(request, "save-timeline"))
                return Results.BadRequest(new { error = "A save-timeline action is required." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/timeline",
                OwnerHttpBinding.EmptyPayload("save-timeline"));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            return Results.Ok(saves.CurrentPosition(runtime.Society.WorldId));
        });

        app.MapPost("/api/v1/owner/saves/autosave/status", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            WorldAutosaveStore autosave) =>
        {
            if (!IsControl(request, "autosave-status"))
                return Results.BadRequest(new { error = "An autosave-status action is required." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/autosave/status",
                OwnerHttpBinding.EmptyPayload("autosave-status"));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            return Results.Ok(autosave.Capture());
        });

        app.MapPost("/api/v1/owner/saves/autosave/configure", (
            OwnerSignedHttpRequest<OwnerAutosaveConfigurationAction> request,
            WorldAutosaveStore autosave,
            ManualWorldSaveStore saves,
            WorldRuntime runtime,
            ProviderConfigurationStore providers,
            ILogger<WorldRuntimeService> logger) =>
        {
            var action = request.Action;
            if (action.WorldId != autosave.Capture().WorldId)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "autosave_configure", "world_changed");
                return Results.Conflict(new { error = "The world changed. Reopen World Settings before changing autosaves." });
            }
            if (!runtime.Society.IsPaused)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "autosave_configure", "not_paused");
                return Results.Conflict(new { error = "Pause the world before changing autosave settings." });
            }
            try
            {
                var updated = autosave.Configure(action.Enabled, action.IntervalMinutes, action.RotationCount);
                saves.KeepNewestAutosaves(Math.Max(1, updated.RotationCount), providers, worldId: updated.WorldId);
                ManualWorldSaveTelemetry.AutosaveConfigured(logger, updated.Enabled,
                    updated.IntervalMinutes, updated.RotationCount, runtime.WorldTick);
                return Results.Ok(updated);
            }
            catch (ArgumentException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "autosave_configure", "invalid_option");
                return Results.BadRequest(new { error = "Choose an offered interval and rotation count." });
            }

        }).ForWorld<OwnerAutosaveConfigurationAction>(action => action.WorldId,
            OwnerHttpBinding.AutosaveConfigurationPayload,
            (context, _) =>
            {
                ManualWorldSaveTelemetry.Rejected(context.RequestServices.GetRequiredService<ILogger<WorldRuntimeService>>(), "autosave_configure", "world_changed");
                return Results.Conflict(new { error = "The world changed. Reopen World Settings before changing autosaves." });
            },
            invalidPayload: (_, _) => Results.BadRequest(new { error = "Read this world's autosave settings before changing them." }),
            missingAction: () => Results.BadRequest(new { error = "Autosave settings are required." }));

        app.MapPost("/api/v1/owner/saves/create", (
            OwnerSignedHttpRequest<OwnerManualSaveAction> request,
            OwnerRequestAuthorizer authorizer,
            ManualWorldSaveStore saves,
            WorldRuntime runtime,
            ProviderConfigurationStore providers,
            WorldAutosaveStore autosave,
            ILogger<WorldRuntimeService> logger) =>
        {
            if (request?.Action is not { Operation: "create" } action)
                return Results.BadRequest(new { error = "A named save action is required." });
            string payload;
            try { payload = OwnerHttpBinding.ManualSavePayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A save name is required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/create", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try
            {
                ManualWorldSave saved;
                lock (providers.WorldMutationGate)
                    saved = saves.Create(action.Value, runtime,
                        providers.CaptureRuntimeConfiguration().Assignments ?? [], autosave.Capture());
                ManualWorldSaveTelemetry.Created(logger, saved.Id, saved.WorldTick);
                return Results.Ok(saved);
            }
            catch (ArgumentException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "create", "invalid_name");
                return Results.BadRequest(new { error = "Save name must be 1–80 printable characters." });
            }
            catch (InvalidOperationException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "create", "not_paused");
                return Results.Conflict(new { error = "Pause the world before saving." });
            }
        });

        app.MapPost("/api/v1/owner/saves/overwrite", (
            OwnerSignedHttpRequest<OwnerManualSaveAction> request,
            OwnerRequestAuthorizer authorizer,
            ManualWorldSaveStore saves,
            WorldRuntime runtime,
            ProviderConfigurationStore providers,
            WorldAutosaveStore autosave,
            ILogger<WorldRuntimeService> logger) =>
        {
            if (request?.Action is not { Operation: "overwrite" } action)
                return Results.BadRequest(new { error = "A selected save ID is required." });
            string payload;
            try { payload = OwnerHttpBinding.ManualSavePayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A selected save ID is required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/overwrite", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try
            {
                ManualSaveOverwriteReceipt result;
                lock (providers.WorldMutationGate)
                    result = saves.Overwrite(action.Value, runtime,
                        providers.CaptureRuntimeConfiguration().Assignments ?? [], autosave.Capture());
                ManualWorldSaveTelemetry.Overwritten(logger, result.Saved.Id, result.BackupId,
                    result.Saved.WorldTick);
                return Results.Ok(result);
            }
            catch (FileNotFoundException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "overwrite", "missing");
                return Results.NotFound(new { error = "The selected save no longer exists." });
            }
            catch (ArgumentException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "overwrite", "invalid_id");
                return Results.BadRequest(new { error = "Invalid save ID." });
            }
            catch (InvalidDataException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "overwrite", "invalid_checkpoint");
                return Results.Conflict(new { error = "The selected checkpoint is invalid and was preserved." });
            }
            catch (InvalidOperationException)
            {
                ManualWorldSaveTelemetry.Rejected(logger, "overwrite", "not_paused_or_wrong_world");
                return Results.Conflict(new { error = "Pause the world and select one of its named saves." });
            }
        });

        app.MapPost("/api/v1/owner/saves/load", (
            OwnerSignedHttpRequest<OwnerManualSaveAction> request,
            OwnerRequestAuthorizer authorizer,
            ManualWorldSaveStore saves,
            WorldRuntime runtime,
            WorldStateFile stateFile,
            ProviderConfigurationStore providers,
            WorldAutosaveStore autosave,
            WorldJevPolicy jevPolicy,
            IHostApplicationLifetime lifetime,
            ILogger<WorldRuntimeService> logger) =>
        {
            if (request?.Action is not { Operation: "load" or "inspect-save-models" } action)
                return Results.BadRequest(new { error = "A save-load action is required." });
            string payload;
            try { payload = OwnerHttpBinding.ManualSavePayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A save ID is required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/saves/load", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            lock (providers.WorldMutationGate)
            {
                if (action.Operation == "load" && !runtime.Society.IsPaused)
                {
                    ManualWorldSaveTelemetry.Rejected(logger, "load", "not_paused");
                    return Results.Conflict(new { error = "Pause the world before loading." });
                }
                try
                {
                    var committed = saves.ReadCommitted(action.Value);
                    var checkpoint = committed.Checkpoint;
                    stateFile.VerifyRequiredHistory(checkpoint);
                    var current = runtime.ExportState();
                    var modelPreview = WorldModelSettings.Preview(action.Value, checkpoint,
                        committed.Assignments, runtime, providers);
                    if (action.Operation == "inspect-save-models") return Results.Ok(modelPreview);
                    WorldModelSettings.RequireApproval(modelPreview, action.ModelSettingsApproval);
                    if (!string.Equals(checkpoint.WorldSeed, current.WorldSeed, StringComparison.Ordinal) ||
                        !string.Equals(checkpoint.Society.Society.WorldId, current.Society.Society.WorldId, StringComparison.Ordinal))
                    {
                        ManualWorldSaveTelemetry.Rejected(logger, "load", "different_world");
                        return Results.Conflict(new { error = "This save belongs to a different world." });
                    }
                    // Loading must never lose the world being left. Its unsaved progress
                    // becomes a recovery copy on its own branch, unless the world is
                    // still exactly the save it continues from.
                    var currentAssignments = providers.CaptureRuntimeConfiguration().Assignments ?? [];
                    var currentAutosave = autosave.Capture();
                    var backup = saves.FindUnchangedSave(runtime, currentAssignments, currentAutosave)
                        ?? saves.CreateLoadRecovery(runtime, currentAssignments, currentAutosave);
                    try { saves.Load(action.Value, backup.Id, runtime, stateFile, providers, autosave); }
                    catch
                    {
                        // An unfinished rollback still owns this world. Stop
                        // serving it until startup can finish the durable intent.
                        if (saves.HasPendingLoad) lifetime.StopApplication();
                        throw;
                    }
                    jevPolicy.Initialize(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
                    ManualWorldSaveTelemetry.Loaded(logger, action.Value, backup.Id, runtime.WorldTick);
                    return Results.Ok(new { loadedId = action.Value, backupId = backup.Id, worldTick = runtime.WorldTick });
                }
                catch (ModelSettingsConfirmationRequiredException exception)
                { return Results.Conflict(new OwnerControlFailure("model_settings_confirmation_required", exception.Message)); }
                catch (FileNotFoundException)
                {
                    ManualWorldSaveTelemetry.Rejected(logger, "load", "missing");
                    return Results.NotFound(new { error = "The manual save no longer exists." });
                }
                catch (ArgumentException)
                {
                    ManualWorldSaveTelemetry.Rejected(logger, "load", "invalid_id");
                    return Results.BadRequest(new { error = "Invalid save ID." });
                }
                catch (InvalidDataException)
                {
                    ManualWorldSaveTelemetry.Rejected(logger, "load", "invalid_checkpoint_or_credential");
                    return Results.Conflict(new { error = "The save is invalid or its credential slot is unavailable." });
                }
                catch (InvalidOperationException)
                {
                    ManualWorldSaveTelemetry.Rejected(logger, "load", "not_paused");
                    return Results.Conflict(new { error = "Pause the world before loading." });
                }
            }
        });
    }

    private static void MapWorlds(WebApplication app)
    {
        app.MapPost("/api/v1/owner/worlds/list", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            HttpContext context) =>
        {
            if (!IsControl(request, "list-worlds"))
                return Results.BadRequest(new { error = "A world-list action is required." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/worlds/list",
                OwnerHttpBinding.EmptyPayload("list-worlds"));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try
            {
                return Results.Ok(services.GetRequiredService<WorldSelectionCoordinator>().List(context.RequestAborted));
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The client left Load World; listing changed nothing and nobody reads this reply.
                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
        });

        app.MapPost("/api/v1/owner/worlds/saves", (
            OwnerSignedHttpRequest<OwnerManualSaveAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (request?.Action is not { Operation: "world-saves" } action)
                return Results.BadRequest(new { error = "A world save-history request is required." });
            string payload;
            try { payload = OwnerHttpBinding.ManualSavePayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A world ID is required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/worlds/saves", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try { return Results.Ok(services.GetRequiredService<WorldSelectionCoordinator>().ReadSaveHistory(action.Value)); }
            catch (FileNotFoundException) { return Results.NotFound(new { error = "The selected world no longer exists." }); }
            catch (InvalidDataException) { return Results.Conflict(new { error = "This world's history cannot be read. Your saves are safe." }); }
        });

        app.MapPost("/api/v1/owner/worlds/create", (
            OwnerSignedHttpRequest<OwnerWorldCreationAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (request?.Action is not { } action)
                return Results.BadRequest(new { error = "World options are required." });
            string creationPayload;
            try { creationPayload = OwnerHttpBinding.WorldCreationPayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "World name, seed, and size are required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/worlds/create",
                creationPayload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            if (!HasCreateIdentity(action))
                return Results.BadRequest(new { error = "Preview the map before creating it." });
            if (!TryWorldOptions(action, out var options))
                return Results.BadRequest(new { error = "World seed, size, or water choice is invalid." });
            try
            {
                var entry = services.GetRequiredService<WorldSelectionCoordinator>().Create(action.Name, options!,
                    action.CandidateAttempt!.Value, action.ExpectedManifestDigest!, action.ExpectedMapLayersDigest!,
                    action.AcceptUnmetTargets);
                return Results.Ok(entry);
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            catch (GeographyClearingUnavailableException)
            {
                return Results.Conflict(new OwnerControlFailure("no_playable_candidate",
                    "There is no room for a first Town with these settings."));
            }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
        });

        app.MapPost("/api/v1/owner/worlds/preview", (
            OwnerSignedHttpRequest<OwnerWorldCreationAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (request?.Action is not { } action)
                return Results.BadRequest(new { error = "World options are required." });
            string payload;
            try { payload = OwnerHttpBinding.WorldCreationPayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "World name, seed, and size are required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/worlds/preview", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            if (!HasNoPreviewIdentity(action))
                return Results.BadRequest(new { error = "Preview requests cannot carry a candidate acceptance." });
            if (!TryWorldOptions(action, out var options))
                return Results.BadRequest(new { error = "World seed, size, or water choice is invalid." });
            try { return Results.Ok(services.GetRequiredService<WorldSelectionCoordinator>().Preview(options!)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            catch (GeographyClearingUnavailableException)
            {
                return Results.Conflict(new OwnerControlFailure("no_playable_candidate",
                    "There is no room for a first Town with these settings."));
            }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
        });

        app.MapPost("/api/v1/owner/worlds/select", (
            OwnerSignedHttpRequest<OwnerManualSaveAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (request?.Action is not { Operation: "select-world" or "inspect-world-models" } action)
                return Results.BadRequest(new { error = "A world selection is required." });
            string payload;
            try { payload = OwnerHttpBinding.ManualSavePayload(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A world ID is required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/worlds/select",
                payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try
            {
                var worlds = services.GetRequiredService<WorldSelectionCoordinator>();
                return action.Operation == "inspect-world-models" ? Results.Ok(worlds.PreviewModels(action.Value)) :
                    Results.Ok(worlds.Select(action.Value, action.ModelSettingsApproval));
            }
            catch (ModelSettingsConfirmationRequiredException exception)
            { return Results.Conflict(new OwnerControlFailure("model_settings_confirmation_required", exception.Message)); }
            catch (FileNotFoundException) { return Results.NotFound(new { error = "The selected world does not exist." }); }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (InvalidDataException) { return Results.Conflict(new { error = "The selected world is invalid." }); }
            catch (ArgumentException) { return Results.Conflict(new { error = "This world's model configuration is unavailable." }); }
        });
    }

    private static void MapStartupRecovery(WebApplication app)
    {
        app.MapPost("/api/v1/owner/recovery/status", (OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer, IServiceProvider services) =>
        {
            if (!IsControl(request, "recovery-status")) return Results.BadRequest();
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/recovery/status",
                OwnerHttpBinding.EmptyPayload("recovery-status"));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            return Results.Ok(services.GetRequiredService<WorldStartupRecovery>().Capture());
        });

        app.MapPost("/api/v1/owner/recovery/restore", (OwnerSignedHttpRequest<OwnerManualSaveAction> request,
            OwnerRequestAuthorizer authorizer, OwnerAuthorityStore authority, IServiceProvider services) =>
        {
            if (request?.Action is not { Operation: "recover", Value: { Length: 32 } id } action ||
                !id.All(char.IsAsciiHexDigit)) return Results.BadRequest();
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/recovery/restore",
                OwnerHttpBinding.ManualSavePayload(action));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try
            {
                var recovery = services.GetRequiredService<WorldStartupRecovery>();
                if (!recovery.Pending || recovery.Capture().Autosave?.Id != id)
                    return Results.Conflict(new { error = "Read recovery again and choose the offered autosave." });
                return Results.Ok(recovery.Recover(id, services.GetRequiredService<WorldAutosaveStore>(), authority));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or
                UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                return Results.Conflict(new { error = "Recovery could not finish. Your saved files are kept. Check storage and read recovery again." });
            }
        });
    }

    private static void MapDeletion(WebApplication app)
    {
        app.MapPost("/api/v1/owner/delete", (
            OwnerSignedHttpRequest<OwnerDeletionAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            WorldRuntime runtime,
            WorldStateFile stateFile,
            ManualWorldSaveStore saves,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("WorldDeletion");
            if (request?.Action is not { } action || action.Kind is not ("save" or "world"))
                return Results.BadRequest(new { error = "Select a save or world to delete." });
            string payload;
            try { payload = OwnerHttpBinding.Deletion(action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "The deletion target is invalid." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/delete", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            lock (providers.WorldMutationGate)
            {
                if (!runtime.Society.IsPaused)
                    return Results.Conflict(new { error = "Pause the world before deleting saves or worlds." });
                try
                {
                    if (action.Kind == "save")
                    {
                        if (action.WorldId != runtime.Society.WorldId || action.ExpectedCreatedUtc is null)
                            return Results.Conflict(new { error = "The world changed. Reopen the save list before deleting." });
                        saves.Delete(action.Id, action.WorldId, action.ExpectedCreatedUtc.Value);
                    }
                    else
                    {
                        if (action.WorldId == runtime.Society.WorldId)
                            return Results.Conflict(new { error = "Open or create another world before deleting this one." });
                        services.GetRequiredService<WorldCatalogStore>().Delete(action.Id, action.WorldId,
                            saves.DeleteWorldSnapshots);
                    }
                }
                catch (FileNotFoundException)
                {
                    return Results.NotFound(new { error = "The selected item no longer exists. Refresh the list." });
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest(new { error = "The deletion target is invalid." });
                }
                catch (InvalidOperationException)
                {
                    return Results.Conflict(new { error = "The selection changed or is active. Reopen the list before deleting." });
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
                {
                    // Do not claim success: the durable intent may have committed,
                    // but cleanup was interrupted. Repeating the same target is safe.
                    ManualWorldSaveTelemetry.DeletionPending(logger,
                        action.Kind, exception.GetType().Name);
                    return Results.Conflict(new { error = "Deletion could not finish. Refresh the list; any pending cleanup will be retried when the host starts." });
                }
                var complete = true;
                try { stateFile.ReclaimUnreferencedHistory(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
                {
                    complete = false;
                    ManualWorldSaveTelemetry.DeletionPending(logger,
                        "history", exception.GetType().Name);
                }
                ManualWorldSaveTelemetry.Deleted(logger, action.Kind, complete);
                return Results.Ok(new OwnerDeletionReceipt(action.Id, complete));
            }
        });
    }

    private static void MapRecoveryCleanup(WebApplication app)
    {
        app.MapPost("/api/v1/owner/saves/recovery-cleanup", (
            OwnerSignedHttpRequest<OwnerRecoveryCleanupAction> request,
            ProviderConfigurationStore providers,
            WorldRuntime runtime, WorldStateFile stateFile, ManualWorldSaveStore saves) =>
        {
            var action = request.Action;
            if (!runtime.Society.IsPaused)
                return Results.Conflict(new { error = "Pause this world and reopen its recovery preview." });
            try
            {
                if (action.Operation == "preview")
                    return Results.Ok(saves.PreviewRecoveryCleanup(action.WorldId, action.KeepCount, stateFile));
                var removed = saves.CleanRecoveryHistory(action.WorldId, action.KeepCount, action.ExpectedDigest!, stateFile);
                // Only the listed recovery checkpoints are removed. Shared
                // archives, manual saves and migration originals stay in place.
                return Results.Ok(new OwnerRecoveryCleanupReceipt(removed, true));
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Recovery history changed. Preview it again before deleting." });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                return Results.Conflict(new { error = "Cleanup could not finish. Refresh the saves; pending deletion will be retried when the host starts." });
            }

        }).ForWorld<OwnerRecoveryCleanupAction>(action => action.WorldId,
            OwnerHttpBinding.RecoveryCleanup,
            (_, _) => Results.Conflict(new { error = "Pause this world and reopen its recovery preview." }),
            validate: action => action.Operation is not ("preview" or "apply") || action.KeepCount is < 1 or > 10 ||
                string.IsNullOrWhiteSpace(action.WorldId) || (action.Operation == "preview" ? action.ExpectedDigest is not null :
                    action.ExpectedDigest is not { Length: 64 } || !action.ExpectedDigest.All(char.IsAsciiHexDigit))
                ? Results.BadRequest(new { error = "Choose a recovery count and preview before confirming cleanup." }) : null,
            missingAction: () => Results.BadRequest(new { error = "Choose a recovery count and preview before confirming cleanup." }));
    }
}
