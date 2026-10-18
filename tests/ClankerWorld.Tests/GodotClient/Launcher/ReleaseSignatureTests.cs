using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.GodotClient.Launcher;

namespace ClankerWorld.Tests;

public sealed class ReleaseSignatureTests
{
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
    public async Task ApprovalBindsThePackageAndManifestAndPreservesCallerCancellation()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new TestReleaseSignatureVerifier(key);
        var proof = new ReleasePackageSignature("test.zip", new string('a', 64), new string('b', 64), "");
        proof = proof with
        {
            Signature = Convert.ToBase64String(key.SignData(proof.Payload(), HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence))
        };
        using var content = new StringContent(JsonSerializer.Serialize(proof));
        var read = await ReleasePackageSignature.ReadAsync(content, CancellationToken.None);
        Assert.True(read.Verify("test.zip", verifier));
        Assert.False(read.Verify("other.zip", verifier));
        Assert.False((read with { PackageSha256 = new string('c', 64) }).Verify("test.zip", verifier));
        Assert.False((read with { ManifestSha256 = new string('c', 64) }).Verify("test.zip", verifier));
        // A raw signature from any locally generated key is not a workflow approval.
        Assert.False(read.Verify("test.zip"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var fresh = new StringContent("{}");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReleasePackageSignature.ReadAsync(fresh, cancelled.Token));
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

// Install/repair fixtures have their own authentication boundary. The Go tests
// exercise the production Sigstore certificates and transparency proofs.
internal sealed class TestReleaseSignatureVerifier(ECDsa key) : IReleaseSignatureVerifier
{
    public bool Verify(byte[] payload, string bundle)
    {
        try
        {
            return key.VerifyData(payload, Convert.FromBase64String(bundle), HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
