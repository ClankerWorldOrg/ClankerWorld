using System.Net;
using System.Security.Cryptography;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JevHelperRejectsAMissingOrOtherProvidersKeyWithoutChangingTheWorld(bool existingOpenAiKey)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-typesafe-selection-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var host = new ServerWebApplicationFactory(directory.FullName, null, gameWorld: true);
            using var client = host.CreateClient();
            var device = await StartAndActivateAsync(host, client, signer);
            var store = host.Services.GetRequiredService<ProviderConfigurationStore>();
            var slotId = Guid.NewGuid().ToString("N");
            if (existingOpenAiKey) store.CreateCredentialSlot(new(slotId, "openai", "OpenAI account", "synthetic-openai-key"));
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            runtime.Pause();
            var before = WorldRuntimeCodec.Encode(runtime.ExportState());
            var keys = File.ReadAllBytes(store.Path);
            var action = new OwnerRoutineHelperAction(runtime.Society.WorldId, "jev", PlayerDecisionProviders.DefaultJevModel, slotId);
            const string endpoint = "/api/v1/owner/control/routine-helper";
            using var refused = await SendSignedAsync(host, client, signer, device.DeviceId,
                endpoint, action, OwnerHttpBinding.RoutineHelperPayload(action));
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Contains("saved TypeSafe key", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(keys, File.ReadAllBytes(store.Path));
        }
        finally { directory.Delete(recursive: true); }
    }
}
