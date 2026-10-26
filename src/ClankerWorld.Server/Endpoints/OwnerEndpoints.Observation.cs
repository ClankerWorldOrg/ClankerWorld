using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Runtime;

namespace ClankerWorld.Server.Endpoints;

internal static partial class OwnerEndpoints
{
    private static void MapObservation(WebApplication app)
    {
        app.MapPost("/api/v1/owner/reconnect", (
            OwnerSignedHttpRequest<OwnerReconnectAction> request,
            OwnerRequestAuthorizer authorizer,
            OwnerWorldObservationStore observations,
            OwnerClientPresenceLease clientPresence) =>
        {
            if (request?.Action is null || request.Action.AfterEventId < 0)
            {
                return InvalidAction("The event cursor cannot be negative.", "afterEventId");
            }

            if ((request.Action.KnownTerrainWorldId is null) != (request.Action.KnownTerrainDigest is null) ||
                request.Action.KnownTerrainWorldId is { } worldId && (string.IsNullOrWhiteSpace(worldId) || worldId.Length > 256) ||
                request.Action.KnownTerrainDigest is { } digest &&
                    (digest.Length != 64 || digest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))) ||
                request.Action.KnownMapLayersDigest is { } layersDigest &&
                    (request.Action.KnownTerrainWorldId is null || layersDigest.Length != 64 ||
                     layersDigest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))))
            {
                return InvalidAction("The cached terrain world and lowercase manifest digest must be supplied together; a map-layer digest requires that terrain cache claim.", "knownTerrainDigest");
            }

            var authorization = authorizer.Authorize(
                request,
                "POST",
                "/api/v1/owner/reconnect",
                OwnerActionPayload.Reconnect(request.Action));
            if (!authorization.IsSuccess)
            {
                return OwnerFailures.ToHttpResult(authorization.Failure);
            }

            clientPresence.RecordAuthenticatedReconnect(authorization.Value!.DeviceId);

            return Results.Ok(new OwnerWorldReconnect(
                observations.GetOwnerHandshake(),
                observations.GetReconnectBaseline(request.Action.AfterEventId,
                    request.Action.KnownTerrainWorldId, request.Action.KnownTerrainDigest,
                    request.Action.KnownMapLayersDigest)));
        });
    }
}
