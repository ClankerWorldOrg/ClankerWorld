using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Client = ClankerWorld.GodotClient.UI;
using ClientNetworking = ClankerWorld.GodotClient.Networking;
using ClientProtocol = ClankerWorld.GodotClient.Protocol;
using OwnerAuthorityIdentity = ClankerWorld.GodotClient.Pairing.OwnerAuthorityIdentity;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    [InlineData("start-world")]
    [InlineData("first-layout")]
    public async Task WorldControlsRequireAWorldBoundHostBeforePosting(string operation)
    {
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new LegacyWorldActionFilter(null))));
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new ActionCompatibilitySigner(key);
        var device = await StartAndActivateAsync(host, client, key);
        var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
        var authority = new OwnerAuthorityIdentity(identity.ServerAuthorityId, identity.WorldId);
        var runtime = host.Services.GetRequiredService<WorldRuntime>();
        var before = WorldRuntimeCodec.Encode(runtime.ExportState());
        var api = new ClientNetworking.OwnerWorldApi(client);
        var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
        Task Control() => operation switch
        {
            "first-layout" => api.AcceptFirstTownLayoutAsync(uri, authority, device.DeviceId,
                new(0, 0, runtime.Society.WorldId), signer, default),
            "start-world" => api.StartWorldAsync(uri, authority, device.DeviceId, runtime.Society.WorldId, signer, default),
            _ => api.SetPausedAsync(uri, authority, device.DeviceId, runtime.Society.WorldId, operation == "pause", signer, default),
        };
        await Assert.ThrowsAsync<OwnerActionCompatibilityException>(Control);
        Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("clankerworld.owner-autosave-configuration.v1", false)]
    [InlineData("clankerworld.owner-autosave-configuration.v2", true)]
    public async Task AutosaveClientRequiresTheWorldBoundFormatAndKeepsPairingUsable(string? advertisedDomain, bool supported)
    {
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true);
        var metadata = new LegacyWorldActionFilter(advertisedDomain);
        using var host = baseHost.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(metadata)));
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new ActionCompatibilitySigner(key);
        var device = await StartAndActivateAsync(host, client, key);
        var runtime = host.Services.GetRequiredService<WorldRuntime>();
        runtime.Pause();
        var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
        var authority = new OwnerAuthorityIdentity(identity.ServerAuthorityId, identity.WorldId);
        var api = new ClientNetworking.OwnerWorldApi(client);
        var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
        var before = await api.GetAutosaveSettingsAsync(uri, authority, device.DeviceId, signer, default);
        var action = new ClankerWorld.Contracts.OwnerAutosaveConfigurationAction(false, 1, 0, before.WorldId);
        if (supported)
        {
            var updated = await api.ConfigureAutosaveAsync(uri, authority, device.DeviceId, action, signer, default);
            Assert.Equal((before.WorldId, false, 1, 0),
                (updated.WorldId, updated.Enabled, updated.IntervalMinutes, updated.RotationCount));
            Assert.Equal(updated, await api.GetAutosaveSettingsAsync(uri, authority, device.DeviceId, signer, default));
        }
        else
        {
            var failure = await Assert.ThrowsAsync<OwnerActionCompatibilityException>(() =>
                api.ConfigureAutosaveAsync(uri, authority, device.DeviceId, action, signer, default));
            Assert.Contains("matching updates", ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(failure), StringComparison.Ordinal);
            Assert.Contains("pairing can stay", ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(failure), StringComparison.Ordinal);
            Assert.Equal(before, await api.GetAutosaveSettingsAsync(uri, authority, device.DeviceId, signer, default));
        }
        Assert.Equal(supported ? 1 : 0, metadata.AutosaveConfigurationRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("clankerworld.owner-world-creation.v1")]
    [InlineData("clankerworld.owner-world-creation.v2")]
    public async Task OlderActionHostAllowsReconnectButClientExplainsNewWorldUpdate(string? advertisedDomain)
    {
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true, legacyPace: false);
        var legacy = new LegacyWorldActionFilter(advertisedDomain);
        using var host = baseHost.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(legacy)));
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new ActionCompatibilitySigner(key);
        var device = await StartAndActivateAsync(host, client, key);
        var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
        var authority = new OwnerAuthorityIdentity(identity.ServerAuthorityId, identity.WorldId);
        // Compatibility depends on protocol replies, not elapsed runner time.
        var api = new ClientNetworking.OwnerWorldApi(client, new ManualTimeProvider());
        var uri = client.BaseAddress!;
        // Literal loopback is the production client's explicit test/development exception.
        uri = new UriBuilder(uri) { Host = "127.0.0.1" }.Uri;
        var before = WorldRuntimeCodec.Encode(host.Services.GetRequiredService<WorldRuntime>().ExportState());
        var reconnect = await ModelWorkHarness.WaitForSignalAsync(
            api.ReconnectAsync(uri, authority, device.DeviceId, 0, null, null, null, signer, default));
        Assert.NotNull(reconnect.Baseline.Snapshot);

        var action = new OwnerWorldCreationAction("Disposable", "compatibility", "Small", 50, false);
        // Reproduce the old host's actual strict v1 verification with a new v3 proof.
        using var failedAction = await SendSignedAsync(host, client, key, device.DeviceId,
            OwnerPairingEndpoints.OwnerWorldPreview, action, OwnerActionPayload.WorldCreation(action));
        Assert.Equal(HttpStatusCode.Unauthorized, failedAction.StatusCode);
        Assert.Equal(1, legacy.WorldActionRequests);

        var clientAction = new ClankerWorld.Contracts.OwnerWorldCreationAction(action.Name, action.Seed, action.Size, 50, false);
        var previewFailure = await Assert.ThrowsAsync<OwnerActionCompatibilityException>(() =>
            api.PreviewWorldAsync(uri, authority, device.DeviceId, clientAction, signer, default));
        await Assert.ThrowsAsync<OwnerActionCompatibilityException>(() =>
            api.CreateWorldAsync(uri, authority, device.DeviceId, clientAction, signer, default));
        var text = ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(previewFailure);
        Assert.Contains("matching updates", text, StringComparison.Ordinal);
        Assert.Contains("pairing can stay", text, StringComparison.Ordinal);
        Assert.DoesNotContain("not allowed", text, StringComparison.Ordinal);
        Assert.Equal(1, legacy.WorldActionRequests); // Neither client action was posted or downgraded.

        var agentId = "agent:" + Guid.NewGuid().ToString("N");
        var placement = new ClankerWorld.Contracts.OwnerAgentPlacementAction(agentId, 4, 2,
            new ClankerWorld.Contracts.OwnerProviderConfigurationAction("personal", "openai", "gpt-5-mini", null,
                false, agentId, null), null, null);
        var placementFailure = await Assert.ThrowsAsync<OwnerActionCompatibilityException>(() =>
            api.PlaceAgentAsync(uri, authority, device.DeviceId, placement, signer, default));
        Assert.Contains("matching updates", ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(placementFailure), StringComparison.Ordinal);
        Assert.Equal(0, legacy.AgentPlacementRequests); // The client must not send affiliation-free v1 placement.
        Assert.Equal(before, WorldRuntimeCodec.Encode(host.Services.GetRequiredService<WorldRuntime>().ExportState()));
        Assert.NotNull(await ModelWorkHarness.WaitForSignalAsync(
            api.ReconnectAsync(uri, authority, device.DeviceId, 0, null, null, null, signer, default)));
    }

    [Fact]
    public async Task MatchingClientPreviewsAndCreatesWithAdvancedOptionsAndStrictProofs()
    {
        using var host = new ServerWebApplicationFactory(null, gameWorld: true, legacyPace: false);
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new ActionCompatibilitySigner(key);
        var device = await StartAndActivateAsync(host, client, key);
        var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
        var authority = new OwnerAuthorityIdentity(identity.ServerAuthorityId, identity.WorldId);
        var api = new ClientNetworking.OwnerWorldApi(client);
        var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
        var action = new ClankerWorld.Contracts.OwnerWorldCreationAction("Disposable", "compatible-advanced", "Small", 50, false,
            "Dominant", "Temperate", false, "Abundant", "High", "Low", "High");
        var preview = await api.PreviewWorldAsync(uri, authority, device.DeviceId, action, signer, default);
        var createAction = action with
        {
            CandidateAttempt = preview.Coverage!.Attempt,
            ExpectedManifestDigest = preview.ManifestDigest,
            ExpectedMapLayersDigest = preview.MapLayersDigest,
        };
        var created = await api.CreateWorldAsync(uri, authority, device.DeviceId, createAction, signer, default);
        var state = host.Services.GetRequiredService<WorldRuntime>().ExportState();
        Assert.Equal(created.WorldId, state.Society.Society.WorldId);
        Assert.Equal(preview.ManifestDigest, state.Map.ManifestDigest);
        Assert.Equal(preview.MapLayersDigest, MapLayerManifestCodec.Digest(state.Map));
        Assert.Equal(GenerationAmount.High, state.Geography!.ForestCover);
        Assert.Equal(GenerationAmount.Low, state.Geography.MountainRelief);
        Assert.Equal(GenerationAmount.High, state.Geography.RiverAbundance);

        var serverAction = new OwnerWorldCreationAction(action.Name, action.Seed, action.Size, 50, false);
        var downgrade = LegacyWorldActionFilter.V1Payload(serverAction);
        using var refused = await SendSignedAsync(host, client, key, device.DeviceId,
            OwnerPairingEndpoints.OwnerWorldPreview, serverAction, downgrade);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(OwnerHttpBinding.WorldCreationPayloadDomain, ClientProtocol.OwnerWorldActionPayload.WorldCreationPayloadDomain);
        Assert.Equal(OwnerHttpBinding.AgentPlacementPayloadDomain, ClientProtocol.OwnerWorldActionPayload.AgentPlacementPayloadDomain);
    }

    [Fact]
    public async Task RevokedDeviceStillGetsAnAccessFailureBeforeCompatibilityIsConsidered()
    {
        using var host = new ServerWebApplicationFactory(null, gameWorld: true, legacyPace: false);
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new ActionCompatibilitySigner(key);
        var device = await StartAndActivateAsync(host, client, key);
        var authorityStore = host.Services.GetRequiredService<OwnerAuthorityStore>();
        Assert.True(authorityStore.RevokeDevice(device.DeviceId).IsSuccess);
        var identity = authorityStore.Identity;
        var authority = new OwnerAuthorityIdentity(identity.ServerAuthorityId, identity.WorldId);
        var api = new ClientNetworking.OwnerWorldApi(client);
        var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => api.PreviewWorldAsync(uri,
            authority, device.DeviceId, new("Disposable", "seed", "Small", 50, false), signer, default));
        Assert.Equal(HttpStatusCode.Forbidden, failure.StatusCode);
        Assert.Contains("not allowed", ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(failure), StringComparison.Ordinal);
    }

    // Emulate the pre-Advanced host at its signed HTTP boundary. Challenge issuance
    // and action verification use the real authority store/authorizer; only the
    // historical advertised metadata and exact v1 payload differ from the host.
    private sealed class LegacyWorldActionFilter(string? advertisedDomain) : IStartupFilter
    {
        public int WorldActionRequests { get; private set; }
        public int AgentPlacementRequests { get; private set; }
        public int AutosaveConfigurationRequests { get; private set; }

        public static string V1Payload(OwnerWorldCreationAction action) => string.Join('\n',
            OwnerActionPayload.WorldCreation(action).Split('\n').Take(10))
            .Replace("clankerworld.owner-world-creation.v3", "clankerworld.owner-world-creation.v1", StringComparison.Ordinal);

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (context.Request.Path == OwnerPairingEndpoints.ChallengeIssue)
                {
                    var request = (await context.Request.ReadFromJsonAsync<IssueOwnerChallengeHttpRequest>())!;
                    var authority = context.RequestServices.GetRequiredService<OwnerAuthorityStore>();
                    var result = authority.IssueChallenge(new(request.DeviceId, request.RequestId,
                        request.CanonicalProof, request.SignatureBase64));
                    context.RequestServices.GetRequiredService<OwnerAuthorityStateFile>().Save(authority);
                    await (result.IsSuccess ? Results.Ok(result.Value! with
                    {
                        SupportedActionPayloads = advertisedDomain is null ? null : [advertisedDomain],
                    }) : OwnerFailures.ToHttpResult(result.Failure)).ExecuteAsync(context);
                    return;
                }
                if (context.Request.Path == OwnerPairingEndpoints.OwnerWorldPreview ||
                    context.Request.Path == OwnerPairingEndpoints.OwnerWorldCreate)
                {
                    WorldActionRequests++;
                    var request = (await context.Request.ReadFromJsonAsync<OwnerSignedHttpRequest<OwnerWorldCreationAction>>())!;
                    var result = context.RequestServices.GetRequiredService<OwnerRequestAuthorizer>()
                        .Authorize(request, "POST", context.Request.Path, V1Payload(request.Action));
                    await (result.IsSuccess ? Results.Ok(new { accepted = true }) :
                        OwnerFailures.ToHttpResult(result.Failure)).ExecuteAsync(context);
                    return;
                }
                if (context.Request.Path == OwnerPairingEndpoints.OwnerAgentPlace)
                {
                    AgentPlacementRequests++;
                }
                if (context.Request.Path == OwnerPairingEndpoints.OwnerAutosaveConfigure)
                {
                    AutosaveConfigurationRequests++;
                }
                await continuation(context);
            });
            next(app);
        };
    }

    internal sealed class ActionCompatibilitySigner(ECDsa key) : IOwnerDeviceSigner
    {
        public string PublicKeySpkiBase64 => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        public string PublicKeyFingerprint => OwnerPairingProtocol.CreatePublicKeyFingerprint(key.ExportSubjectPublicKeyInfo());
        public string SignCanonicalProof(string proof) => Sign(key, proof);
        public void Dispose() { } // The surrounding test owns the key.
    }
}
