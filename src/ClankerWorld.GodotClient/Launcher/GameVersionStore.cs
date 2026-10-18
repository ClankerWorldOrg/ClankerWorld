using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClankerWorld.GodotClient.Launcher;

/// <summary>An installed game version, with its folder's size on disk.</summary>
public sealed record InstalledVersion(GameVersionName Version, string Directory, long SizeBytes, string? SourceRevision)
{
    public string Executable => Path.Combine(Directory,
        PackageEntryPoints.ReadInstalled(Directory)?.Game ?? LauncherLayout.GameExecutable);
}

/// <summary>Why an install or repair stopped. Nothing is half-installed after any of these.</summary>
public sealed class GameInstallException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Installs, checks, repairs and removes game versions in the launcher's
/// versions folder. A download is checked against the release's published
/// SHA-256 and offline signature before anything is unpacked or run. A version's own
/// <c>manifest.sha256</c> lets Repair find files that changed since.
/// Nothing here ever touches the saves, logs or settings folders.
/// </summary>
public sealed class GameVersionStore(LauncherLayout layout, HttpClient http, string signingPublicKey = ReleasePackageSignature.PublicKeyPem)
{
    private const long MaximumPackageBytes = 2L * 1024 * 1024 * 1024;

    public IReadOnlyList<InstalledVersion> Installed()
    {
        if (!Directory.Exists(layout.VersionsDirectory)) return [];
        var installed = new List<InstalledVersion>();
        foreach (var directory in Directory.EnumerateDirectories(layout.VersionsDirectory))
        {
            var version = GameVersionName.Parse(Path.GetFileName(directory));
            // Keep damaged installs visible so their Repair and Remove actions remain available.
            // Temporary and retired folders have names that cannot parse as game versions.
            if (version is null) continue;
            installed.Add(new InstalledVersion(version, directory, SizeOf(directory), ReadSourceRevision(directory)));
        }
        installed.Sort((a, b) => b.Version.CompareTo(a.Version));
        return installed;
    }

    /// <summary>Files that are missing or differ from the version's own manifest.</summary>
    public static IReadOnlyList<string> Verify(InstalledVersion version)
    {
        var problems = new List<string>();
        (string Relative, string Hash)[] manifest;
        try
        {
            manifest = ReadManifest(Path.Combine(version.Directory, LauncherLayout.PackageManifest)).ToArray();
            var names = manifest.Select(item => item.Relative).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // This version's own entry points, so another version's layout never looks damaged.
            if (PackageEntryPoints.Read(version.Directory, names.Contains) is not { } entryPoints)
                return [PackageEntryPoints.FileName];
            if (!names.Contains(entryPoints.Game) || !names.Contains(entryPoints.Server))
                return [LauncherLayout.PackageManifest];
            foreach (var path in Directory.EnumerateFiles(version.Directory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(version.Directory, path);
                if (relative != LauncherLayout.PackageManifest && relative != ReleasePackageSignature.InstalledFile && !names.Contains(relative)) problems.Add(relative);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return [LauncherLayout.PackageManifest];
        }
        foreach (var (relative, expected) in manifest)
        {
            var path = Path.Combine(version.Directory, relative);
            try
            {
                if (!File.Exists(path) || !HashFile(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    problems.Add(relative);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                problems.Add(relative);
            }
        }
        return problems;
    }

    /// <summary>
    /// Downloads, checks and installs a release, replacing any copy already
    /// installed (which is how Repair works). The old copy is kept until the
    /// new one is fully in place.
    /// </summary>
    public async Task<InstalledVersion> InstallAsync(GameRelease release, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!GameReleaseFeed.IsTrustedDownload(release.Package.DownloadUrl))
            throw new GameInstallException("This download doesn't come from ClankerWorld's releases, so it wasn't installed.");
        if (release.Package.Size > MaximumPackageBytes)
            throw new GameInstallException("This download is larger than any ClankerWorld version, so it wasn't installed.");
        if (release.PackageSignature is not { } signatureAsset || !GameReleaseFeed.IsTrustedDownload(signatureAsset.DownloadUrl))
            throw ReleasePackageSignature.Refused();
        using var signatureResponse = await OpenDownloadAsync(signatureAsset, cancellationToken);
        if (!signatureResponse.IsSuccessStatusCode) throw ReleasePackageSignature.Refused();
        var signature = await ReleasePackageSignature.ReadAsync(signatureResponse.Content, cancellationToken);
        if (!signature.Verify(release.Package.Name, signingPublicKey)) throw ReleasePackageSignature.Refused();
        Directory.CreateDirectory(layout.DownloadsDirectory);
        var zip = Path.Combine(layout.DownloadsDirectory, release.Package.Name + ".part");
        try
        {
            await using var package = new FileStream(zip, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            var actual = await DownloadAsync(release.Package, package, progress, cancellationToken);
            if (!actual.Equals(signature.PackageSha256, StringComparison.OrdinalIgnoreCase))
                throw new GameInstallException(
                    "The download didn't match the published file, so it wasn't installed. Your saves are untouched. Try again.");
            package.Position = 0;
            return InstallPackage(package, release.Version, signature);
        }
        finally { TryDelete(zip); }
    }

    /// <summary>Unpacks an already checked package into its version folder.</summary>
    internal InstalledVersion InstallPackage(string zipPath, GameVersionName version, ReleasePackageSignature? signature = null)
    {
        using var package = File.OpenRead(zipPath);
        return InstallPackage(package, version, signature);
    }

    private InstalledVersion InstallPackage(Stream package, GameVersionName version, ReleasePackageSignature? signature)
    {
        Directory.CreateDirectory(layout.VersionsDirectory);
        var staging = Path.Combine(layout.VersionsDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
        var destination = layout.VersionDirectory(version);
        var retired = Path.Combine(layout.VersionsDirectory, $".old-{version}-{Guid.NewGuid():N}");
        try
        {
            Extract(package, GameReleaseFeed.PackageName(version) + "/", staging);
            if (signature is not null)
            {
                if (!HashFile(Path.Combine(staging, LauncherLayout.PackageManifest)).Equals(signature.ManifestSha256, StringComparison.OrdinalIgnoreCase))
                    throw ReleasePackageSignature.Refused();
                File.WriteAllText(Path.Combine(staging, ReleasePackageSignature.InstalledFile), JsonSerializer.Serialize(signature));
            }
            if (PackageEntryPoints.ReadInstalled(staging) is not { } entryPoints ||
                !File.Exists(Path.Combine(staging, entryPoints.Game)))
                throw new GameInstallException("This download isn't a ClankerWorld game package, so it wasn't installed.");
            var staged = new InstalledVersion(version, staging, 0, null);
            if (Verify(staged).Count > 0)
                throw new GameInstallException("This download's files don't match its own list, so it wasn't installed.");
            try
            {
                if (Directory.Exists(destination)) Directory.Move(destination, retired);
                Directory.Move(staging, destination);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (!Directory.Exists(destination) && Directory.Exists(retired)) Directory.Move(retired, destination);
                throw new GameInstallException($"Close ClankerWorld {version} first, then try again.", exception);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw new GameInstallException("This download is damaged, so it wasn't installed. Try again.", exception);
        }
        finally
        {
            TryDeleteDirectory(staging);
            // A failed rollback must leave its only usable copy for startup recovery.
            if (Directory.Exists(destination)) TryDeleteDirectory(retired);
        }
        return Installed().Single(installed => installed.Version == version);
    }

    /// <summary>Rechecks the pinned approval and every manifested file before Play.</summary>
    public static void RequireSigned(InstalledVersion version, string publicKey = ReleasePackageSignature.PublicKeyPem)
    {
        try
        {
            var signature = ReleasePackageSignature.ReadFile(Path.Combine(version.Directory, ReleasePackageSignature.InstalledFile));
            if (!signature.Verify(GameReleaseFeed.PackageName(version.Version) + ".zip", publicKey))
                throw ReleasePackageSignature.Refused();
            if (!HashFile(Path.Combine(version.Directory, LauncherLayout.PackageManifest)).Equals(signature.ManifestSha256, StringComparison.OrdinalIgnoreCase) ||
                Verify(version).Count != 0)
                throw NeedsRepair();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw NeedsRepair();
        }
    }

    private static GameInstallException NeedsRepair() => new(
        "This version's files couldn't be verified. Choose Repair on the Versions page before playing. Your saves are untouched.");

    /// <summary>Removes one version's program files. Worlds, keys and logs stay where they are.</summary>
    public void Remove(InstalledVersion version)
    {
        var expected = Path.GetFullPath(layout.VersionDirectory(version.Version));
        if (!string.Equals(Path.GetFullPath(version.Directory), expected, StringComparison.Ordinal))
            throw new InvalidOperationException("Only folders in the versions folder can be removed.");
        var retired = Path.Combine(layout.VersionsDirectory, $".removed-{version.Version}-{Guid.NewGuid():N}");
        try { Directory.Move(expected, retired); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameInstallException($"Close ClankerWorld {version.Version} first, then try again.", exception);
        }
        TryDeleteDirectory(retired);
    }

    /// <summary>Clears leftovers from an install or removal that was interrupted.</summary>
    public void CleanUp()
    {
        if (Directory.Exists(layout.VersionsDirectory))
            foreach (var directory in Directory.EnumerateDirectories(layout.VersionsDirectory).ToArray())
            {
                var name = Path.GetFileName(directory);
                if (name.StartsWith(".staging-", StringComparison.Ordinal) || RetiredVersion(name, ".removed-") is not null)
                    TryDeleteDirectory(directory);
                else if (RetiredVersion(name, ".old-") is { } version)
                {
                    var destination = layout.VersionDirectory(version);
                    try
                    {
                        if (!Directory.Exists(destination)) Directory.Move(directory, destination);
                        else TryDeleteDirectory(directory);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        // Keep the backup if it cannot yet be restored (for example, while in use).
                    }
                }
            }
        if (Directory.Exists(layout.DownloadsDirectory))
            foreach (var file in Directory.EnumerateFiles(layout.DownloadsDirectory, "*.part"))
                TryDelete(file);
    }

    private static GameVersionName? RetiredVersion(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length + 33 &&
        name[^33] == '-' && Guid.TryParseExact(name[^32..], "N", out _)
            ? GameVersionName.Parse(name[prefix.Length..^33]) : null;

    public static IEnumerable<(string Relative, string Hash)> ReadManifest(string manifestPath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(manifestPath))
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
            if (line.Length < 67 || line[64] != ' ' || line[65] is not (' ' or '*'))
                throw new InvalidDataException("The version's file list is invalid.");
            var hash = line[..64];
            var relative = line[66..];
            if (relative.StartsWith("./", StringComparison.Ordinal)) relative = relative[2..];
            if (!hash.All(Uri.IsHexDigit) || !IsSafeRelativePath(relative) || !names.Add(relative))
                throw new InvalidDataException("The version's file list is invalid.");
            yield return (relative.Replace('/', Path.DirectorySeparatorChar), hash);
        }
    }

    private async Task<HttpResponseMessage> OpenDownloadAsync(ReleaseAsset asset, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (GameReleaseFeed.IsTrustedDownloadResponse(response.RequestMessage?.RequestUri)) return response;
        response.Dispose();
        throw new GameInstallException("This download left ClankerWorld's release hosts, so it wasn't installed. Your saves are untouched. Try again.");
    }

    private async Task<string> DownloadAsync(ReleaseAsset asset, Stream target, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await OpenDownloadAsync(asset, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaximumPackageBytes || total > asset.Size)
                throw new GameInstallException("The download was larger than the published file, so it wasn't installed.");
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            progress?.Report((double)total / asset.Size);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Extract(Stream package, string prefix, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("The package has files outside its version folder.");
            var relative = name[prefix.Length..];
            if (relative.Length == 0 || relative.EndsWith('/')) continue;
            if (!IsSafeRelativePath(relative)) throw new InvalidDataException("The package has an unsafe file name.");
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException("The package has an unsafe file name.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }

    internal static bool IsSafeRelativePath(string relative) =>
        relative.Length is > 0 and < 260 && !relative.StartsWith('/') && !relative.Contains(':', StringComparison.Ordinal) &&
        !relative.Contains('\\', StringComparison.Ordinal) &&
        relative.Split('/').All(part => part.Length > 0 && part != "." && part != "..");

    private static string? ReadSourceRevision(string directory)
    {
        const string prefix = "# Source commit: ";
        try
        {
            return File.ReadLines(Path.Combine(directory, LauncherLayout.PackageManifest)).Take(4)
                .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..].Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static long SizeOf(string directory)
    {
        try
        {
            return new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return 0; }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
