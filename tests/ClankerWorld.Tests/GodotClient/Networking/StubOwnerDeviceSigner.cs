using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.Tests;

internal sealed class StubOwnerDeviceSigner : IOwnerDeviceSigner
{
    public string PublicKeySpkiBase64 => "fixture-key";
    public string PublicKeyFingerprint => "fixture-fingerprint";
    public string SignCanonicalProof(string canonicalProof) => "fixture-signature";
    public void Dispose() { }
}
