using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("off")]
    [InlineData("jev")]
    [InlineData("another-key")]
    public async Task HelperKeyDeletionIsRefusedUntilTheSavedHelperReleasesIt(string nextHelper)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-helper-key-delete-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var slotId = Guid.NewGuid().ToString("N");
            var otherSlotId = Guid.NewGuid().ToString("N");
            const string secret = "synthetic-helper-key-to-retain";
            const string deletePath = "/api/v1/owner/providers/slots/delete";
            const string helperPath = "/api/v1/owner/control/routine-helper";
            var delete = new OwnerCredentialSlotDeletionAction(slotId);
            string deviceId;
            RoutineHelperSettings helper;
            using (var host = new ServerWebApplicationFactory(directory.FullName, null, gameWorld: true))
            using (var client = host.CreateClient())
            {
                deviceId = (await StartAndActivateAsync(host, client, signer)).DeviceId;
                var store = host.Services.GetRequiredService<ProviderConfigurationStore>();
                store.CreateCredentialSlot(new(slotId, "openai", "Helper", secret));
                store.CreateCredentialSlot(new(otherSlotId, "openai", "Other", "synthetic-other-helper-key"));
                Assert.DoesNotContain(store.CaptureStatus().Assignments!, item => item.CredentialSlotId == slotId);
                var runtime = host.Services.GetRequiredService<WorldRuntime>();
                runtime.Pause();
                var select = new OwnerRoutineHelperAction(runtime.Society.WorldId, "decisions", "gpt-6-luna", slotId);
                using var selected = await SendSignedAsync(host, client, signer, deviceId, helperPath,
                    select, OwnerHttpBinding.RoutineHelperPayload(select));
                Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
                helper = runtime.RoutineHelper;
                var worldBytes = WorldRuntimeCodec.Encode(runtime.ExportState());
                var keyBytes = File.ReadAllBytes(store.Path);
                var revision = store.CaptureStatus().Revision;

                using var refused = await SendSignedAsync(host, client, signer, deviceId, deletePath,
                    delete, OwnerHttpBinding.CredentialSlotDeletionPayload(delete));
                Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
                Assert.Contains("routine helper", await refused.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
                Assert.Equal(revision, store.CaptureStatus().Revision);
                Assert.Equal(keyBytes, File.ReadAllBytes(store.Path));
                Assert.Equal(worldBytes, WorldRuntimeCodec.Encode(runtime.ExportState()));
            }

            using (var host = new ServerWebApplicationFactory(directory.FullName, null, gameWorld: true))
            using (var client = host.CreateClient())
            {
                var store = host.Services.GetRequiredService<ProviderConfigurationStore>();
                var runtime = host.Services.GetRequiredService<WorldRuntime>();
                Assert.Equal(helper, runtime.RoutineHelper);
                Assert.Equal(secret, Assert.Single(store.CaptureRuntimeConfiguration().CredentialSlots!, item => item.Id == slotId).ApiKey);
                using var stillRefused = await SendSignedAsync(host, client, signer, deviceId, deletePath,
                    delete, OwnerHttpBinding.CredentialSlotDeletionPayload(delete));
                Assert.Equal(HttpStatusCode.Conflict, stillRefused.StatusCode);

                var select = nextHelper switch
                {
                    "off" => new OwnerRoutineHelperAction(runtime.Society.WorldId, "off", string.Empty),
                    "jev" => new OwnerRoutineHelperAction(runtime.Society.WorldId, "jev", RoutineHelperSettings.Jev.Model),
                    _ => new OwnerRoutineHelperAction(runtime.Society.WorldId, "decisions", "gpt-6-luna", otherSlotId),
                };
                using var selected = await SendSignedAsync(host, client, signer, deviceId, helperPath,
                    select, OwnerHttpBinding.RoutineHelperPayload(select));
                Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
                helper = runtime.RoutineHelper;
                var worldBytes = WorldRuntimeCodec.Encode(runtime.ExportState());
                using var deleted = await SendSignedAsync(host, client, signer, deviceId, deletePath,
                    delete, OwnerHttpBinding.CredentialSlotDeletionPayload(delete));
                Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
                var status = await deleted.Content.ReadFromJsonAsync<OwnerProviderConfigurationStatus>();
                Assert.DoesNotContain(status!.CredentialSlots!, item => item.Id == slotId);
                Assert.Equal(worldBytes, WorldRuntimeCodec.Encode(runtime.ExportState()));
                Assert.DoesNotContain(secret, File.ReadAllText(store.Path), StringComparison.Ordinal);
            }

            using var restarted = new ServerWebApplicationFactory(directory.FullName, null, gameWorld: true);
            using var restartedClient = restarted.CreateClient();
            var restoredStore = restarted.Services.GetRequiredService<ProviderConfigurationStore>();
            Assert.DoesNotContain(restoredStore.CaptureStatus().CredentialSlots!, item => item.Id == slotId);
            Assert.Contains(restoredStore.CaptureStatus().CredentialSlots!, item => item.Id == otherSlotId);
            Assert.Equal(helper, restarted.Services.GetRequiredService<WorldRuntime>().RoutineHelper);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
