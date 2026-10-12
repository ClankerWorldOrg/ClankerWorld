using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public void WorldsGetAThumbnailWhenCreatedAndOlderWorldsWhenFirstListed()
    {
        var directory = Directory.CreateTempSubdirectory("world-thumbnails-");
        try
        {
            string catalogPath;
            Dictionary<string, WorldThumbnail> created;
            using (var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true))
            using (var client = host.CreateClient())
            {
                var runtime = host.Services.GetRequiredService<WorldRuntime>();
                runtime.Pause();
                var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
                using var other = new WorldRuntime("world-thumbnail-other");
                var entry = catalog.Add("Other", other.ExportState());
                Assert.Equal(WorldThumbnail.From(other.ExportState().Map), entry.Thumbnail);
                Assert.Equal(WorldThumbnail.From(runtime.ExportState().Map), catalog.Active().Thumbnail);
                created = catalog.Capture().Worlds.ToDictionary(world => world.Id, world => world.Thumbnail!);
                catalogPath = Path.Combine(host.Services.GetRequiredService<WorldStateFile>().Path + ".worlds", "catalog.json");
            }

            // A catalog written before thumbnails existed has none.
            var older = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
            foreach (var world in older["Worlds"]!.AsArray()) world!.AsObject().Remove("Thumbnail");
            File.WriteAllText(catalogPath, older.ToJsonString());

            using (var restarted = new ServerWebApplicationFactory(directory.FullName, gameWorld: true))
            using (var client = restarted.CreateClient())
            {
                var listed = restarted.Services.GetRequiredService<WorldSelectionCoordinator>().List();
                Assert.Equal(2, listed.Worlds.Count);
                Assert.All(listed.Worlds, world => Assert.Equal(created[world.Id], world.Thumbnail));
                // The catalog keeps them, so the next list needs no work for them.
                var stored = JsonNode.Parse(File.ReadAllText(catalogPath))!["Worlds"]!.AsArray();
                Assert.All(stored, world => Assert.NotNull(world!["Thumbnail"]));
                // A repeat list reuses the checked checkpoint and keeps the pictures.
                var relisted = restarted.Services.GetRequiredService<WorldSelectionCoordinator>().List();
                Assert.All(relisted.Worlds, world => Assert.Equal(created[world.Id], world.Thumbnail));
            }
        }
        finally { directory.Delete(recursive: true); }
    }
}
