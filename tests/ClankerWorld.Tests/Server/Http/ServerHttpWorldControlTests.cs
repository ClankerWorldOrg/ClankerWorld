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
    [Theory]
    [InlineData("resume")]
    [InlineData("pause")]
    [InlineData("start-world")]
    [InlineData("first-layout")]
    public async Task DelayedWorldControlsPreserveTheOtherSelectedWorldAndItsCheckpoint(string operation)
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
            var path = operation == "first-layout" ? "/api/v1/owner/town/first-layout" : "/api/v1/owner/control/" + operation;
            object Action(string worldId) => operation == "first-layout"
                ? new OwnerFirstTownLayoutAction(0, 0, worldId) : new OwnerWorldControlAction(operation, worldId);
            string Payload(object action) => action is OwnerFirstTownLayoutAction layout
                ? OwnerHttpBinding.FirstTownLayoutPayload(layout) : OwnerHttpBinding.WorldControl((OwnerWorldControlAction)action);
            var action = Action(runtime.Society.WorldId);
            var held = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path,
                action, Payload(action));
            host.Services.GetRequiredService<WorldSelectionCoordinator>().SelectApprovedModels(entryB.Id);
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var checkpoint = File.ReadAllBytes(file.Path);
            var before = WorldRuntimeCodec.Encode(runtime.ExportState());

            using var response = await client.PostAsJsonAsync(path, held);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("The world changed.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.True(runtime.Society.IsPaused);
            var tampered = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path, action, Payload(action));
            using var wrongProof = await client.PostAsJsonAsync(path, tampered with { Action = Action(runtime.Society.WorldId) });
            Assert.Equal(HttpStatusCode.Unauthorized, wrongProof.StatusCode);
            var legacy = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path, Action(null!),
                operation == "first-layout" ? "clankerworld.owner-first-town-layout.v1\nx=0\ny=0" : OwnerHttpBinding.EmptyPayload(operation));
            using var missingWorld = await client.PostAsJsonAsync(path, legacy);
            Assert.Equal(HttpStatusCode.BadRequest, missingWorld.StatusCode);
            Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpoint, File.ReadAllBytes(file.Path));

            // A fresh request still controls B, using its observed ID rather than the pairing identity.
            using var signer = new ActionCompatibilitySigner(key);
            var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
            var api = new ClankerWorld.GodotClient.Networking.OwnerWorldApi(client);
            var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
            var receipt = await api.SetPausedAsync(uri, new(identity.ServerAuthorityId, identity.WorldId), device.DeviceId,
                runtime.Society.WorldId, false, signer, default);
            Assert.False(receipt.IsPaused);
        }
        finally { directory.Delete(recursive: true); }
    }
}
