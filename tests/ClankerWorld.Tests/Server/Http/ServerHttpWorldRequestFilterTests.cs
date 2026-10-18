using ClankerWorld.Server.Endpoints;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.World;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WorldRequestFilterAuthenticatesThenHoldsSelectionThroughTheHandler(bool otherWorld, bool invalidProof)
    {
        var directory = Directory.CreateTempSubdirectory("world-filter-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            client.BaseAddress = new Uri("http://127.0.0.1/");
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var before = WorldRuntimeCodec.Encode(runtime.ExportState());
            var action = new OwnerAgingOverrideAction(1, otherWorld ? "another-world" : runtime.Society.WorldId);
            const string path = "/api/v1/owner/control/aging-override";
            var request = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path, action,
                OwnerHttpBinding.AgingOverridePayload(action));
            if (invalidProof) request = request with { SignatureBase64 = Convert.ToBase64String(new byte[64]) };
            var http = new DefaultHttpContext { RequestServices = host.Services };
            http.Request.Method = "POST";
            http.Request.Path = path;
            var context = new DefaultEndpointFilterInvocationContext(http, request);
            var gate = host.Services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate;
            var called = false;
            var filter = new OwnerWorldRequestFilter<OwnerAgingOverrideAction>(0, value => value.WorldId,
                OwnerHttpBinding.AgingOverridePayload, (_, _) => Results.Conflict());
            var result = await filter.InvokeAsync(context, _ =>
            {
                called = true;
                Assert.True(Monitor.IsEntered(gate));
                var canSelect = false;
                var selectionThread = new Thread(() =>
                {
                    canSelect = Monitor.TryEnter(gate);
                    if (canSelect) Monitor.Exit(gate);
                });
                selectionThread.Start();
                Assert.True(selectionThread.Join(TimeSpan.FromSeconds(5)));
                Assert.False(canSelect);
                return ValueTask.FromResult<object?>(Results.Ok());
            });
            Assert.Equal(!otherWorld, called);
            Assert.Equal(invalidProof ? 401 : otherWorld ? 409 : 200,
                Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.False(Monitor.IsEntered(gate));
        }
        finally { directory.Delete(recursive: true); }
    }
}
