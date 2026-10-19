namespace ClankerWorld.Contracts;

/// <summary>Host-local comparison data; never part of saves or public observations.</summary>
public sealed record HostDeviceRequest(string PairingId, string? PairingCode,
    string PublicKeyFingerprint, DateTimeOffset ExpiresAtUtc);
