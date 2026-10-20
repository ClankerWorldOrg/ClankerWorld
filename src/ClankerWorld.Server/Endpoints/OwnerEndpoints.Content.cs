using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Content;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapContent(WebApplication app)
    {
        app.MapBuildingDesign();

        app.MapPost("/api/v1/owner/content/propose", (
            OwnerSignedHttpRequest<OwnerContentPackageAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {

            if (!OwnerContentBinding.TryMapManifest(request?.Action, out var manifest, out var failure))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [failure],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.ProposePayload(request!.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/propose", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var record = runtime.ProposeContent(manifest!);
                stateFile.Save(runtime);
                return Results.Ok(OwnerContentPackageReceipt.From("propose", record));
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
            }
        });

        app.MapPost("/api/v1/owner/content/validate", (
            OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {

            if (request?.Action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action.packageId"] = ["A package ID is required."],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.PackageIdPayload("validate", request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/validate", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var resolution = runtime.ResolveContent(request.Action.PackageId);
                var record = runtime.ValidateContent(request.Action.PackageId, resolution);
                stateFile.Save(runtime);
                return Results.Ok(OwnerContentPackageReceipt.From("validate", record));
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
            }
        });

        app.MapPost("/api/v1/owner/content/approve", (
            OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {

            if (request?.Action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action.packageId"] = ["A package ID is required."],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.PackageIdPayload("approve", request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/approve", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var record = runtime.ApproveContent(request.Action.PackageId);
                stateFile.Save(runtime);
                return Results.Ok(OwnerContentPackageReceipt.From("approve", record));
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
            }
        });

        app.MapPost("/api/v1/owner/content/stage", (
            OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {

            if (request?.Action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action.packageId"] = ["A package ID is required."],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.PackageIdPayload("stage", request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/stage", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var record = runtime.StageContent(request.Action.PackageId);
                stateFile.Save(runtime);
                return Results.Ok(OwnerContentPackageReceipt.From("stage", record));
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
            }
        });

        app.MapPost("/api/v1/owner/content/rollback", (
            OwnerSignedHttpRequest<OwnerContentRollbackAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            ILogger<WorldRuntimeService> logger) =>
        {

            if (request?.Action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = ["A package ID and rollback reason are required."],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.RollbackPayload(request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/rollback", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            try
            {
                var record = runtime.RollbackContent(request.Action.PackageId, request.Action.Reason);
                stateFile.Save(runtime);
                OwnerContentTelemetry.Rollback(logger, runtime.WorldTick, record.Manifest.PackageId, "quarantined");
                return Results.Ok(OwnerContentPackageReceipt.From("rollback", record));
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }
            catch (InvalidOperationException exception)
            {
                OwnerContentTelemetry.Rollback(logger, runtime.WorldTick, request.Action.PackageId, "rejected");
                return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new OwnerControlFailure("content_not_found", exception.Message));
            }
        });

        app.MapPost("/api/v1/owner/content/remove", (
            OwnerSignedHttpRequest<OwnerContentPackageIdAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            ILogger<WorldRuntimeService> logger) =>
        {
            if (request?.Action is null) return Results.BadRequest(new { error = "A package ID is required." });
            string payload;
            try { payload = OwnerContentBinding.PackageIdPayload("remove", request.Action); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A valid package ID is required." }); }
            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/content/remove", payload);
            if (!authorization.IsSuccess) return OwnerFailures.ToHttpResult(authorization.Failure);
            try
            {
                var runtime = services.GetRequiredService<WorldRuntime>();
                services.GetRequiredService<WorldStateFile>().RemoveQuarantinedContent(runtime, request.Action.PackageId);
                OwnerContentTelemetry.Rollback(logger, runtime.WorldTick,
                    request.Action.PackageId, "removed");
                return Results.Ok(new { removed = request.Action.PackageId });
            }
            catch (InvalidOperationException exception)
            { return Results.Conflict(new OwnerControlFailure("content_rejected", exception.Message)); }
            catch (KeyNotFoundException)
            { return Results.NotFound(new OwnerControlFailure("content_not_found", "The package no longer exists.")); }
        });

        app.MapPost("/api/v1/owner/buildings/place", (
            OwnerSignedHttpRequest<OwnerBuildingPlacementAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services,
            ILoggerFactory loggerFactory) =>
        {

            if (request?.Action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = ["A building placement action is required."],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.BuildingPlacementPayload(request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/buildings/place", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            var previousBorderTiles = runtime.Towns.SingleOrDefault(item => item.Id == TownBorderRules.FirstTownId)?.BorderTiles.Count ?? 0;
            var result = runtime.PlaceBuilding(
                request.Action.InstanceId,
                request.Action.DefinitionId,
                new ClankerWorld.Simulation.World.GridPoint(request.Action.X, request.Action.Y));
            if (!result.Applied)
            {
                return Results.Conflict(new OwnerControlFailure("building_rejected", result.Failure ?? "Building placement was rejected."));
            }

            stateFile.Save(runtime);
            var placed = runtime.WorldSimulation.Buildings.Single(item => item.InstanceId == result.InstanceId);
            var town = placed.TownId is { } townId
                ? runtime.Towns.Single(item => item.Id == townId)
                : null;
            var telemetry = loggerFactory.CreateLogger("ClankerWorld.Town");
            TownTelemetry.Transition(telemetry, runtime.WorldTick, town?.Id ?? "none",
                town is null ? TownTransitionKind.BuildingUnassigned : TownTransitionKind.BuildingAssigned,
                town?.ResidentIds.Count ?? 0, town?.AssignedBuildingIds.Count ?? 0, town?.BorderTiles.Count ?? 0);
            if (town is not null && town.BorderTiles.Count != previousBorderTiles)
                TownTelemetry.Transition(telemetry, runtime.WorldTick, town.Id, TownTransitionKind.BorderExpanded,
                    town.ResidentIds.Count, town.AssignedBuildingIds.Count, town.BorderTiles.Count);
            if (town is not null)
                foreach (var bridge in runtime.Bridges.Where(item => item.RouteId == $"road:{town.Id}:{placed.InstanceId}"))
                    TownTelemetry.Bridge(telemetry, runtime.WorldTick, bridge.Id, bridge.Trigger, "built", "none");
            return Results.Ok(result);
        });

        app.MapPost("/api/v1/owner/buildings/remove", (
            OwnerSignedHttpRequest<OwnerBuildingRemovalAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            var result = runtime.RemoveBuilding(request.Action.InstanceId, request.Action.ExpectedTownId,
                request.Action.ExpectedHouseholdId, request.Action.WorldId);
            if (result.Applied) services.GetRequiredService<WorldStateFile>().Save(runtime);
            return Results.Ok(result);
        }).ForWorld<OwnerBuildingRemovalAction>(action => action.WorldId,
            OwnerContentBinding.BuildingRemovalPayload,
            (_, action) => Results.Ok(BuildingManagementResult.Rejected(action.InstanceId, "The active world changed. Select this building again before changing it.")),
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A building removal action is required."] }));

        app.MapPost("/api/v1/owner/buildings/reassign", (
            OwnerSignedHttpRequest<OwnerBuildingReassignmentAction> request,
            IServiceProvider services) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            var result = runtime.ReassignBuilding(request.Action.InstanceId, request.Action.ExpectedTownId,
                request.Action.ExpectedHouseholdId, request.Action.TargetTownId, request.Action.TargetHouseholdId, request.Action.WorldId);
            if (result.Applied) services.GetRequiredService<WorldStateFile>().Save(runtime);
            return Results.Ok(result);
        }).ForWorld<OwnerBuildingReassignmentAction>(action => action.WorldId,
            OwnerContentBinding.BuildingReassignmentPayload,
            (_, action) => Results.Ok(BuildingManagementResult.Rejected(action.InstanceId, "The active world changed. Select this building again before changing it.")),
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A building reassignment action is required."] }));

        app.MapPost("/api/v1/owner/production/start", (
            OwnerSignedHttpRequest<OwnerProductionStartAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {

            if (request?.Action is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = ["A production-start action is required."],
                });
            }

            string payload;
            try
            {
                payload = OwnerContentBinding.ProductionStartPayload(request.Action);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = [exception.Message],
                });
            }

            var authorization = authorizer.Authorize(request, "POST", "/api/v1/owner/production/start", payload);
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            var runtime = services.GetRequiredService<WorldRuntime>();
            var stateFile = services.GetRequiredService<WorldStateFile>();
            var result = runtime.StartProduction(
                request.Action.RecipeId,
                request.Action.BuildingInstanceId,
                request.Action.WorkerId);
            if (!result.Applied)
            {
                return Results.Conflict(new OwnerControlFailure("production_rejected", result.Failure ?? "Recipe production was rejected."));
            }

            stateFile.Save(runtime);
            return Results.Ok(result);
        });

        app.MapPost("/api/v1/owner/authoring", (
            OwnerSignedHttpRequest<OwnerAuthoringBatchAction> request, IServiceProvider services) =>
        {
            var action = request.Action;
            var runtime = services.GetRequiredService<WorldRuntime>();
            var timeline = runtime.ExportObservation().Timeline;
            if (action.Timeline!.InstanceId != timeline.InstanceId || action.Timeline.Generation != timeline.Generation)
                return Results.Conflict(new OwnerControlFailure("world_edit_refused",
                    "The world was loaded or changed. Refresh and select the tiles again."));
            var result = services.GetRequiredService<WorldStateFile>().ApplyMapEdit(runtime,
                new(action.WorldId!, action.ExpectedEventId, action.RunEpoch, action.BatchId,
                    action.Operations.Select(operation => operation is null ? null! : new WorldMapEditOperation(
                        operation.Kind, operation.Id, operation.Value, operation.SecondaryValue,
                        operation.X, operation.Y, operation.IsRenewable)).ToArray()));
            if (result.Failure is { } failure)
                return Results.Conflict(new OwnerControlFailure("world_edit_refused", failure));
            var state = runtime.ExportState();
            return Results.Ok(new OwnerAuthoringBatchReceipt(action.BatchId, result.Applied || result.AlreadyApplied,
                null, state.EventHistoryFloor + state.Events.Count, 0,
                state.Map.ManifestDigest));
        }).ForWorld<OwnerAuthoringBatchAction>(action => action.WorldId ?? "", OwnerHttpBinding.AuthoringPayload,
            (_, _) => Results.Conflict(new OwnerControlFailure("world_edit_refused", "The active world changed. Refresh before editing.")),
            validate: action => action.Timeline is not { IsValid: true } || action.WorldId is null || action.ExpectedEventId < 0 || action.RunEpoch < 0 ||
                action.Operations is not { Count: > 0 and <= WorldMapEditRules.MaximumOperations }
                ? Results.Conflict(new OwnerControlFailure("world_edit_refused", "Refresh the world, then choose between 1 and 64 tiles to edit.")) : null);
    }
}
