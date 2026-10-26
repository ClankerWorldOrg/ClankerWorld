using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapRuntimeControl(WebApplication app)
    {
        MapDeveloperEdits(app);

        app.MapPost("/api/v1/owner/control/aging-override", (
            OwnerSignedHttpRequest<OwnerAgingOverrideAction> request,
            IServiceProvider services,
            OwnerWorldObservationStore observations,
            ILogger<WorldRuntimeService> logger) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            bool changed;
            try
            {
                changed = runtime.SetAgingOverride(action.Rate);
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Pause the world before changing life pace." });
            }
            // A retry must also persist a prior in-memory change whose first save failed.
            services.GetRequiredService<WorldStateFile>().Save(runtime);
            if (changed) OwnerAgingOverrideTelemetry.Changed(logger, runtime.WorldTick, action.Rate);
            return Results.Ok(OwnerControlReceipt.From("aging_override", changed, observations.GetSnapshot()));

        }).ForWorld<OwnerAgingOverrideAction>(action => action.WorldId,
            OwnerHttpBinding.AgingOverridePayload,
            (_, _) => Results.Conflict(new { error = "The world changed. Reopen World Settings before changing life pace." }),
            validate: action => action.Rate is not (1 or 365 or 1_460) ? InvalidAction("Life pace must be 1, 365 or 1460.", "action.rate") : null,
            invalidPayload: (_, _) => Results.BadRequest(new { error = "Read the current world's settings before changing life pace." }),
            missingAction: () => InvalidAction("Life pace must be 1, 365 or 1460.", "action.rate"));

        app.MapPost("/api/v1/owner/control/jev-assistance", (
            OwnerSignedHttpRequest<OwnerJevAssistanceAction> request,
            IServiceProvider services,
            OwnerWorldObservationStore observations,
            WorldJevPolicy jevPolicy,
            ILogger<WorldRuntimeService> logger) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            bool changed;
            try
            {
                changed = runtime.SetJevEnabled(action.Enabled);
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Pause the world before changing Jev assistance." });
            }
            jevPolicy.Set(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
            services.GetRequiredService<WorldStateFile>().Save(runtime);
            if (changed) OwnerJevAssistanceTelemetry.Changed(logger, runtime.WorldTick, runtime.JevEnabled);
            return Results.Ok(OwnerControlReceipt.From("jev_assistance", changed, observations.GetSnapshot()));

        }).ForWorld<OwnerJevAssistanceAction>(action => action.WorldId,
            OwnerHttpBinding.JevAssistancePayload,
            (_, _) => Results.Conflict(new { error = "The world changed. Reopen World Settings before changing Jev assistance." }),
            invalidPayload: (_, _) => Results.BadRequest(new { error = "Read the current world's settings before changing Jev assistance." }),
            missingAction: () => InvalidAction("A Jev setting is required."));

        app.MapPost("/api/v1/owner/control/routine-helper", (
            OwnerSignedHttpRequest<OwnerRoutineHelperAction> request,
            OwnerRequestAuthorizer authorizer, IServiceProvider services,
            OwnerWorldObservationStore observations, WorldJevPolicy jevPolicy, ILogger<WorldRuntimeService> logger) =>
        {
            var action = request.Action;
            var settings = new RoutineHelperSettings(action.Provider, action.Model, action.CredentialSlotId);
            var runtime = services.GetRequiredService<WorldRuntime>();
            if (settings.CredentialSlotId is { } slotId && services.GetRequiredService<ProviderConfigurationStore>()
                .CaptureRuntimeConfiguration().CredentialSlots?.Any(slot => slot.Id == slotId &&
                    slot.Provider == PlayerDecisionProviders.HelperCredentialProvider(settings.Provider)) != true)
                return Results.Conflict(new
                {
                    error = settings.Provider == PlayerDecisionProviders.Jev
                    ? "Choose an available saved TypeSafe key."
                    : "Choose an available saved OpenAI key."
                });
            bool changed;
            try { changed = runtime.SetRoutineHelper(settings, action.WorldId); }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
            jevPolicy.Set(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
            // A no-op retry still persists an earlier change whose save failed.
            services.GetRequiredService<WorldStateFile>().Save(runtime);
            if (changed) OwnerJevAssistanceTelemetry.HelperChanged(logger, runtime.WorldTick, settings.Provider, settings.Model);
            return Results.Ok(OwnerControlReceipt.From("routine_helper", changed, observations.GetSnapshot()));

        }).ForWorld<OwnerRoutineHelperAction>(action => action.WorldId,
            OwnerActionPayload.RoutineHelper,
            (_, _) => Results.Conflict(new { error = "The active world changed. Open World Settings again." }),
            validate: action =>
            {
                if (string.IsNullOrWhiteSpace(action.WorldId) || action.WorldId.Length > 128 || action.WorldId.Any(char.IsControl))
                    return InvalidAction("A world and routine helper are required.");
                try { new RoutineHelperSettings(action.Provider, action.Model, action.CredentialSlotId).Validate(); }
                catch (ArgumentException exception) { return InvalidAction(exception.Message); }
                return null;
            },
            missingAction: () => InvalidAction("A world and routine helper are required."));

        app.MapPost("/api/v1/owner/control/pause", (
            OwnerSignedHttpRequest<OwnerWorldControlAction> request,
            IServiceProvider services) =>
        {
            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            var wasPaused = worldRuntime.Society.IsPaused;
            worldRuntime.Pause();
            var changed = !wasPaused && worldRuntime.Society.IsPaused;
            // A prior failed write may already have changed memory. Even a no-op
            // retry must durably acknowledge the requested pause.
            worldStateFile.Save(worldRuntime);

            return Results.Ok(OwnerControlReceipt.From("pause", changed, worldRuntime.ExportState()));
        }).ForWorldControl("pause");

        app.MapPost("/api/v1/owner/control/resume", (
            OwnerSignedHttpRequest<OwnerWorldControlAction> request,
            IServiceProvider services) =>
        {
            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            if (worldRuntime.FounderSetup is { Started: false })
                return Results.Conflict(new { message = "Place four configured founders, then select Start World." });
            if (ModelCallRefusal(services.GetRequiredService<ProviderUsageStore>(), "resume") is { } refusal)
                return refusal;
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            var wasPaused = worldRuntime.Society.IsPaused;
            // Persist every acknowledgement, including an already-running
            // retry. A paused live world resumes only after the write succeeds.
            worldStateFile.Save(worldRuntime, resumeOnSuccess: true);
            var changed = wasPaused && !worldRuntime.Society.IsPaused;

            return Results.Ok(OwnerControlReceipt.From("resume", changed, worldRuntime.ExportState()));
        }).ForWorldControl("resume");
    }

    private static IResult? ModelCallRefusal(ProviderUsageStore usage, string nextAction)
    {
        var status = usage.Capture();
        if (status.AccountingError is { } accountingError)
            return Results.Conflict(new { message = accountingError });
        return status.LimitReached
            ? Results.Conflict(new { message = $"The model-call limit is reached. Raise the limit or allow more calls in Settings → Game, then {nextAction}." })
            : null;
    }

    private static void MapInstructions(WebApplication app)
    {
        app.MapPost("/api/v1/owner/instructions", (
            OwnerSignedHttpRequest<OwnerInstructionAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var kind = action.Kind.Trim().Equals("order", StringComparison.OrdinalIgnoreCase)
                ? OwnerInstructionKind.Order : OwnerInstructionKind.Suggestion;
            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                // Selection and load use this same gate. Keep the world check,
                // mutation and durable save together so selection cannot retarget a retry.
                var receipt = worldRuntime.SubmitInstruction(new OwnerInstructionRequest(
                    request.Action.IdempotencyKey,
                    DerivedId.Create($"owner-device:{request.DeviceId}"),
                    request.Action.TargetAgentId,
                    kind,
                request.Action.Text,
                request.Action.Queue));
                worldStateFile.Save(worldRuntime);
                return Results.Ok(receipt);

            }
            catch (ArgumentException exception)
            {
                return InvalidAction(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("idempotency_conflict", exception.Message));
            }


        }).ForWorld<OwnerInstructionAction>(action => action.WorldId,
            OwnerActionPayload.Instruction,
            (_, _) => Results.Conflict(new OwnerControlFailure("world_mismatch", "Return to the world where this instruction was sent before retrying.")),
            validate: action => !TryParseInstructionKind(action.Kind, out _) ? InvalidAction("Instruction kind must be suggestion or order.", "action.kind") : null,
            missingAction: () => InvalidAction("Instruction kind must be suggestion or order.", "action.kind"));

        app.MapPost("/api/v1/owner/orders/cancel", (
            OwnerSignedHttpRequest<OwnerOrderCancelAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var receipt = worldRuntime.CancelOrder(new OwnerOrderCancelRequest(
                    request.Action.IdempotencyKey,
                    DerivedId.Create($"owner-device:{request.DeviceId}"),
                    request.Action.WorldId,
                    request.Action.TargetAgentId,
                    request.Action.OrderId));
                worldStateFile.Save(worldRuntime);
                return Results.Ok(receipt);

            }
            catch (ArgumentException exception)
            {
                return InvalidAction(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("idempotency_conflict", exception.Message));
            }
        }).ForWorld<OwnerOrderCancelAction>(action => action.WorldId,
            OwnerActionPayload.OrderCancel,
            (_, _) => Results.Conflict(new OwnerControlFailure("world_mismatch", "Return to the world where this order was sent before cancelling it.")),
            missingAction: () => InvalidAction("An order cancellation is required."));
    }

    private static void MapSetup(WebApplication app)
    {
        var founderSetupGate = app.Services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate;

        app.MapPost("/api/v1/owner/town/first-layout", (
            OwnerSignedHttpRequest<OwnerFirstTownLayoutAction> request,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            var before = runtime.ExportState();
            try
            {
                var plan = runtime.AcceptFirstTownLayout(new GridPoint(action.X, action.Y));
                try { services.GetRequiredService<WorldStateFile>().Save(runtime); }
                catch
                {
                    runtime.SwitchPausedWorld(before);
                    throw;
                }
                TownTelemetry.LayoutAccepted(loggerFactory.CreateLogger("ClankerWorld.Town"),
                    runtime.WorldTick, before.Towns?.Any(town => town.OriginSite is not null) == true
                        ? "redone" : "accepted", action.X, action.Y, plan.Buildings.Count, plan.RoadTiles.Count);
                return Results.Ok(new OwnerFirstTownLayoutReceipt(action.X, action.Y,
                    plan.Buildings.Count, plan.RoadTiles.Count));
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
        }).ForWorld<OwnerFirstTownLayoutAction>(action => action.WorldId, OwnerActionPayload.FirstTownLayout,
            (_, _) => Results.Conflict(new { error = "The world changed. Reopen it before choosing a Town site." }));

        app.MapPost("/api/v1/owner/founders/place", (
            OwnerSignedHttpRequest<OwnerFounderPlacementAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            ILoggerFactory loggerFactory,
            IServiceProvider services) =>
        {
            if (request?.Action is not { Cognition: { } cognition } action)
                return InvalidAction("A founder placement is required in a world.");
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/founders/place",
                OwnerHttpBinding.FounderPlacementPayload, out _) is { } authorizationFailure)
                return authorizationFailure;
            if (cognition.AgentId != action.FounderId || cognition.Role != PlayerDecisionProviders.PersonalRole ||
                cognition.ForgetCredential || !PlayerDecisionProviders.IsModelProvider(cognition.Provider))
                return InvalidAction("Choose one personal hosted model for this founder.", "cognition");

            lock (founderSetupGate)
            {
                try
                {
                    var runtime = services.GetRequiredService<WorldRuntime>();
                    var position = new GridPoint(action.X, action.Y);
                    runtime.ValidateFounderPlacement(action.FounderId, position);
                    var before = runtime.ExportState();
                    string household = "";
                    try
                    {
                        providers.ConfigureWithCommit(cognition, () =>
                        {
                            household = runtime.PlaceFounder(action.FounderId, position);
                            services.GetRequiredService<WorldStateFile>().Save(runtime);
                        });
                    }
                    catch
                    {
                        runtime.SwitchPausedWorld(before);
                        throw;
                    }
                    var placed = runtime.FounderSetup!.FounderIds.Count;
                    var town = runtime.Towns.Single(item => item.Id == TownBorderRules.FirstTownId);
                    TownTelemetry.Transition(loggerFactory.CreateLogger("ClankerWorld.Town"), runtime.WorldTick,
                        town.Id, TownTransitionKind.ResidentJoined, town.ResidentIds.Count,
                        town.AssignedBuildingIds.Count, town.BorderTiles.Count);
                    return Results.Ok(new OwnerFounderPlacementReceipt(action.FounderId, household, placed, WorldRuntime.RequiredFounders));
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    return InvalidAction(exception.Message);
                }
            }
        });

        app.MapPost("/api/v1/owner/founders/move", (
            OwnerSignedHttpRequest<OwnerFounderMoveAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            if (request?.Action is not { } action)
                return Results.BadRequest(new { error = "Choose a placed founder and destination." });
            string payload;
            try { payload = OwnerHttpBinding.FounderMovePayload(action); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/founders/move", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            lock (founderSetupGate)
            {
                var runtime = services.GetRequiredService<WorldRuntime>();
                var before = runtime.ExportState();
                try
                {
                    var changed = runtime.MoveFounder(action.FounderId, new GridPoint(action.X, action.Y));
                    if (changed)
                    {
                        try { services.GetRequiredService<WorldStateFile>().Save(runtime); }
                        catch
                        {
                            runtime.SwitchPausedWorld(before);
                            throw;
                        }
                        TownTelemetry.FounderMoved(loggerFactory.CreateLogger("ClankerWorld.Town"),
                            runtime.WorldTick, action.FounderId, action.X, action.Y);
                    }
                    return Results.Ok(new OwnerFounderMoveReceipt(action.FounderId, action.X, action.Y, changed));
                }
                catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
                catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
            }
        });

        app.MapPost("/api/v1/owner/founders/undo", (
            OwnerSignedHttpRequest<OwnerFounderUndoAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            if (request?.Action is not { } action)
                return Results.BadRequest(new { error = "Choose the last placed founder to undo." });
            string payload;
            try { payload = OwnerHttpBinding.FounderUndoPayload(action); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/founders/undo", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            lock (founderSetupGate)
            {
                var runtime = services.GetRequiredService<WorldRuntime>();
                var stateFile = services.GetRequiredService<WorldStateFile>();
                var before = runtime.ExportState();
                var assignments = providers.CaptureRuntimeConfiguration().Assignments ?? [];
                var worldMutated = false;
                var worldSaved = false;
                var providerChanged = false;
                try
                {
                    var placed = runtime.UndoLastFounder(action.FounderId);
                    worldMutated = true;
                    stateFile.Save(runtime);
                    worldSaved = true;
                    providers.Configure(new OwnerProviderConfigurationAction("personal", "inherit", null, null,
                        false, action.FounderId));
                    providerChanged = true;
                    TownTelemetry.FounderUndone(loggerFactory.CreateLogger("ClankerWorld.Town"),
                        runtime.WorldTick, action.FounderId, placed);
                    return Results.Ok(new OwnerFounderUndoReceipt(action.FounderId, placed,
                        WorldRuntime.RequiredFounders));
                }
                catch (Exception exception)
                {
                    if (worldMutated)
                    {
                        runtime.SwitchPausedWorld(before);
                        if (worldSaved) stateFile.Save(runtime);
                    }
                    if (providerChanged) providers.RestoreWorldAssignments(assignments);
                    if (exception is ArgumentException) return Results.BadRequest(new { error = exception.Message });
                    if (exception is InvalidOperationException) return Results.Conflict(new { error = exception.Message });
                    throw;
                }
            }
        });

        app.MapPost("/api/v1/owner/agents/place", (
            OwnerSignedHttpRequest<OwnerAgentPlacementAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ClankerWorld.AgentPlacement");
            if (request?.Action is not { Cognition: { } cognition } action)
                return InvalidAction("An agent placement is required in a world.");
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/agents/place",
                OwnerHttpBinding.AgentPlacementPayload, out _) is { } authorizationFailure)
                return authorizationFailure;
            if (cognition.AgentId != action.AgentId || cognition.Role != PlayerDecisionProviders.PersonalRole ||
                cognition.ForgetCredential || !PlayerDecisionProviders.IsModelProvider(cognition.Provider))
                return InvalidAction("Choose one personal hosted model for this agent.", "cognition");

            lock (founderSetupGate)
            {
                try
                {
                    var runtime = services.GetRequiredService<WorldRuntime>();
                    var position = new GridPoint(action.X, action.Y);
                    var household = runtime.AddAgent(action.AgentId, position,
                        action.ExpectedHouseholdId, action.ExpectedTownId, () => providers.Configure(cognition));
                    services.GetRequiredService<WorldStateFile>().Save(runtime);
                    AgentPlacementLog.Placed(logger, action.AgentId, household, runtime.WorldTick, action.X, action.Y);
                    var town = runtime.Towns.SingleOrDefault(item => item.ResidentIds.Contains(action.AgentId, StringComparer.Ordinal));
                    TownTelemetry.Transition(logger, runtime.WorldTick, town?.Id ?? "none",
                        town is null ? TownTransitionKind.ResidentUnaffiliated : TownTransitionKind.ResidentJoined,
                        town?.ResidentIds.Count ?? 0, town?.AssignedBuildingIds.Count ?? 0, town?.BorderTiles.Count ?? 0);
                    return Results.Ok(new OwnerAgentPlacementReceipt(action.AgentId, household, town?.Id));
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    var loggedId = action.AgentId is { Length: 38 } id &&
                        id.StartsWith("agent:", StringComparison.Ordinal) &&
                        Guid.TryParseExact(id["agent:".Length..], "N", out _)
                            ? id : "<invalid-id>";
                    AgentPlacementLog.Rejected(logger, loggedId, exception.GetType().Name);
                    if (exception is AgentPlacementChangedException)
                        return Results.Conflict(new OwnerControlFailure("placement_changed", exception.Message));
                    return InvalidAction(exception.Message);
                }
            }
        });

        app.MapPost("/api/v1/owner/agents/rename", (
            OwnerSignedHttpRequest<OwnerAgentRenameAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            if (request?.Action is not { } action)
                return InvalidAction("Choose an agent and name.");
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/agents/rename",
                OwnerActionPayload.AgentRename, out _) is { } authorizationFailure)
                return authorizationFailure;
            try
            {
                var runtime = services.GetRequiredService<WorldRuntime>();
                var changed = runtime.RenameAgent(action.AgentId, action.Name);
                services.GetRequiredService<WorldStateFile>().Save(runtime);
                if (changed)
                {
                    var logger = loggerFactory.CreateLogger("ClankerWorld.AgentIdentity");
                    AgentPlacementLog.Renamed(logger, action.AgentId, runtime.WorldTick);
                }
                return Results.Ok(new OwnerAgentRenameReceipt(action.AgentId,
                    runtime.Society.GetAgent(action.AgentId).Name, changed));
            }
            catch (ClankerWorld.Simulation.Society.AgentNameTakenException exception)
            {
                return Results.Conflict(new OwnerControlFailure("name_taken", exception.Message));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return InvalidAction(exception.Message);
            }
        });

        app.MapPost("/api/v1/owner/control/start-world", (
            OwnerSignedHttpRequest<OwnerWorldControlAction> request,
            ProviderConfigurationStore providers,
            IServiceProvider services,
            OwnerWorldObservationStore observations,
            ILoggerFactory loggerFactory) =>
        {
            var runtime = services.GetRequiredService<WorldRuntime>();
            var setup = runtime.FounderSetup;
            if (setup is null || setup.Started || setup.FounderIds.Count != WorldRuntime.RequiredFounders)
                return Results.Conflict(new { message = "Place all four founders before starting the world." });
            var providerStatus = providers.CaptureStatus();
            var assignments = providerStatus.Assignments ?? [];
            foreach (var founderId in setup.FounderIds)
            {
                var personal = assignments.Where(item => item.AgentId == founderId).ToArray();
                if (personal.Length != 2 || !personal.Any(item => item.Role == PlayerDecisionProviders.RoutineRole) ||
                    !personal.Any(item => item.Role == PlayerDecisionProviders.PlanningRole) ||
                    personal.Any(item => !PlayerDecisionProviders.IsModelProvider(item.Provider)) ||
                    personal.Select(item => (item.Provider, item.Model, item.CredentialSlotId)).Distinct().Count() != 1 ||
                    personal.Any(item => item.CredentialSlotId is { } slot
                        ? !(providerStatus.CredentialSlots ?? []).Any(saved => saved.Id == slot && saved.Provider == item.Provider)
                        : !providerStatus.Providers.Any(option => option.Provider == item.Provider && option.HasCredential)))
                    return Results.Conflict(new { message = "Every founder needs one configured personal model and credential." });
            }
            if (ModelCallRefusal(services.GetRequiredService<ProviderUsageStore>(), "select Start World") is { } refusal)
                return refusal;
            var beforeStart = runtime.ExportState();
            try
            {
                // Do not permit background advancement before the start is durable.
                runtime.StartWorld(resume: false);
                services.GetRequiredService<WorldStateFile>().Save(runtime);
            }
            catch
            {
                runtime.SwitchPausedWorld(beforeStart);
                throw;
            }
            runtime.Resume();
            var town = runtime.Towns.Single(item => item.Id == TownBorderRules.FirstTownId);
            TownTelemetry.Transition(loggerFactory.CreateLogger("ClankerWorld.Town"), runtime.WorldTick,
                town.Id, TownTransitionKind.Founded, town.ResidentIds.Count,
                town.AssignedBuildingIds.Count, town.BorderTiles.Count);
            return Results.Ok(OwnerControlReceipt.From("start-world", true, observations.GetSnapshot()));
        }).ForWorldControl("start-world");
    }

    private static void MapProviders(WebApplication app)
    {
        app.MapPost("/api/v1/owner/providers/status", (
            OwnerSignedHttpRequest<OwnerProviderStatusAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers) =>
        {
            if (request?.Action is null)
            {
                return InvalidAction("A provider-status action is required.");
            }

            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/providers/status",
                _ => OwnerHttpBinding.ProviderStatusPayload(), out _) is { } authorizationFailure)
                return authorizationFailure;

            return Results.Ok(providers.CaptureStatus());
        });

        // Lists the chat models a key can use so the owner can pick one. The
        // key never leaves the host, and a pasted key is used without saving it.
        app.MapPost("/api/v1/owner/providers/models", async (
            OwnerSignedHttpRequest<OwnerProviderModelListAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderModelCatalog catalog,
            CancellationToken cancellationToken) =>
        {
            if (request?.Action is null)
                return InvalidAction("A model-list action is required.");
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/providers/models",
                OwnerActionPayload.ProviderModelList, out _) is { } authorizationFailure)
                return authorizationFailure;
            try
            {
                return Results.Ok(await catalog.ListAsync(request.Action, cancellationToken).ConfigureAwait(false));
            }
            catch (ArgumentException exception)
            {
                return InvalidAction(exception.Message);
            }
        });

        // A setup check is an explicit paid call through the game's actual
        // model or helper adapter. It never stores the supplied key or changes
        // provider configuration, and never retries with different options.
        app.MapPost("/api/v1/owner/providers/setup-check", async (
            OwnerSignedHttpRequest<OwnerProviderSetupCheckAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderSetupCheckService setupCheck,
            CancellationToken cancellationToken) =>
        {
            if (request?.Action is null)
                return InvalidAction("A provider setup-check action is required.");
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/providers/setup-check",
                OwnerHttpBinding.ProviderSetupCheckPayload, out _) is { } authorizationFailure)
                return authorizationFailure;
            return Results.Ok(await setupCheck.CheckAsync(request.Action, cancellationToken).ConfigureAwait(false));
        });

        app.MapPost("/api/v1/owner/usage/status", (
            OwnerSignedHttpRequest<OwnerUsageStatusAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderUsageStore usage) =>
        {
            if (request?.Action is null)
                return Results.BadRequest(new { error = "A usage-status action is required." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/usage/status",
                OwnerHttpBinding.UsageStatusPayload());
            return authorization.IsSuccess ? Results.Ok(usage.Capture()) : OwnerFailures.ToHttpResult(authorization.Failure);
        });

        app.MapPost("/api/v1/owner/usage/limit", (
            OwnerSignedHttpRequest<OwnerUsageLimitAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderUsageStore usage) =>
        {
            if (request?.Action is null)
                return Results.BadRequest(new { error = "A usage-limit action is required." });
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/usage/limit",
                OwnerActionPayload.UsageLimit, out _) is { } authorizationFailure)
                return authorizationFailure;
            try
            {
                var status = usage.Configure(request.Action);
                ProviderUsageTelemetry.LimitConfigured(app.Logger, status.AttemptLimit is not null,
                    status.AttemptLimit, status.Attempts);
                return Results.Ok(status);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                return InvalidAction(exception.Message);
            }
        });

        app.MapPost("/api/v1/owner/providers/slots/create", (
            OwnerSignedHttpRequest<OwnerCredentialSlotCreationAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers) =>
        {
            if (request?.Action is null)
                return InvalidAction("A named API key is required.");
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/providers/slots/create",
                OwnerActionPayload.CredentialSlotCreation, out _) is { } authorizationFailure)
                return authorizationFailure;
            try { return Results.Ok(providers.CreateCredentialSlot(request.Action)); }
            catch (ArgumentException exception)
            {
                return InvalidAction(exception.Message);
            }
        });

        app.MapPost("/api/v1/owner/providers/slots/delete", (
            OwnerSignedHttpRequest<OwnerCredentialSlotDeletionAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            WorldRuntime runtime,
            ILogger<ProviderConfigurationStore> logger) =>
        {
            if (request?.Action is null)
                return InvalidAction("A credential-slot deletion action is required.");

            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/providers/slots/delete",
                OwnerActionPayload.CredentialSlotDeletion, out _) is { } authorizationFailure)
                return authorizationFailure;

            try
            {
                OwnerProviderConfigurationStatus status;
                lock (providers.WorldMutationGate)
                    status = providers.DeleteCredentialSlot(request.Action.CredentialSlotId, runtime.RoutineHelper);
                OwnerCredentialSlotTelemetry.Deleted(logger, "deleted", request.Action.CredentialSlotId);
                return Results.Ok(status);
            }
            catch (ArgumentException exception)
            {
                return InvalidAction(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                OwnerCredentialSlotTelemetry.Deleted(logger, "assigned", request.Action.CredentialSlotId);
                return Results.Conflict(new { error = exception.Message });
            }
        });

        app.MapPost("/api/v1/owner/providers/configure", (
            OwnerSignedHttpRequest<OwnerProviderConfigurationAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            WorldRuntime runtime,
            OwnerWorldObservationStore observations) =>
        {
            if (request?.Action is null)
            {
                return InvalidAction("A provider-configuration action is required.");
            }

            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/providers/configure",
                OwnerActionPayload.ProviderConfiguration, out _) is { } authorizationFailure)
                return authorizationFailure;

            try
            {
                if (request.Action.AgentId is { } target &&
                    !observations.GetSnapshot().Agents.Any(item => item.Id == target))
                {
                    return InvalidAction("Choose an agent in this world.", "agentId");
                }
                lock (providers.WorldMutationGate)
                    return Results.Ok(providers.Configure(request.Action, runtime.RoutineHelper));
            }
            catch (ArgumentException exception)
            {
                return InvalidAction(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

        // Legacy diagnostic routes deliberately remain present only to make their
        // read denial explicit (and to retain a 405 response for accidental POSTs).
    }
}
