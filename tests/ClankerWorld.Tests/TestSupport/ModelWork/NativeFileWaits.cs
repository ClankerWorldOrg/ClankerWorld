namespace ClankerWorld.Tests;

internal static class NativeFileWaits
{
    public static async Task<FileStream> AcquireForRenameAsync(string path, Action? sharingViolation = null)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete); }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            {
                sharingViolation?.Invoke();
                await Task.Delay(10, deadline.Token);
            }
        }
    }
}
