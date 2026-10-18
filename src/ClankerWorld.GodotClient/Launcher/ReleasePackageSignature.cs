using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClankerWorld.GodotClient.Launcher;

/// <summary>Offline approval binds the download and the installed file manifest.</summary>
public sealed record ReleasePackageSignature(string Package, string PackageSha256, string ManifestSha256, string Signature)
{
    // Owner task before this draft can ship: pin the offline release key's PUBLIC PEM.
    // An empty pin deliberately accepts no release. Never put a private key here.
    public const string PublicKeyPem = "";
    public const string InstalledFile = ".release-signature.json";
    public const int MaximumBytes = 4096;

    public byte[] Payload() => Encoding.UTF8.GetBytes(
        $"clankerworld.release-package.v1\npackage={Package}\nsha256={PackageSha256.ToLowerInvariant()}\nmanifest_sha256={ManifestSha256.ToLowerInvariant()}\n");

    public bool Verify(string package, string publicKeyPem)
    {
        if (Package != package || !IsHash(PackageSha256) || !IsHash(ManifestSha256) || string.IsNullOrEmpty(Signature)) return false;
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            return key.KeySize == 256 && key.VerifyData(Payload(), Convert.FromBase64String(Signature),
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException or FormatException)
        {
            return false;
        }
    }

    public static async Task<ReleasePackageSignature> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumBytes) throw Refused();
        using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var bytes = new byte[MaximumBytes + 1];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
        return Parse(bytes.AsSpan(0, count));
    }

    public static ReleasePackageSignature ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        var bytes = new byte[MaximumBytes + 1];
        var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        return Parse(bytes.AsSpan(0, count));
    }

    private static ReleasePackageSignature Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw Refused();
        try { return JsonSerializer.Deserialize<ReleasePackageSignature>(bytes) ?? throw Refused(); }
        catch (JsonException) { throw Refused(); }
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public static GameInstallException Refused() => new(
        "This version has no valid ClankerWorld release signature, so it can't be installed or started. Your saves are untouched. Choose a signed release or update the launcher.");
}
