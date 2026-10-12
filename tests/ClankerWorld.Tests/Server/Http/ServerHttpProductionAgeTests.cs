using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Content;
using ClankerWorld.Server.Pairing;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(SocietyAgeBand.Adolescent)]
    [InlineData(SocietyAgeBand.Adult)]
    public async Task SignedProductionRequestEnforcesWorkerAgeWithoutChangingRejectedWorlds(SocietyAgeBand age)
    {
        var prepared = await ProductionAgeTests.PrepareAsync(age);
        var directory = Directory.CreateTempSubdirectory("production-age-http-");
        try
        {
            var path = Path.Combine(directory.FullName, "runtime.json");
            File.WriteAllBytes(path, WorldRuntimeCodec.Encode(prepared.State));
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var world = host.Services.GetRequiredService<WorldRuntime>();
            var before = WorldRuntimeCodec.Encode(world.ExportState());
            var savedBefore = File.ReadAllBytes(path);
            var action = new OwnerProductionStartAction(prepared.RecipeId, prepared.WorkstationId, prepared.WorkerId);
            using var response = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/production/start", action, OwnerContentBinding.ProductionStartPayload(action));
            if (age is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var receipt = await response.Content.ReadFromJsonAsync<ProductionStartResult>();
                Assert.True(receipt!.Applied, receipt.Failure);
                Assert.NotNull(receipt.JobId);
                var saved = WorldRuntimeCodec.Decode(File.ReadAllBytes(path));
                Assert.Equal(before, savedBefore);
                await ProductionAgeTests.CompleteAfterReloadAsync(saved, receipt.JobId);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var failure = await response.Content.ReadFromJsonAsync<OwnerControlFailure>();
                Assert.Equal("production_rejected", failure!.Code);
                Assert.Contains("too young", failure.Detail, StringComparison.Ordinal);
                Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
                Assert.Equal(savedBefore, File.ReadAllBytes(path));
                using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(savedBefore));
                Assert.False(reloaded.StartProduction(action.RecipeId, action.BuildingInstanceId, action.WorkerId).Applied);
                Assert.Equal(savedBefore, WorldRuntimeCodec.Encode(reloaded.ExportState()));
            }
        }
        finally { directory.Delete(recursive: true); }
    }
}
