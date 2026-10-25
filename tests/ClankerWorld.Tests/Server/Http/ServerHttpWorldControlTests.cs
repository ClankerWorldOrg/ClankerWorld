using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task DelayedResumePreservesTheOtherSelectedWorldAndItsCheckpoint()
    {
        var directory = Directory.CreateTempSubdirectory("delayed-world-control-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            runtime.Pause();
            var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
            using var other = new WorldRuntime("delayed-world-control-B");
            other.Pause();
            var entryB = catalog.Add("Other", other.ExportState());
            const string path = "/api/v1/owner/control/resume";
            var action = new OwnerControlAction("resume");
            var held = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path,
                action, OwnerHttpBinding.EmptyPayload("resume"));
            host.Services.GetRequiredService<WorldSelectionCoordinator>().SelectApprovedModels(entryB.Id);
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var checkpoint = File.ReadAllBytes(file.Path);
            var before = WorldRuntimeCodec.Encode(runtime.ExportState());

            using var response = await client.PostAsJsonAsync(path, held);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.True(runtime.Society.IsPaused);
            Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpoint, File.ReadAllBytes(file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }
}
