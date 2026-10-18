namespace ClankerWorld.Server.Saves;

/// <summary>Read untrusted save files through one bounded, unchanged file handle.</summary>
public static class SavedFile
{
    public const int CheckpointLimit = 256 * 1024 * 1024;
    public const int HistoryLimit = 64 * 1024 * 1024;
    public const int CatalogLimit = 16 * 1024 * 1024;
    public const int MetadataLimit = 4 * 1024 * 1024;

    public static byte[] Read(string path, int maximumBytes = MetadataLimit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        CheckLength(stream.Length, maximumBytes);
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        // Also refuse a file extended through an already-open writer on Unix.
        if (stream.ReadByte() != -1) throw new InvalidDataException("The save file changed while it was being read. Try again.");
        return bytes;
    }

    public static void CheckLength(long length, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (length > maximumBytes)
            throw new InvalidDataException($"The save file exceeds the supported size of {maximumBytes / (1024 * 1024)} MiB. Its original file has been kept.");
    }
}
