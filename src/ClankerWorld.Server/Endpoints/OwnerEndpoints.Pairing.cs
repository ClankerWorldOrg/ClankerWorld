using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Control;
using ClankerWorld.Server.Observation;

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
        // bodies are already capped by RequestBodyLimits.
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/api/v1/pairings" &&
                !context.RequestServices.GetRequiredService<PairingRequestBudget>().TryAcquire())
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                return;
            }
            await next(context);
        });

        // Prove that this listener is the companion holding the game's secret,
        // without creating a pairing or changing the world.
        app.MapGet("/api/v1/local/companion", (HttpContext context, OwnerPairingHostOptions options) =>
            options.IsCompanionHost && options.IsLocalApprovalRequest(context)
                ? Results.Ok() : Results.NotFound());

        app.MapPost("/api/v1/local/pairings", (
            StartOwnerPairingHttpRequest request, HttpContext context, OwnerPairingHostOptions options,
            OwnerAuthorityStore authority, OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsLocalApprovalRequest(context)) return Results.NotFound();
            var result = authority.StartPairingLocally(new OwnerPairingRequest(request?.PublicKeySpkiBase64 ?? string.Empty));
            stateFile.Save(authority);
            PairingRequestBudget.LogLocalRecovery(app.Logger, result.IsSuccess ? "created" : "refused");
            return result.IsSuccess ? Results.Ok(result.Value) : OwnerFailures.ToHttpResult(result.Failure);
        });

        app.MapPost("/api/v1/pairings", (
            StartOwnerPairingHttpRequest request,
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

            return Results.Ok(result.Value);
        });

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

        // This route is served only by the second loopback-only listener configured by
        // the deployment. Tailscale Serve forwards the normal viewer port, never this
        // approval port, so a remote client cannot bootstrap itself.
        app.MapPost("/api/v1/local/pairings/{pairingId}/approve", (
            string pairingId,
            LocalPairingApprovalHttpRequest request,
            HttpContext context,
            OwnerPairingHostOptions options,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsLocalApprovalRequest(context))
            {
                return Results.NotFound();
            }

            var result = authority.ApprovePendingPairingLocally(pairingId, request?.PairingCode ?? string.Empty);
            // A mismatch increments the durable bounded-attempt counter, so a restart
            // must not erase unsuccessful approval attempts.
            stateFile.Save(authority);
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
        app.MapPost("/api/v1/local/devices/{deviceId}/revoke", (
            string deviceId,
            LocalDeviceRevokeHttpRequest _,
            HttpContext context,
            OwnerPairingHostOptions options,
            OwnerAuthorityStore authority,
            OwnerAuthorityStateFile stateFile) =>
        {
            if (!options.IsLocalApprovalRequest(context))
            {
                return Results.NotFound();
            }

            var result = authority.RevokeDeviceLocally(deviceId);
            stateFile.Save(authority);
            if (!result.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(result.Failure);
            }

            return Results.Ok(result.Value);
        });

        // The game that started a companion host stops it here when the player
        // quits. Keep it alive and paused if its last committed tick cannot be saved.
        app.MapPost("/api/v1/local/shutdown", (
            HttpContext context,
            OwnerPairingHostOptions options,
            IHostApplicationLifetime lifetime) =>
        {
            if (!options.IsCompanionShutdownRequest(context)) return Results.NotFound();
            var recovery = context.RequestServices.GetService<WorldStartupRecovery>();
            if (recovery is null) return Results.NotFound();
            if (recovery.Pending) return Results.Conflict(new { message = "Choose world recovery before saving." });
            var advancing = context.RequestServices.GetRequiredService<WorldRuntimeService>();
            var outcome = advancing.SaveBeforeShutdown(pauseWorld: true);
            if (outcome == ShutdownSaveOutcome.WriteFailed)
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The world could not be saved",
                    detail: "The host is still running and the world is paused. Try again.");
            CompanionHostTelemetry.ShutdownRequested(app.Logger);
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
                    OwnerHttpBinding.LifePacePayloadDomain,
                    OwnerHttpBinding.JevAssistancePayloadDomain,
                    "clankerworld.owner-routine-helper.v1",
                    OwnerHttpBinding.ModelThinkingPayloadDomain,
                ],
            });
        });
    }
}
