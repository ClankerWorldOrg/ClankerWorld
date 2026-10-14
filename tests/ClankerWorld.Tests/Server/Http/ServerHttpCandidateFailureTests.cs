using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClankerWorld.GodotClient.Pairing;
using Client = ClankerWorld.GodotClient.UI;
using ClientNetworking = ClankerWorld.GodotClient.Networking;
using ClientProtocol = ClankerWorld.GodotClient.Protocol;
using OwnerAuthorityIdentity = ClankerWorld.GodotClient.Pairing.OwnerAuthorityIdentity;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task PreviewAndCreateKeepThePlayableMapAndShowItsUnavailableAttempt()
    {
        var attempted = new List<GeographyOptions>();
        // Control clearing failures at the generation boundary, not the HTTP
        // response. Every available map still uses exact normal generation.
        WorldMap GenerateWithUnavailableClearing(GeographyOptions options)
        {
            attempted.Add(options);
            if (options.Seed == "controlled-no-clearing" ||
                options.Seed == "audit-town-2" && options.CandidateAttempt == 2)
                throw new GeographyClearingUnavailableException();
            return GeographyCandidateSelector.GenerateCandidate(options);
        }
        void AssertAttemptSequence(params int[] expected)
        {
            Assert.Equal(expected, attempted.Select(options => options.CandidateAttempt));
            Assert.All(attempted, options =>
                Assert.Equal(GeographyGenerator.CurrentBalancedVisibilityVersion, options.BalancedVisibilityVersion));
            attempted.Clear();
        }
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true, legacyPace: false);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<WorldSelectionCoordinator>(provider => new WorldSelectionCoordinator(
                provider.GetRequiredService<WorldCatalogStore>(),
                provider.GetRequiredService<WorldRuntime>(),
                provider.GetRequiredService<WorldStateFile>(),
                provider.GetRequiredService<ProviderConfigurationStore>(),
                provider.GetRequiredService<WorldAutosaveStore>(),
                provider.GetRequiredService<WorldJevPolicy>(),
                provider.GetRequiredService<ILogger<WorldSelectionCoordinator>>(),
                _ => provider.GetRequiredService<IDecisionProvider>(),
                provider.GetRequiredService<ManualWorldSaveStore>(),
                options => GeographyCandidateSelector.Select(options, GenerateWithUnavailableClearing)))));
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await StartAndActivateAsync(host, client, key);
        var oldWorld = host.Services.GetRequiredService<WorldRuntime>();
        var oldBytes = WorldRuntimeCodec.Encode(oldWorld.ExportState());
        var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
        var oldCatalog = JsonSerializer.SerializeToUtf8Bytes(catalog.Capture(), WebJsonOptions);
        var authorityIdentity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
        var authority = new OwnerAuthorityIdentity(authorityIdentity.ServerAuthorityId, authorityIdentity.WorldId);
        using var signer = new ActionCompatibilitySigner(key);
        var api = new ClientNetworking.OwnerWorldApi(client);
        var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
        foreach (var forestCover in new[] { "High", "Normal" })
        {
            var expectedAttempts = forestCover == "High" ? new[] { 0 } : new[] { 0, 1, 2 };
            var unavailable = new OwnerWorldCreationAction("No clearing", "controlled-no-clearing", "Small", 80, true,
                LatitudeCooling: false, ForestCover: forestCover, MountainRelief: "High");
            var unavailableCreate = unavailable with
            {
                CandidateAttempt = 0,
                ExpectedManifestDigest = "valid-shaped-manifest",
                ExpectedMapLayersDigest = "valid-shaped-layers",
            };
            foreach (var (path, request) in new[]
            {
                ("/api/v1/owner/worlds/preview", unavailable),
                ("/api/v1/owner/worlds/create", unavailableCreate),
            })
            {
                using var refusedGeneration = await SendSignedAsync(host, client, key, device.DeviceId,
                    path, request, OwnerHttpBinding.WorldCreationPayload(request));
                Assert.Equal(HttpStatusCode.Conflict, refusedGeneration.StatusCode);
                var failureBody = JsonSerializer.Deserialize<OwnerControlFailure>(
                    await refusedGeneration.Content.ReadAsByteArrayAsync(), WebJsonOptions)!;
                Assert.Equal("no_playable_candidate", failureBody.Code);
                Assert.Equal("There is no room for a first Town with these settings.", failureBody.Detail);
                AssertAttemptSequence(expectedAttempts);
            }
            var unavailableClientAction = new ClankerWorld.Contracts.OwnerWorldCreationAction("No clearing", "controlled-no-clearing", "Small", 80, true,
                LatitudeCooling: false, ForestCover: forestCover, MountainRelief: "High");
            var previewFailure = await Assert.ThrowsAsync<OwnerWorldGenerationException>(() =>
                api.PreviewWorldAsync(uri, authority, device.DeviceId, unavailableClientAction, signer, default));
            AssertAttemptSequence(expectedAttempts);
            var createFailure = await Assert.ThrowsAsync<OwnerWorldGenerationException>(() =>
                api.CreateWorldAsync(uri, authority, device.DeviceId, unavailableClientAction with
                {
                    CandidateAttempt = 0,
                    ExpectedManifestDigest = "valid-shaped-manifest",
                    ExpectedMapLayersDigest = "valid-shaped-layers",
                }, signer, default));
            AssertAttemptSequence(expectedAttempts);
            Assert.Equal(HttpStatusCode.Conflict, previewFailure.StatusCode);
            Assert.Equal(Client.GameUiText.FriendlyFailure(previewFailure), Client.GameUiText.FriendlyFailure(createFailure));
            Assert.Contains("Choose another seed", Client.GameUiText.FriendlyFailure(previewFailure), StringComparison.Ordinal);
            Assert.DoesNotContain("starting clearing", Client.GameUiText.FriendlyFailure(previewFailure), StringComparison.Ordinal);
            Assert.Equal(oldCatalog, JsonSerializer.SerializeToUtf8Bytes(catalog.Capture(), WebJsonOptions));
            Assert.Equal(oldBytes, WorldRuntimeCodec.Encode(oldWorld.ExportState()));
            Assert.Equal(authorityIdentity, host.Services.GetRequiredService<OwnerAuthorityStore>().Identity);
        }
        var action = new OwnerWorldCreationAction("Playable survivors", "audit-town-2", "Small", 80, true,
            LatitudeCooling: false, MountainRelief: "High");

        using var previewed = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/preview", action, OwnerHttpBinding.WorldCreationPayload(action));
        Assert.Equal(HttpStatusCode.OK, previewed.StatusCode);
        AssertAttemptSequence(0, 1, 2);
        var response = await previewed.Content.ReadAsByteArrayAsync();
        var preview = JsonSerializer.Deserialize<WorldPreviewView>(response, WebJsonOptions)!;
        Assert.Collection(preview.Candidates, candidate => Assert.Equal(0, candidate.Attempt),
            candidate => Assert.Equal(1, candidate.Attempt));
        var failure = Assert.Single(preview.FailedCandidates);
        Assert.Equal(2, failure.Attempt);
        Assert.Equal("no-clearing", failure.Reason);
        var selected = Assert.IsType<ClankerWorld.Contracts.OwnerWorldCandidateReport>(preview.Coverage);
        Assert.Equal(0, selected.Attempt);
        Assert.True(selected.MeetsTargets);
        var direct = GeographyCandidateSelector.GenerateCandidate(new GeographyOptions(action.Seed, WorldSizePreset.Small,
            WaterPercent: 80, LatitudeCooling: false, HydrologyVersion: GeographyGenerator.CurrentHydrologyVersion,
            MountainRelief: GenerationAmount.High, CandidateAttempt: selected.Attempt));
        Assert.Equal(direct.ManifestDigest, preview.ManifestDigest);
        Assert.Equal(MapLayerManifestCodec.Digest(direct), preview.MapLayersDigest);
        Assert.Equal(oldBytes, WorldRuntimeCodec.Encode(oldWorld.ExportState()));

        // The real owner payload reaches the independent client DTO; older
        // previews without failure metadata remain readable with an empty list.
        var ownerPreview = JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldPreview>(response,
            WebJsonOptions)!;
        Assert.Equal(2, Assert.Single(ownerPreview.FailedCandidates).Attempt);
        Assert.Equal(preview.ManifestDigest, ownerPreview.ManifestDigest);
        var legacyPayload = JsonNode.Parse(response)!.AsObject();
        Assert.True(legacyPayload.Remove("failedCandidates"));
        var olderPreview = JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldPreview>(
            legacyPayload.ToJsonString(), WebJsonOptions)!;
        Assert.Empty(olderPreview.FailedCandidates);
        Assert.Equal(ownerPreview.Coverage, olderPreview.Coverage);
        Assert.Equal(ownerPreview.ManifestDigest, olderPreview.ManifestDigest);

        var create = action with
        {
            CandidateAttempt = selected.Attempt,
            ExpectedManifestDigest = preview.ManifestDigest,
            ExpectedMapLayersDigest = preview.MapLayersDigest,
        };
        var wrongAttempt = create with { CandidateAttempt = failure.Attempt };
        using var refused = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/create", wrongAttempt, OwnerHttpBinding.WorldCreationPayload(wrongAttempt));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        AssertAttemptSequence(0, 1, 2);
        var wrongClientAttempt = new ClankerWorld.Contracts.OwnerWorldCreationAction(action.Name, action.Seed, action.Size, 80, true,
            LatitudeCooling: false, MountainRelief: "High", CandidateAttempt: failure.Attempt,
            ExpectedManifestDigest: preview.ManifestDigest, ExpectedMapLayersDigest: preview.MapLayersDigest);
        var identityFailure = await Assert.ThrowsAsync<HttpRequestException>(() => api.CreateWorldAsync(uri,
            authority, device.DeviceId, wrongClientAttempt, signer, default));
        Assert.Equal("something changed on the world server. Try again", Client.GameUiText.FriendlyFailure(identityFailure));
        AssertAttemptSequence(0, 1, 2);
        Assert.Equal(oldCatalog, JsonSerializer.SerializeToUtf8Bytes(catalog.Capture(), WebJsonOptions));
        Assert.Equal(oldBytes, WorldRuntimeCodec.Encode(oldWorld.ExportState()));

        using var created = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/create", create, OwnerHttpBinding.WorldCreationPayload(create));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        AssertAttemptSequence(0, 1, 2);
        var state = host.Services.GetRequiredService<WorldRuntime>().ExportState();
        Assert.Equal(authorityIdentity, host.Services.GetRequiredService<OwnerAuthorityStore>().Identity);
        Assert.Equal(preview.ManifestDigest, state.Map.ManifestDigest);
        Assert.Equal(preview.MapLayersDigest, MapLayerManifestCodec.Digest(state.Map));
        Assert.Equal(0, state.Map.GenerationAttempt);
        Assert.Equal(0, state.Geography!.CandidateAttempt);
        Assert.False(state.Geography.LatitudeCooling);
        Assert.Equal(ClankerWorld.Simulation.World.GenerationAmount.High, state.Geography.MountainRelief);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
