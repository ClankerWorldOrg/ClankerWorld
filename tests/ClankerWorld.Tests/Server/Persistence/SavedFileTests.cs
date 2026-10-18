using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class SavedFileTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void BoundedReadPreservesExactBytesAtTheLimit(int length)
    {
        var directory = Directory.CreateTempSubdirectory("bounded-save-");
        try
        {
            var path = Path.Combine(directory.FullName, "data");
            var bytes = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
            File.WriteAllBytes(path, bytes);
            Assert.Equal(bytes, SavedFile.Read(path, 16));
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("checkpoint", SavedFile.CheckpointLimit)]
    [InlineData("catalog", SavedFile.CatalogLimit)]
    [InlineData("metadata", SavedFile.MetadataLimit)]
    [InlineData("history", SavedFile.HistoryLimit)]
    public void OversizedFilesAreRefusedBeforeParsingAndKept(string kind, int limit)
    {
        var directory = Directory.CreateTempSubdirectory("oversized-save-");
        try
        {
            var checkpoint = Path.Combine(directory.FullName, "world.save");
            var history = Path.Combine(checkpoint + ".history", new string('a', 64) + ".json");
            var catalog = Path.Combine(checkpoint + ".worlds", "catalog.json");
            var path = kind switch
            {
                "catalog" => catalog,
                "metadata" => checkpoint + ".autosave.json",
                "history" => history,
                _ => checkpoint,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // A sparse invalid file proves the size gate precedes the JSON reader,
            // without allocating or writing hundreds of megabytes in a test.
            using (var file = File.Create(path)) { file.WriteByte(0xff); file.SetLength((long)limit + 1); }
            var stateFile = new WorldStateFile(checkpoint);
            SavedFileLimitException failure;
            switch (kind)
            {
                case "catalog":
                    failure = Assert.Throws<SavedFileLimitException>(() => WorldCatalogStore.ReadActiveIdentity(checkpoint));
                    break;
                case "metadata":
                    failure = Assert.Throws<SavedFileLimitException>(() => new WorldAutosaveStore(checkpoint, "bounded"));
                    break;
                case "history":
                    using (var world = new WorldRuntime("bounded"))
                        failure = Assert.Throws<SavedFileLimitException>(() => stateFile.VerifyRequiredHistory(
                            world.ExportState() with { HistoryArchiveHead = new string('a', 64) }));
                    break;
                default:
                    failure = Assert.Throws<SavedFileLimitException>(() => stateFile.LoadOrCreate("bounded"));
                    break;
            }
            Assert.Contains("exceeds the supported size", failure.Message);
            Assert.Equal((long)limit + 1, new FileInfo(path).Length);
            using var original = File.OpenRead(path);
            Assert.Equal(0xff, original.ReadByte());
            Assert.Single(Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories));
        }
        finally { directory.Delete(true); }
    }
}
