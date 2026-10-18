using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapPairing(WebApplication app)
    {
        // This is deliberately discovery-only. Full owner capabilities are returned
        // only inside the authenticated reconnect baseline below.
        app.MapGet("/api/v1/handshake", () => Results.Ok(new HandshakeView(
            new ProtocolVersion(Major: 1, Minor: 1),
            ["owner-device-pairing.v1"],
            ["owner-device-pairing.v1"])));

        // Bound unauthenticated creation before JSON binding. Do not spend this budget
        // on signed owner challenges/reconnect/pause or host-local recovery. Pairing
        // bodies are already capped by RequestBodyLimits. Routing has already chosen
        // the endpoint, so every path spelling it accepts for the route is counted.
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<PairingRequestBudget.Route>() is not null &&
                !context.RequestServices.GetRequiredService<PairingRequestBudget>().TryAcquire())
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                return;
            }
            await next(context);
        });

        // Prove that this listener is the local server holding the game's secret,
        // without receiving it, creating a pairing or changing the world.
        app.MapGet("/api/v1/approval/launch-check", (HttpContext context, ApprovalListenerOptions options) =>
            options.LaunchCheckAnswer(context) is { } answer
                ? Results.Ok(new LocalServerLaunchCheckAnswer(answer)) : Results.NotFound());

        app.MapPost("/api/v1/approval/pairings", (
            StartOwnerPairingHttpRequest request, HttpContext context, ApprovalListenerOptions options,
            OwnerAuthorityStore authority, OwnerAuthorityStateFile stateFile, OwnerHostInformation host) =>
        {
            if (!options.IsApprovalRequest(context)) return Results.NotFound();
            var result = authority.StartPairingLocally(new OwnerPairingRequest(request?.PublicKeySpkiBase64 ?? string.Empty));
            stateFile.Save(authority);
            PairingRequestBudget.LogLocalRecovery(app.Logger, result.IsSuccess ? "created" : "refused");
            return result.IsSuccess ? Results.Ok(result.Value! with { Host = host }) : OwnerFailures.ToHttpResult(result.Failure);
        });

        app.MapPost("/api/v1/pairings", (
            StartOwnerPairingHttpRequest request,
            OwnerHostInformation host,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            var result = authority.StartPairing(new OwnerPairingRequest(request?.PublicKeySpkiBase64 ?? string.Empty));
            // Start/expiry trimming changes operational authority state even when a
            // new pairing is refused for capacity. Persist that bounded state before
            // returning either outcome.
            stateFile.Save(authority);
            if (!result.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(result.Failure);
            }

            return Results.Ok(result.Value! with { Host = host });
        }).WithMetadata(PairingRequestBudget.Route.Instance);

        app.MapGet("/api/v1/pairings/{pairingId}", (
            string pairingId,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            var result = authority.GetPairingStatus(pairingId);
            // Polling can cause a pending record to become expired, so retain that
            // state before replying.
            stateFile.Save(authority);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : OwnerFailures.ToHttpResult(result.Failure);
        });

        app.MapPost("/api/v1/pairings/activate", (
            ActivateOwnerPairingHttpRequest request,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            var result = authority.ActivatePairing(new OwnerPairingActivationRequest(
                request?.PairingId ?? string.Empty,
                request?.CanonicalProof ?? string.Empty,
                request?.SignatureBase64 ?? string.Empty));
            // Expiry is applied while evaluating activation, including rejected
            // activation requests, so persist before returning the result.
            stateFile.Save(authority);
            if (!result.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(result.Failure);
            }

            return Results.Ok(result.Value);
        });

        app.MapGet("/api/v1/approval/pairings", (HttpContext context, ApprovalListenerOptions options,
            OwnerAuthorityStore authority, OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsApprovalRequest(context)) return Results.NotFound();
            var pending = authority.GetPendingPairingsLocally();
            stateFile.Save(authority);
            return Results.Ok(pending);
        });

        app.MapGet("/api/v1/approval/hosting", (HttpContext context, ApprovalListenerOptions options,
            OwnerClientPresenceLease presence, WorldStartupRecovery recovery, OwnerHostInformation host) =>
        {
            if (!options.IsApprovalRequest(context)) return Results.NotFound();
            return Results.Ok(new HostStatus(presence.ActiveClientCount, !recovery.Pending &&
                context.RequestServices.GetRequiredService<WorldRuntimeService>().HostIdleSaveFailed, recovery.Pending,
                host.Kind, context.RequestServices.GetService<LauncherHostOptions>()?.StopWhenGameQuits ?? false));
        });

        app.MapPost("/api/v1/approval/pairings/{pairingId}/decline", (string pairingId,
            HttpContext context, ApprovalListenerOptions options, OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsApprovalRequest(context)) return Results.NotFound();
            var result = authority.DeclinePendingPairingLocally(pairingId);
            stateFile.Save(authority);
            PairingRequestBudget.LogLocalRecovery(app.Logger, result.IsSuccess ? "declined" : "refused");
            return result.IsSuccess ? Results.NoContent() : OwnerFailures.ToHttpResult(result.Failure);
        });

        // This route is served only by the second loopback-only listener configured by
        // the deployment. Tailscale Serve forwards the normal server port, never this
        // approval port, so a remote client cannot bootstrap itself.
        app.MapPost("/api/v1/approval/pairings/{pairingId}/approve", (
            string pairingId,
            PairingApprovalHttpRequest request,
            HttpContext context,
            ApprovalListenerOptions options,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsApprovalRequest(context))
            {
                return Results.NotFound();
            }

            var result = authority.ApprovePendingPairingLocally(pairingId, request?.PairingCode ?? string.Empty);
            // A mismatch increments the durable bounded-attempt counter, so a restart
            // must not erase unsuccessful approval attempts.
            stateFile.Save(authority);
            PairingRequestBudget.LogLocalRecovery(app.Logger, result.IsSuccess ? "approved" : "refused");
            if (!result.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(result.Failure);
            }

            return Results.Ok(result.Value);
        });

        // Recovery deliberately shares the separate host-local listener with first
        // pairing approval. If every paired Windows device is lost or revoked, the
        // host can still revoke a stale public key before pairing a replacement; this
        // path is not forwarded by Tailscale Serve.
        app.MapPost("/api/v1/approval/devices/{deviceId}/revoke", (
            string deviceId,
            DeviceRevokeHttpRequest _,
            HttpContext context,
            ApprovalListenerOptions options,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsApprovalRequest(context))
            {
                return Results.NotFound();
            }

            var result = stateFile.RevokeDevice(authority, deviceId);
            if (!result.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(result.Failure);
            }

            return Results.Ok(result.Value);
        });

        // The game that started a local server stops it here when the player
        // quits. Keep it alive and paused if its last committed tick cannot be saved.
        app.MapPost("/api/v1/approval/shutdown", (
            HttpContext context,
            ApprovalListenerOptions options,
            IHostApplicationLifetime lifetime) =>
        {
            if (!options.IsLocalServerShutdownRequest(context)) return Results.NotFound();
            var recovery = context.RequestServices.GetService<WorldStartupRecovery>();
            if (recovery is null) return Results.NotFound();
            // A refused startup checkpoint has no loaded world to save.
            if (!recovery.Pending && context.RequestServices.GetRequiredService<WorldRuntimeService>()
                .SaveBeforeShutdown(pauseWorld: true) == ShutdownSaveOutcome.WriteFailed)
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The world could not be saved",
                    detail: "The host is still running and the world is paused. Try again.");
            LocalServerTelemetry.ShutdownRequested(app.Logger);
            // Let Accepted finish using request services before host teardown.
            context.Response.OnCompleted(() =>
            {
                lifetime.StopApplication();
                return Task.CompletedTask;
            });
            return Results.Accepted();
        });

        app.MapPost("/api/v1/owner/challenges", (
            IssueOwnerChallengeHttpRequest request,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            var result = authority.IssueChallenge(new OwnerChallengeIssueRequest(
                request?.DeviceId ?? string.Empty,
                request?.RequestId ?? string.Empty,
                request?.CanonicalProof ?? string.Empty,
                request?.SignatureBase64 ?? string.Empty));
            // Challenge expiry/retention trimming also happens on rejected requests.
            stateFile.Save(authority);
            if (!result.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(result.Failure);
            }

            return Results.Ok(result.Value! with
            {
                SupportedActionPayloads =
                [
                    OwnerHttpBinding.WorldCreationPayloadDomain,
                    OwnerHttpBinding.AgentPlacementPayloadDomain,
                    OwnerHttpBinding.AutosaveConfigurationPayloadDomain,
                    OwnerHttpBinding.AgingOverridePayloadDomain,
                    OwnerHttpBinding.JevAssistancePayloadDomain,
                    "clankerworld.owner-routine-helper.v1",
                    OwnerHttpBinding.ModelThinkingPayloadDomain,
                ],
            });
        });
    }
}
