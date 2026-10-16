using ClankerWorld.Simulation.Cognition;
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
            validate: action => action.Rate is not (1 or 365 or 1_460) ? Results.ValidationProblem(new Dictionary<string, string[]> { ["action.rate"] = ["Life pace must be 1, 365 or 1460."] }) : null,
            invalidPayload: (_, _) => Results.BadRequest(new { error = "Read the current world's settings before changing life pace." }),
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action.rate"] = ["Life pace must be 1, 365 or 1460."] }));

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
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A Jev setting is required."] }));

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
            OwnerHttpBinding.RoutineHelperPayload,
            (_, _) => Results.Conflict(new { error = "The active world changed. Open World Settings again." }),
            validate: action =>
            {
                if (string.IsNullOrWhiteSpace(action.WorldId) || action.WorldId.Length > 128 || action.WorldId.Any(char.IsControl))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A world and routine helper are required."] });
                try { new RoutineHelperSettings(action.Provider, action.Model, action.CredentialSlotId).Validate(); }
                catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = [exception.Message] }); }
                return null;
            },
            missingAction: () => Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["A world and routine helper are required."] }));

        app.MapPost("/api/v1/owner/control/pause", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (!IsControl(request, "pause"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action.operation"] = ["This endpoint only accepts the pause operation."],
                });
            }

            var authorization = authorizer.Authorize(
                request,
                "POST",
                "/api/v1/owner/control/pause",
                OwnerHttpBinding.EmptyPayload("pause"));
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }


            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            var wasPaused = worldRuntime.Society.IsPaused;
            worldRuntime.Pause();
            var changed = !wasPaused && worldRuntime.Society.IsPaused;
            // A prior failed write may already have changed memory. Even a no-op
            // retry must durably acknowledge the requested pause.
            worldStateFile.Save(worldRuntime);

            return Results.Ok(OwnerControlReceipt.From("pause", changed, worldRuntime.ExportState()));


        });

        app.MapPost("/api/v1/owner/control/resume", (
            OwnerSignedHttpRequest<OwnerControlAction> request,
            OwnerRequestAuthorizer authorizer,
            IServiceProvider services) =>
        {
            if (!IsControl(request, "resume"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action.operation"] = ["This endpoint only accepts the resume operation."],
                });
            }

            var authorization = authorizer.Authorize(
                request,
                "POST",
                "/api/v1/owner/control/resume",
                OwnerHttpBinding.EmptyPayload("resume"));
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }


            var worldRuntime = services.GetRequiredService<WorldRuntime>();
            if (worldRuntime.FounderSetup is { Started: false })
                return Results.Conflict(new { message = "Place four configured founders, then select Start World." });
            if (services.GetRequiredService<ProviderUsageStore>().Capture().AccountingError is { } accountingError)
                return Results.Conflict(new { message = accountingError });
            if (services.GetRequiredService<ProviderUsageStore>().Capture().LimitReached)
                return Results.Conflict(new { message = "The model-call limit is reached. Raise the limit or allow more calls in Settings → Game, then resume." });
            var worldStateFile = services.GetRequiredService<WorldStateFile>();
            var wasPaused = worldRuntime.Society.IsPaused;
            // Persist every acknowledgement, including an already-running
            // retry. A paused live world resumes only after the write succeeds.
            worldStateFile.Save(worldRuntime, resumeOnSuccess: true);
            var changed = wasPaused && !worldRuntime.Society.IsPaused;

            return Results.Ok(OwnerControlReceipt.From("resume", changed, worldRuntime.ExportState()));


        });
    }
}
