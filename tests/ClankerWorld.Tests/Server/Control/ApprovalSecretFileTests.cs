using ClankerWorld.Server.Pairing;

namespace ClankerWorld.Tests;

public sealed class ApprovalSecretFileTests
{
    [Fact]
    public void SecretIsPrivateStableAndUsableAsACurlHeader()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), $"clankerworld-approval-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "approval-header");
        try
        {
            var secret = ApprovalSecretFile.LoadOrCreate(path);
            var original = File.ReadAllBytes(path);
            Assert.Equal(64, secret.Length);
            Assert.Equal(ApprovalListenerOptions.ApprovalSecretHeader + ": " + secret + "\n", File.ReadAllText(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
            Assert.Equal(secret, ApprovalSecretFile.LoadOrCreate(path));
            Assert.Equal(original, File.ReadAllBytes(path));

            var second = ApprovalSecretFile.LoadOrCreate(Path.Combine(directory, "second"));
            Assert.NotEqual(secret, second);

            var invalidHex = ApprovalListenerOptions.ApprovalSecretHeader + ": " + new string('Z', 64) + "\n";
            File.WriteAllText(path, invalidHex);
            Assert.Throws<InvalidOperationException>(() => ApprovalSecretFile.LoadOrCreate(path));
            Assert.Equal(invalidHex, File.ReadAllText(path));

            File.WriteAllText(path, "damaged");
            var error = Assert.Throws<InvalidOperationException>(() => ApprovalSecretFile.LoadOrCreate(path));
            Assert.Equal("damaged", File.ReadAllText(path));
            Assert.DoesNotContain(secret, error.ToString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SharedPermissionsAndSymbolicLinksAreRefusedWithoutChangingTheFile()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), $"clankerworld-approval-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "approval-header");
        try
        {
            _ = ApprovalSecretFile.LoadOrCreate(path);
            var original = File.ReadAllBytes(path);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            Assert.Throws<InvalidOperationException>(() => ApprovalSecretFile.LoadOrCreate(path));
            Assert.Equal(original, File.ReadAllBytes(path));
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var link = Path.Combine(directory, "link");
            File.CreateSymbolicLink(link, path);
            Assert.Throws<InvalidOperationException>(() => ApprovalSecretFile.LoadOrCreate(link));
            Assert.Equal(original, File.ReadAllBytes(path));
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupWrite);
            Assert.Throws<InvalidOperationException>(() => ApprovalSecretFile.LoadOrCreate(path));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
