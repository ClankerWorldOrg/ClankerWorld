using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.GodotClient.Launcher;

namespace ClankerWorld.Tests;

public sealed class ReleaseSignatureTests
{
    // Recorded from the offline OpenSSL tool with a throwaway key; only the public key remains.
    private const string OfflineInteropPublicKey = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEvSW+9vnUyndDbYYltLHGVhYuousW
        1XQMs82C/cf6gzeuzDb2U8CCizxP7Uf0+8A/nplZQkL5RnI7zhDSgCkSgA==
        -----END PUBLIC KEY-----
        """;

    [Fact]
    public void ProductionReleasePinIsP256AndSeparateFromTheDisposableFixture()
    {
        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", ReleasePackageSignature.PublicKeyPem);
        using var key = ECDsa.Create();
        key.ImportFromPem(ReleasePackageSignature.PublicKeyPem);
        Assert.Equal(ECCurve.NamedCurves.nistP256.Oid.Value, key.ExportParameters(false).Curve.Oid.Value);
        Assert.NotEqual(OfflineInteropPublicKey, ReleasePackageSignature.PublicKeyPem);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not json")]
    public async Task MalformedProofsAreRefused(string text)
    {
        using var content = new StringContent(text);
        await Assert.ThrowsAsync<GameInstallException>(() => ReleasePackageSignature.ReadAsync(content, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedProofStopsAtTheByteLimit(bool declared)
    {
        using var stream = new UnseekableStream(new byte[ReleasePackageSignature.MaximumBytes + 100_000]);
        using var content = new StreamContent(stream);
        if (declared) content.Headers.ContentLength = stream.Length;
        await Assert.ThrowsAsync<GameInstallException>(() => ReleasePackageSignature.ReadAsync(content, CancellationToken.None));
        Assert.InRange(stream.BytesRead, 0, ReleasePackageSignature.MaximumBytes + 1);
    }

    [Fact]
    public async Task ParsedProofUsesOnlyThePinnedKeyAndCallerCancellationIsPreserved()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = new ReleasePackageSignature("test.zip", new string('a', 64), new string('b', 64), "");
        proof = proof with
        {
            Signature = Convert.ToBase64String(key.SignData(proof.Payload(), HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence))
        };
        using var content = new StringContent(JsonSerializer.Serialize(proof));
        var read = await ReleasePackageSignature.ReadAsync(content, CancellationToken.None);
        Assert.True(read.Verify("test.zip", key.ExportSubjectPublicKeyInfoPem()));
        Assert.False(read.Verify("other.zip", key.ExportSubjectPublicKeyInfoPem()));
        Assert.False(read.Verify("test.zip", ""));
        Assert.False((read with { ManifestSha256 = new string('c', 64) }).Verify("test.zip", key.ExportSubjectPublicKeyInfoPem()));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var fresh = new StringContent("{}");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReleasePackageSignature.ReadAsync(fresh, cancelled.Token));
    }

    [Fact]
    public void OfflineOpenSslApprovalUsesTheSamePayloadAsTheLauncher()
    {
        var proof = new ReleasePackageSignature(
            "ClankerWorld-0.0.1-windows-x64.zip",
            "8ef7c8b51d3b051ceeac83d4db858c0b81d47b9a701f140280a360ae922dc99f",
            "4e02b86ab085d5e17803c7db862d40d0b8b82091b41d5a56bc23310f7bfacfa5",
            "MEUCIQCx7xu8WWmodfIBxRfI4c8or/r7Jd1IPCF4/ixdlpo0KwIgOO4qJuIZTmpp7lr0Ryj04+djiUO+JO03K83iCZkb8+0=");
        Assert.True(proof.Verify(proof.Package, OfflineInteropPublicKey));
        Assert.False((proof with { PackageSha256 = new string('0', 64) }).Verify(proof.Package, OfflineInteropPublicKey));
    }

    private sealed class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public int BytesRead { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
    }
}
