using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Server.Pairing;

/// <summary>Account-local proof for the remote server's approval listener.</summary>
public static class ApprovalSecretFile
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const string Prefix = ApprovalListenerOptions.ApprovalSecretHeader + ": ";
    private const int SecretBytes = 32;
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789ABCDEF");

    // The file is also a curl --header @file input, so no command argument holds the value.
    public static string LoadOrCreate(string path)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Remote approval secret files require Unix account permissions.");
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory, PrivateFile | UnixFileMode.UserExecute);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetUnixFileMode(directory) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
            throw new InvalidOperationException("The approval secret directory must not be writable by other accounts.");
        if (!File.Exists(path))
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = PrivateFile,
            };
            using var stream = new FileStream(path, options);
            stream.Write(Encoding.UTF8.GetBytes(Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretBytes)) + "\n"));
            stream.Flush(flushToDisk: true);
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetUnixFileMode(path) & ~PrivateFile) != 0)
            throw new InvalidOperationException("The approval secret file must be readable and writable only by its owner.");
        // Bound the read and refuse damaged files without replacing their contents.
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != Prefix.Length + SecretBytes * 2 + 1)
            throw new InvalidOperationException("The approval secret file is invalid; preserve it for operator recovery.");
        using var reader = new StreamReader(input, Encoding.UTF8);
        var header = reader.ReadToEnd();
        if (header.Length != Prefix.Length + SecretBytes * 2 + 1 ||
            !header.StartsWith(Prefix, StringComparison.Ordinal) || header[^1] != '\n' ||
            header.AsSpan(Prefix.Length, SecretBytes * 2).ContainsAnyExcept(HexDigits))
            throw new InvalidOperationException("The approval secret file is invalid; preserve it for operator recovery.");
        return header.Substring(Prefix.Length, SecretBytes * 2);
    }
}
