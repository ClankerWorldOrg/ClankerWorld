using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Worlds;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    private readonly ITestOutputHelper output;

    public ServerHttpTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task BalancedPreviewRequiresAcceptanceForMissesAndCreateUsesItsExactCandidate()
    {
        using var host = new ServerWebApplicationFactory(null, gameWorld: true, legacyPace: false);
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await StartAndActivateAsync(host, client, key);
        // Mountains always meet their target now, so this seed's three
        // candidates all miss on forest instead.
        var action = new OwnerWorldCreationAction("Balanced", "issue-409-miss-133", "Small", 50, true);
        var payload = OwnerActionPayload.WorldCreation(action);
        var stopwatch = Stopwatch.StartNew();
        using var previewed = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/preview", action, payload);
        Assert.Equal(HttpStatusCode.OK, previewed.StatusCode);
        var responseBytes = await previewed.Content.ReadAsByteArrayAsync();
        var preview = JsonSerializer.Deserialize<OwnerWorldPreview>(responseBytes, WebJsonOptions)!;
        stopwatch.Stop();
        output.WriteLine($"small-balanced-owner-preview elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F0} " +
            $"response_bytes={responseBytes.Length} candidates={preview.Candidates.Count}");
        Assert.Equal(3, preview.Candidates.Count);
        var coverage = Assert.IsType<ClankerWorld.Contracts.OwnerWorldCandidateReport>(preview.Coverage);
        Assert.True(coverage.ForestTargetApplicable);
        Assert.True(coverage.MountainTargetApplicable);
        Assert.False(coverage.MeetsTargets);
        Assert.All(preview.Candidates, candidate => Assert.False(candidate.MeetsTargets));
        Assert.Equal(coverage.Attempt, Assert.Single(preview.Candidates,
            candidate => candidate.Attempt == coverage.Attempt).Attempt);

        var create = action with
        {
            CandidateAttempt = coverage.Attempt,
            ExpectedManifestDigest = preview.ManifestDigest,
            ExpectedMapLayersDigest = preview.MapLayersDigest,
        };
        using var unaccepted = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/create", create, OwnerActionPayload.WorldCreation(create));
        Assert.Equal(HttpStatusCode.Conflict, unaccepted.StatusCode);
        Assert.Single(host.Services.GetRequiredService<WorldCatalogStore>().Capture().Worlds);

        var stale = create with { ExpectedMapLayersDigest = "sha256:stale-preview", AcceptUnmetTargets = true };
        using var changedPreview = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/create", stale, OwnerActionPayload.WorldCreation(stale));
        Assert.Equal(HttpStatusCode.Conflict, changedPreview.StatusCode);

        var accepted = create with { AcceptUnmetTargets = true };
        using var created = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/create", accepted, OwnerActionPayload.WorldCreation(accepted));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var state = host.Services.GetRequiredService<WorldRuntime>().ExportState();
        Assert.Equal(preview.ManifestDigest, state.Map.ManifestDigest);
        Assert.Equal(preview.MapLayersDigest, MapLayerManifestCodec.Digest(state.Map));
        Assert.Equal(coverage.Attempt, state.Map.GenerationAttempt);
        Assert.Equal(coverage.Attempt, state.Geography!.CandidateAttempt);
    }

    [Fact]
    public async Task AdvancedPreviewAndCreationUseTheSameSignedSavedOptions()
    {
        using var host = new ServerWebApplicationFactory(null, gameWorld: true, legacyPace: false);
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await StartAndActivateAsync(host, client, key);
        var action = new OwnerWorldCreationAction("Advanced", "advanced-http", "Small", 50, false,
            "Dominant", "Temperate", false, "Abundant", "High", "Low", "High");
        var payload = OwnerActionPayload.WorldCreation(action);
        Assert.Equal(payload, ClankerWorld.GodotClient.Protocol.OwnerWorldActionPayload.WorldCreation(
            new ClankerWorld.Contracts.OwnerWorldCreationAction(action.Name, action.Seed, action.Size,
                action.WaterPercent, action.WrapEastWest, action.ClimateMode, action.SelectedClimate, action.LatitudeCooling,
                action.ResourceAbundance, action.ForestCover, action.MountainRelief, action.RiverAbundance)));
        var signed = await CreateSignedRequestAsync(host, client, key, device.DeviceId, "/api/v1/owner/worlds/preview", action, payload);
        using var tampered = await client.PostAsJsonAsync("/api/v1/owner/worlds/preview", signed with { Action = action with { ForestCover = "Low" } });
        Assert.False(tampered.IsSuccessStatusCode);
        foreach (var invalid in new[] { action with { ForestCover = "Unknown" }, action with { MountainRelief = "99" },
                     action with { RiverAbundance = "99" }, action with { WaterPercent = 19 }, action with { WaterPercent = 81 }, action with { Size = "Large" } })
        {
            using var refused = await SendSignedAsync(host, client, key, device.DeviceId, "/api/v1/owner/worlds/preview", invalid, OwnerActionPayload.WorldCreation(invalid));
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }
        using var previewed = await SendSignedAsync(host, client, key, device.DeviceId, "/api/v1/owner/worlds/preview", action, payload);
        Assert.Equal(HttpStatusCode.OK, previewed.StatusCode);
        var preview = (await previewed.Content.ReadFromJsonAsync<OwnerWorldPreview>())!;
        Assert.NotNull(preview.Coverage);
        Assert.Single(preview.Candidates); // Dominant climate is outside the trial target.
        var createAction = action with
        {
            CandidateAttempt = preview.Coverage!.Attempt,
            ExpectedManifestDigest = preview.ManifestDigest,
            ExpectedMapLayersDigest = preview.MapLayersDigest,
        };
        using var created = await SendSignedAsync(host, client, key, device.DeviceId,
            "/api/v1/owner/worlds/create", createAction, OwnerActionPayload.WorldCreation(createAction));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var state = host.Services.GetRequiredService<WorldRuntime>().ExportState();
        Assert.Equal(preview.ManifestDigest, state.Map.ManifestDigest);
        Assert.Equal(preview.MapLayersDigest, MapLayerManifestCodec.Digest(state.Map));
        Assert.Equal(GenerationAmount.High, state.Geography!.ForestCover);
        Assert.Equal(GenerationAmount.Low, state.Geography.MountainRelief);
        Assert.Equal(GenerationAmount.High, state.Geography.RiverAbundance);
        Assert.False(state.Geography.LatitudeCooling);
    }
}
