using System.Net;
using System.Security.Cryptography;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("overwrite")]
    [InlineData("timeline")]
    public async Task SignedSaveOperationsExplainOversizedTimelineWithoutChangingIt(string operation)
    {
        var directory = Directory.CreateTempSubdirectory("oversized-timeline-http-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            runtime.Pause();
            var saves = host.Services.GetRequiredService<ManualWorldSaveStore>();
            var first = saves.Create("First", runtime, []);
            var timeline = Assert.Single(Directory.GetFiles(directory.FullName, "timeline-*.json", SearchOption.AllDirectories));
            using (var stream = File.OpenWrite(timeline)) stream.SetLength((long)SavedFile.MetadataLimit + 1);
            var manualDirectory = Path.GetDirectoryName(timeline)!;
            var before = Directory.GetFiles(manualDirectory).ToDictionary(path => path, File.ReadAllBytes);
            var state = WorldRuntimeCodec.Encode(runtime.ExportState());
            using var response = operation == "timeline"
                ? await SendSignedAsync(host, client, key, device.DeviceId, "/api/v1/owner/saves/timeline",
                    new OwnerControlAction("save-timeline"), OwnerHttpBinding.EmptyPayload("save-timeline"))
                : await SendSignedAsync(host, client, key, device.DeviceId, "/api/v1/owner/saves/" + operation,
                    new OwnerManualSaveAction(operation, operation == "create" ? "Second" : first.Id),
                    OwnerActionPayload.ManualSave(new(operation, operation == "create" ? "Second" : first.Id)));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var message = await response.Content.ReadAsStringAsync();
            Assert.Contains("save_too_large", message);
            Assert.Contains("exceeds the supported size", message);
            Assert.DoesNotContain(directory.FullName, message);
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(manualDirectory).Order());
            foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(state, WorldRuntimeCodec.Encode(runtime.ExportState()));
        }
        finally { directory.Delete(true); }
    }
}
