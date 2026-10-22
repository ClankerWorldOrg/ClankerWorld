namespace ClankerWorld.Tests;

internal sealed class ClientTestDirectory(string prefix) : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory(prefix);
    public string FullName => directory.FullName;
    public void Dispose() => directory.Delete(recursive: true);
}
