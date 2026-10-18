using System.Net;
using ClankerWorld.Contracts;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.GodotClient.Launcher;

/// <summary>Pairs once and reconnects through the same signed API the game uses.</summary>
public sealed class DeviceJoinSession(OwnerWorldApi api, IOwnerDeviceSigner signer, OwnerDeviceRegistrationStore store)
{
    public OwnerPairingStart? Pending { get; private set; }
    public Uri? PendingOrigin { get; private set; }
    public OwnerDeviceRegistration? Registration { get; private set; } = store.TryLoad(signer.PublicKeyFingerprint);

    public async Task StartAsync(Uri origin, CancellationToken cancellationToken,
        Func<Task<OwnerPairingStart>>? startLocally = null)
    {
        if (Pending is not null || Registration is not null)
            throw new InvalidOperationException("Forget the saved connection before joining another host.");
        if (!ServerOrigin.TryResolve(origin.AbsoluteUri, out var canonical))
            throw new InvalidOperationException("Enter an HTTPS host address, without a path.");
        var pairing = startLocally is null ? await api.StartPairingAsync(canonical, signer, cancellationToken)
            : await startLocally();
        (Pending, PendingOrigin) = (pairing, canonical);
    }

    public async Task<bool> PollAsync(CancellationToken cancellationToken)
    {
        var pairing = Pending;
        var origin = PendingOrigin;
        if (pairing is null || origin is null) return Registration is not null;
        var status = await api.GetPairingStatusAsync(origin, pairing.PairingId, cancellationToken);
        if (status.Authority != pairing.Authority || status.DeviceId != pairing.DeviceId ||
            status.PublicKeyFingerprint != signer.PublicKeyFingerprint)
        {
            (Pending, PendingOrigin) = (null, null);
            throw new InvalidDataException("The pairing reply did not match this device and host. Join again.");
        }
        switch (status.State)
        {
            case OwnerPairingState.Pending: return false;
            case OwnerPairingState.Expired:
                (Pending, PendingOrigin) = (null, null);
                throw new InvalidOperationException("The comparison code expired or was declined. Join again.");
            case OwnerPairingState.Approved:
                // A lost reply keeps the pending pairing: a later Active status
                // recovers only the exact authority/device/key checked above.
                await api.ActivatePairingAsync(origin, pairing, signer, cancellationToken);
                break;
            case OwnerPairingState.Active: break;
            default: throw new InvalidDataException("Update ClankerWorld to finish joining this host.");
        }
        var registration = new OwnerDeviceRegistration(pairing.Authority, pairing.DeviceId,
            signer.PublicKeyFingerprint, origin.AbsoluteUri, pairing.Host);
        store.Save(registration);
        Registration = registration;
        (Pending, PendingOrigin) = (null, null);
        return true;
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        var registration = Registration ?? throw new InvalidOperationException("Join a world first.");
        if (!ServerOrigin.TryResolve(registration.WorldUrl, out var origin))
            throw new InvalidDataException("The saved host address is invalid. Forget this connection and join again.");
        var reply = await api.ReconnectAsync(origin, registration.Authority, registration.DeviceId,
            0, null, null, null, signer, cancellationToken);
        // Apply the game's complete observation/handshake validation, not just
        // a responsive port or a successful HTTP status.
        var observations = new ClankerWorld.GodotClient.Protocol.OwnerWorldObservationSession();
        observations.ReplaceRegistration(registration);
        if (!observations.TryAccept(reply, 0, out var failure)) throw new InvalidDataException(failure);
    }

    public void CancelPending() => (Pending, PendingOrigin) = (null, null);

    public void Forget()
    {
        store.Forget();
        Registration = null;
        (Pending, PendingOrigin) = (null, null);
    }

    public string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            "The host refused this device. Ask the host to check its approval before joining again.",
        HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } =>
            "The host's secure connection could not be verified. Check its HTTPS address and certificate.",
        HttpRequestException { StatusCode: null } or TaskCanceledException =>
            "Cannot connect to the host. " + (Registration?.Host ?? Pending?.Host ?? new OwnerHostInformation("unspecified")).StartupHint,
        HttpRequestException => "The host could not complete the request. Check that it is up to date and try again.",
        _ => "Could not join this world. Check the host address and try again.",
    };
}
