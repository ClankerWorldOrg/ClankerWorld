using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapSetup(WebApplication app)
    {
        var founderSetupGate = app.Services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate;

        app.MapPost("/api/v1/owner/town/first-layout", (
            OwnerSignedHttpRequest<OwnerFirstTownLayoutAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {
            if (request?.Action is not { } action)
                return Results.BadRequest(new { error = "Choose a rough Town site." });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/town/first-layout",
                OwnerHttpBinding.FirstTownLayoutPayload(action));
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            lock (founderSetupGate)
            {
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
            }
        });

        app.MapPost("/api/v1/owner/founders/place", (
            OwnerSignedHttpRequest<OwnerFounderPlacementAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            ILoggerFactory loggerFactory,
            IServiceProvider services) =>
        {
            if (request?.Action is not { Cognition: { } cognition } action)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A founder placement is required in a world."] });
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/founders/place",
                OwnerHttpBinding.FounderPlacementPayload, out _) is { } authorizationFailure)
                return authorizationFailure;
            if (cognition.AgentId != action.FounderId || cognition.Role != PlayerDecisionProviders.PersonalRole ||
                cognition.ForgetCredential || !PlayerDecisionProviders.IsModelProvider(cognition.Provider))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["cognition"] = ["Choose one personal hosted model for this founder."] });

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
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
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
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["An agent placement is required in a world."] });
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/agents/place",
                OwnerHttpBinding.AgentPlacementPayload, out _) is { } authorizationFailure)
                return authorizationFailure;
            if (cognition.AgentId != action.AgentId || cognition.Role != PlayerDecisionProviders.PersonalRole ||
                cognition.ForgetCredential || !PlayerDecisionProviders.IsModelProvider(cognition.Provider))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["cognition"] = ["Choose one personal hosted model for this agent."] });

            lock (founderSetupGate)
            {
                try
                {
                    var runtime = services.GetRequiredService<WorldRuntime>();
                    var position = new GridPoint(action.X, action.Y);
                    runtime.ValidateAgentPlacement(action.AgentId, position,
                        action.ExpectedHouseholdId, action.ExpectedTownId);
                    providers.Configure(cognition);
                    var household = runtime.AddAgent(action.AgentId, position,
                        action.ExpectedHouseholdId, action.ExpectedTownId);
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
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
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
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["Choose an agent and name."] });
            if (AuthorizeOwnerRequest(request, authorizer, "/api/v1/owner/agents/rename",
                OwnerHttpBinding.AgentRenamePayload, out _) is { } authorizationFailure)
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
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] });
            }
        });

        app.MapPost("/api/v1/owner/control/start-world", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            ProviderConfigurationStore providers,
            IServiceProvider services,
            OwnerWorldObservationStore observations,
            ILoggerFactory loggerFactory) =>
        {
            if (!IsControl(request, "start-world"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["action.operation"] = ["This endpoint starts a configured world."] });
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/control/start-world",
                OwnerHttpBinding.EmptyPayload("start-world"));
            if (!authorization.IsSuccess)
                return OwnerFailures.ToHttpResult(authorization.Failure);

            lock (founderSetupGate)
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
            }
        });
    }
}
