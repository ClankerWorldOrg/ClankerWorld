using System.Net;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using Microsoft.AspNetCore.Http;

namespace ClankerWorld.Server.Pairing;

public sealed record PairingApprovalHttpRequest(string PairingCode);

public sealed record DeviceRevokeHttpRequest(string? Reason);

public sealed record OwnerControlReceipt(
    string Operation,
    bool Changed,
    bool IsPaused,
    long RunEpoch,
    long Revision,
    long LatestEventId)
{
    public static OwnerControlReceipt From(string operation, bool changed, WorldSnapshotView snapshot)
    {
        var authoring = snapshot.Authoring;
        return new OwnerControlReceipt(
            operation,
            changed,
            authoring?.IsPaused ?? snapshot.Cognition?.IsPaused ?? false,
            authoring?.RunEpoch ?? 0,
            authoring?.Revision ?? snapshot.WorldTick,
            snapshot.LatestEventId);
    }

    // Control acknowledgements must not depend on the optional world-view
    // projection. A projection defect must never turn an accepted pause into
    // an HTTP 500 that leaves the client unsure whether the world stopped.
    public static OwnerControlReceipt From(string operation, bool changed, WorldRuntimeState state)
    {
        var revision = state.EventHistoryFloor + state.Events.Count;
        return new OwnerControlReceipt(operation, changed, state.Society.Society.IsPaused,
            state.Society.Society.RunEpoch, revision, revision);
    }
}

public sealed record OwnerControlFailure(string Code, string Detail);

/// <summary>
/// The normal HTTPS/Tailnet listener never has this port. Only a separately
/// bound loopback listener can approve the first pending device, and only for a
/// request addressed to a literal loopback host name.
/// </summary>
/// <remarks>
/// A local server also has a launch secret.
/// The game wrote it to a file in the player's own data folder before starting
/// the host, so every program on the PC can reach the loopback listener but only
/// the game that started it can approve its pairing or stop it.
/// </remarks>
public sealed record ApprovalListenerOptions(int ApprovalPort, string? LaunchSecret = null)
{
    public const string LaunchSecretHeader = "X-ClankerWorld-Launch-Secret";
    public const int MinimumLaunchSecretLength = 32;

    public bool IsLocalServer => LaunchSecret is not null;

    public bool IsApprovalRequest(HttpContext context)
    {
        if (ApprovalPort <= 0 || context.Connection.LocalPort != ApprovalPort ||
            context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
            return false;
        // A loopback connection is not enough on its own: the caller must also
        // address this host by a loopback name, as the game and a local operator do.
        if (!IsLoopbackHostName(context.Request.Host))
        {
            context.RequestServices?.GetService<RequestRefusals>()?.ApprovalHostNameRefused();
            return false;
        }
        return LaunchSecret is null || HasLaunchSecret(context);
    }

    /// <summary>
    /// <c>127.0.0.1</c>, <c>[::1]</c> or <c>localhost</c>, with any port or none.
    /// Other spellings of a loopback address are refused.
    /// </summary>
    public static bool IsLoopbackHostName(HostString host) =>
        host.HasValue && (host.Host is "127.0.0.1" or "[::1]" ||
            string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    /// <summary>Only a local server can be stopped over HTTP, and only by the game that started it.</summary>
    public bool IsLocalServerShutdownRequest(HttpContext context) => IsLocalServer && IsApprovalRequest(context);

    private bool HasLaunchSecret(HttpContext context)
    {
        var supplied = context.Request.Headers[LaunchSecretHeader].ToString();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(LaunchSecret!));
    }

    /// <summary>
    /// Reads the secret the starting game wrote. The value is never logged or
    /// echoed; a missing, short or unreadable file stops startup.
    /// </summary>
    public static string ReadLaunchSecret(string path)
    {
        var secret = File.ReadAllText(path).Trim();
        if (secret.Length < MinimumLaunchSecretLength || secret.Any(char.IsWhiteSpace))
            throw new InvalidOperationException(
                $"The launch secret file must hold one value of at least {MinimumLaunchSecretLength} characters.");
        return secret;
    }
}

public static class OwnerFailures
{
    public static IResult UnpairedObservation() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Owner pairing required",
        detail: "Pair a device and use a signed owner reconnect request to inspect this world.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "owner_pairing_required",
        });

    public static IResult ToHttpResult(OwnerAuthorityFailure failure)
    {
        var (statusCode, code, title) = failure switch
        {
            OwnerAuthorityFailure.InvalidRequest => (StatusCodes.Status400BadRequest, "invalid_request", "Invalid owner request"),
            OwnerAuthorityFailure.InvalidPublicKey => (StatusCodes.Status400BadRequest, "invalid_public_key", "Invalid owner key"),
            OwnerAuthorityFailure.PairingNotFound or OwnerAuthorityFailure.DeviceNotFound or OwnerAuthorityFailure.ChallengeNotFound =>
                (StatusCodes.Status404NotFound, "owner_record_not_found", "Owner record not found"),
            OwnerAuthorityFailure.PublicKeyAlreadyRegistered or OwnerAuthorityFailure.PairingNotPending or OwnerAuthorityFailure.PairingAlreadyActive =>
                (StatusCodes.Status409Conflict, "owner_state_conflict", "Owner state conflict"),
            OwnerAuthorityFailure.PairingCapacityExceeded or OwnerAuthorityFailure.ChallengeCapacityExceeded =>
                (StatusCodes.Status429TooManyRequests, "owner_capacity_reached", "Owner authority capacity reached"),
            OwnerAuthorityFailure.ChallengeConsumed =>
                (StatusCodes.Status409Conflict, "challenge_already_consumed", "Challenge already consumed"),
            OwnerAuthorityFailure.DeviceRevoked =>
                (StatusCodes.Status403Forbidden, "device_revoked", "Device revoked"),
            OwnerAuthorityFailure.PairingExpired or OwnerAuthorityFailure.PairingNotApproved or OwnerAuthorityFailure.PairingCodeMismatch or
            OwnerAuthorityFailure.InvalidCanonicalProof or OwnerAuthorityFailure.InvalidSignature or OwnerAuthorityFailure.ChallengeExpired or
            OwnerAuthorityFailure.ChallengeDeviceMismatch or OwnerAuthorityFailure.ChallengeNonceMismatch =>
                (StatusCodes.Status401Unauthorized, "owner_proof_rejected", "Owner proof rejected"),
            _ => (StatusCodes.Status400BadRequest, "owner_request_rejected", "Owner request rejected"),
        };
        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: $"Owner authority rejected this request: {failure}.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["authorityFailure"] = failure.ToString(),
            });
    }
}
