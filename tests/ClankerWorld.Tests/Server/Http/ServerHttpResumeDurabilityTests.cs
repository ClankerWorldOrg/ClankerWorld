using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResumeRequiresDurableCheckpointEvenOnRetry(bool alreadyRunning)
    {
        var directory = Directory.CreateTempSubdirectory("durable-resume-");
        try
        {
            using (var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: true))
            using (var client = host.CreateClient())
            using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                var device = await StartAndActivateAsync(host, client, key);
                var runtime = host.Services.GetRequiredService<WorldRuntime>();
                var file = host.Services.GetRequiredService<WorldStateFile>();
                runtime.Pause();
                file.Save(runtime);
                if (alreadyRunning) runtime.Resume(); // Reproduce memory/disk divergence from an older failed request.
                var prior = file.Path + ".prior";
                File.Copy(file.Path, prior);
                // Reproduce Windows sharing denial; use a directory obstruction on Unix.
                using var locked = OperatingSystem.IsWindows()
                    ? new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
                if (locked is null)
                {
                    File.Delete(file.Path);
                    Directory.CreateDirectory(file.Path);
                }
                async Task<HttpResponseMessage> Resume() => await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/control/resume", new OwnerControlAction("resume"), OwnerHttpBinding.EmptyPayload("resume"));
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        using var failed = await Resume();
                        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    Assert.Equal(!alreadyRunning, runtime.Society.IsPaused);
                    Assert.Equal(0, runtime.WorldTick);
                    using var old = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(prior)));
                    Assert.True(old.Society.IsPaused);
                }
                locked?.Dispose();
                if (!OperatingSystem.IsWindows()) Directory.Delete(file.Path);
                File.Move(prior, file.Path, overwrite: true);
                using var recovered = await Resume();
                Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
                var receipt = await recovered.Content.ReadFromJsonAsync<OwnerControlReceipt>();
                Assert.False(receipt!.IsPaused);
                Assert.Equal(!alreadyRunning, receipt.Changed);
                Assert.False(runtime.Society.IsPaused);
                Assert.Equal(0, runtime.WorldTick);
            }
            using var restarted = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: true);
            using var restartedClient = restarted.CreateClient();
            var restored = restarted.Services.GetRequiredService<WorldRuntime>();
            Assert.False(restored.Society.IsPaused);
            Assert.Equal(0, restored.WorldTick);
        }
        finally { directory.Delete(recursive: true); }
    }
}
