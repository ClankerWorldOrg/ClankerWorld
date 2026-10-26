namespace ClankerWorld.Server;

/// <summary>Private installation files and folders, including their first write.</summary>
internal static class PrivateStorage
{
    private const UnixFileMode FilePermissions = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode DirectoryPermissions = FilePermissions | UnixFileMode.UserExecute;

    public static void EnsureDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else
        {
            Directory.CreateDirectory(path, DirectoryPermissions);
            var current = File.GetUnixFileMode(path);
            var restricted = current & DirectoryPermissions;
            if (current != restricted) File.SetUnixFileMode(path, restricted);
        }
    }

    public static FileStream CreateNew(string path, FileShare share = FileShare.None)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = share };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = FilePermissions;
        return new FileStream(path, options);
    }

    public static void WriteAtomic(string temporary, string destination, bool overwrite, Action<FileStream> write)
    {
        try
        {
            using (var stream = CreateNew(temporary))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite);
            RestrictFile(destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void RestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, FilePermissions);
    }
}
