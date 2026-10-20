using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    private static async Task<OwnerManualSaveAction> ApprovedModelLoadAsync(ServerWebApplicationFactory host,
        HttpClient client, ECDsa key, string deviceId, string operation, string id)
    {
        var path = operation == "load" ? "/api/v1/owner/saves/load" : "/api/v1/owner/worlds/select";
        var inspect = new OwnerManualSaveAction(operation == "load" ? "inspect-save-models" : "inspect-world-models", id);
        using var response = await SendSignedAsync(host, client, key, deviceId, path, inspect,
            OwnerHttpBinding.ManualSavePayload(inspect));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var models = await response.Content.ReadFromJsonAsync<OwnerModelSettingsPreview>();
        Assert.NotNull(models);
        return new(operation, id, models.Approval);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadingChangedModelSettingsNeedsExactSignedApprovalBeforeAnyMutation(bool selectWorld)
    {
        var directory = Directory.CreateTempSubdirectory("model-settings-consent-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            runtime.Pause();
            var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
            var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
            var saves = host.Services.GetRequiredService<ManualWorldSaveStore>();
            var autosave = host.Services.GetRequiredService<WorldAutosaveStore>();
            var file = host.Services.GetRequiredService<WorldStateFile>();
            string target;
            if (selectWorld)
            {
                using var other = new WorldRuntime("model-settings-other");
                other.Pause();
                other.SetRoutineHelper(RoutineHelperSettings.Off);
                target = catalog.Add("Other settings", other.ExportState()).Id;
            }
            else
            {
                var current = runtime.RoutineHelper;
                runtime.SetRoutineHelper(RoutineHelperSettings.Off);
                target = saves.Create("Helper off", runtime,
                    providers.CaptureRuntimeConfiguration().Assignments ?? [], autosave.Capture()).Id;
                runtime.SetRoutineHelper(current);
            }
            file.Save(runtime);
            var before = WorldRuntimeCodec.Encode(runtime.ExportState());
            var active = File.ReadAllBytes(file.Path);
            var entries = saves.List(runtime.Society.WorldId).Select(item => item.Id).ToArray();
            var activeId = catalog.Active().Id;
            var path = selectWorld ? "/api/v1/owner/worlds/select" : "/api/v1/owner/saves/load";
            var action = new OwnerManualSaveAction(selectWorld ? "select-world" : "load", target);
            using (var refused = await SendSignedAsync(host, client, key, device.DeviceId, path, action,
                OwnerHttpBinding.ManualSavePayload(action)))
            {
                Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
                Assert.Contains("model_settings_confirmation_required", await refused.Content.ReadAsStringAsync());
            }
            var inspect = action with { Operation = selectWorld ? "inspect-world-models" : "inspect-save-models" };
            using var inspected = await SendSignedAsync(host, client, key, device.DeviceId, path, inspect,
                OwnerHttpBinding.ManualSavePayload(inspect));
            Assert.Equal(HttpStatusCode.OK, inspected.StatusCode);
            var preview = await inspected.Content.ReadFromJsonAsync<OwnerModelSettingsPreview>();
            Assert.NotNull(preview);
            Assert.True(preview.RequiresConfirmation);
            Assert.Contains("Routine helper: Off", preview.Details);
            var wrong = action with { ModelSettingsApproval = new string('0', 64) };
            using (var refused = await SendSignedAsync(host, client, key, device.DeviceId, path, wrong,
                OwnerHttpBinding.ManualSavePayload(wrong))) Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(active, File.ReadAllBytes(file.Path));
            Assert.Equal(activeId, catalog.Active().Id);
            Assert.Equal(entries, saves.List(runtime.Society.WorldId).Select(item => item.Id));
            var approved = action with { ModelSettingsApproval = preview.Approval };
            using var accepted = await SendSignedAsync(host, client, key, device.DeviceId, path, approved,
                OwnerHttpBinding.ManualSavePayload(approved));
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            Assert.Equal(RoutineHelperSettings.Off, runtime.RoutineHelper);
            Assert.True(runtime.Society.IsPaused);
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)));
            Assert.Equal(WorldRuntimeCodec.Encode(runtime.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally { directory.Delete(true); }
    }
}
