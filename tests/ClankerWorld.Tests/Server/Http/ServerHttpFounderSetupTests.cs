using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;
using ClankerWorld.AgentPlacement;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task StartWorldRefusesSpentModelCallLimitUntilMoreCallsAreAllowed()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-founder-limit-");
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var handler = new ControlledSetupCheckHandler((_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                """{"model":"test-model","choices":[{"message":{"content":"{\"selected_candidate_id\":\"safe_idle\",\"confidence\":0.8}"}}]}""")));
            using var baseHost = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: false);
            using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new SetupCheckHttpClientFactory(handler));
            }));
            using var client = host.CreateClient();
            client.BaseAddress = new Uri("http://127.0.0.1/");
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var slotId = Guid.NewGuid().ToString("N");
            var positions = new[] { new GridPoint(0, 0), new GridPoint(1, 2), new GridPoint(2, 2), new GridPoint(3, 2) };
            for (var index = 0; index < positions.Length; index++)
            {
                var id = "founder:" + Guid.NewGuid().ToString("N");
                var cognition = new OwnerProviderConfigurationAction("personal", "openai", "test-model",
                    index == 0 ? "test-founder-key" : null, false, id, slotId,
                    index == 0 ? "Test account" : null);
                var placement = new OwnerFounderPlacementAction(id, positions[index].X, positions[index].Y, cognition);
                using var placed = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/founders/place", placement, OwnerHttpBinding.FounderPlacementPayload(placement));
                Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
            }

            var usage = host.Services.GetRequiredService<ProviderUsageStore>();
            usage.Configure(new OwnerUsageLimitAction(1));
            var check = new OwnerProviderSetupCheckAction("openai", "test-model", slotId);
            using var checkedModel = await SendSignedAsync(host, client, key, device.DeviceId,
                SetupCheckPath, check, OwnerHttpBinding.ProviderSetupCheckPayload(check));
            Assert.Equal(HttpStatusCode.OK, checkedModel.StatusCode);
            Assert.Equal("ready", (await checkedModel.Content.ReadFromJsonAsync<OwnerProviderSetupCheckResult>())!.Outcome);
            Assert.Equal(1, handler.RequestCount);
            Assert.True(usage.Capture().LimitReached);

            var beforeStart = WorldRuntimeCodec.Encode(runtime.ExportState());
            var savePath = host.Services.GetRequiredService<WorldStateFile>().Path;
            var savedBeforeStart = File.ReadAllBytes(savePath);
            var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
            using var signer = new ActionCompatibilitySigner(key);
            var api = new OwnerWorldApi(client);
            var refused = await Assert.ThrowsAsync<OwnerWorldStartRefusedException>(() => api.StartWorldAsync(
                client.BaseAddress!, new(identity.ServerAuthorityId, identity.WorldId), device.DeviceId, runtime.Society.WorldId,
                signer, CancellationToken.None));
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("The model-call limit is reached. Raise the limit or allow more calls in Settings → Game, then select Start World.",
                GameUiFailures.FriendlyFailure(refused));
            Assert.False(runtime.FounderSetup!.Started);
            Assert.True(runtime.Society.IsPaused);
            Assert.Equal(beforeStart, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(savedBeforeStart, File.ReadAllBytes(savePath));

            usage.Configure(new OwnerUsageLimitAction(null, AdditionalCalls: 100));
            Assert.True(runtime.Society.IsPaused);
            var started = await api.StartWorldAsync(client.BaseAddress!, new(identity.ServerAuthorityId, identity.WorldId),
                device.DeviceId, runtime.Society.WorldId, signer, CancellationToken.None);
            Assert.True(started.Changed);
            Assert.True(runtime.FounderSetup!.Started);
            Assert.False(runtime.Society.IsPaused);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task NewWorldRequiresFourConfiguredFoundersAndAnExplicitSignedStart()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-founder-http-");
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var host = new ServerWebApplicationFactory(directory.FullName, null, gameWorld: true,
                legacyPace: false);
            using var client = host.CreateClient();
            var placementLog = new RecordingLogger<ServerHttpTests>();
            host.Services.GetRequiredService<ILoggerFactory>().AddProvider(new RecordingLoggerProvider<ServerHttpTests>(placementLog));
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            Assert.Empty(runtime.Agents);
            using var premature = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/control/resume", WorldControl(host, "resume"), OwnerActionPayload.WorldControl(WorldControl(host, "resume")));
            Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

            var positions = new[] { new GridPoint(0, 0), new GridPoint(1, 2), new GridPoint(2, 2), new GridPoint(3, 2) };
            var slotId = Guid.NewGuid().ToString("N");
            OwnerFounderMoveAction? firstFounderMove = null;
            for (var index = 0; index < positions.Length; index++)
            {
                var id = "founder:" + Guid.NewGuid().ToString("N");
                var cognition = new OwnerProviderConfigurationAction("personal", "openai", "gpt-5-mini",
                    index == 0 ? "test-secret-key" : null, false, id, slotId,
                    index == 0 ? "Test account" : null);
                var action = new OwnerFounderPlacementAction(id, positions[index].X, positions[index].Y, cognition);
                const string path = "/api/v1/owner/founders/place";
                if (index == 0)
                {
                    var envelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path, action,
                        OwnerHttpBinding.FounderPlacementPayload(action));
                    using var tampered = await client.PostAsJsonAsync(path, envelope with
                    {
                        Action = action with { X = positions[index].X + 1 },
                    });
                    Assert.False(tampered.IsSuccessStatusCode);
                    Assert.Empty(runtime.Agents);
                }
                using var placed = await SendSignedAsync(host, client, key, device.DeviceId, path, action,
                    OwnerHttpBinding.FounderPlacementPayload(action));
                Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
                var receipt = await placed.Content.ReadFromJsonAsync<OwnerFounderPlacementReceipt>();
                Assert.Equal(index + 1, receipt!.Placed);
                if (index == 0)
                {
                    var map = runtime.ExportState().Map;
                    var destination = map.Tiles.Select(tile => tile.Position).First(point =>
                        map.IsBuildable(point) && !positions.Contains(point) &&
                        !map.MapObjects.Any(item => item.Position == point) &&
                        !map.Resources.Any(item => item.Position == point));
                    firstFounderMove = new OwnerFounderMoveAction(id, destination.X, destination.Y);
                    const string movePath = "/api/v1/owner/founders/move";
                    var signedMove = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                        movePath, firstFounderMove, OwnerHttpBinding.FounderMovePayload(firstFounderMove));
                    using var tamperedMove = await client.PostAsJsonAsync(movePath, signedMove with
                    {
                        Action = firstFounderMove with { X = destination.X + 1 },
                    });
                    Assert.False(tamperedMove.IsSuccessStatusCode);
                    Assert.Equal(positions[0], runtime.Agents.Single(person => person.AgentId == id).Position);
                    using var moved = await SendSignedAsync(host, client, key, device.DeviceId,
                        movePath, firstFounderMove, OwnerHttpBinding.FounderMovePayload(firstFounderMove));
                    Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
                    Assert.True((await moved.Content.ReadFromJsonAsync<OwnerFounderMoveReceipt>())!.Changed);
                    Assert.Equal(destination, runtime.Agents.Single(person => person.AgentId == id).Position);
                    Assert.Contains(placementLog.Messages, message => message.Contains(
                        "founder_setup outcome=moved world_tick=0", StringComparison.Ordinal));
                }
            }

            Assert.True(runtime.Society.IsPaused);
            Assert.Equal(2, runtime.Society.Households.Single(item => item.Id == "household:second").MemberIds.Count);
            var lastFounder = runtime.FounderSetup!.FounderIds[^1];
            var undo = new OwnerFounderUndoAction(lastFounder);
            const string undoPath = "/api/v1/owner/founders/undo";
            var signedUndo = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                undoPath, undo, OwnerHttpBinding.FounderUndoPayload(undo));
            using var tamperedUndo = await client.PostAsJsonAsync(undoPath, signedUndo with
            {
                Action = undo with { FounderId = runtime.FounderSetup.FounderIds[0] },
            });
            Assert.False(tamperedUndo.IsSuccessStatusCode);
            Assert.Equal(4, runtime.FounderSetup.FounderIds.Count);
            using var undone = await SendSignedAsync(host, client, key, device.DeviceId,
                undoPath, undo, OwnerHttpBinding.FounderUndoPayload(undo));
            Assert.Equal(HttpStatusCode.OK, undone.StatusCode);
            Assert.Equal(3, (await undone.Content.ReadFromJsonAsync<OwnerFounderUndoReceipt>())!.Placed);
            Assert.DoesNotContain(lastFounder, runtime.FounderSetup.FounderIds);
            Assert.Equal(runtime.FounderSetup.FounderIds[^1], host.Services
                .GetRequiredService<OwnerWorldObservationStore>().GetSnapshot().FounderSetup!.LastFounderId);
            var providerStatus = host.Services.GetRequiredService<ProviderConfigurationStore>().CaptureStatus();
            var inheritedRoutes = providerStatus.Assignments!.Where(item => item.AgentId == lastFounder).ToArray();
            Assert.Equal(2, inheritedRoutes.Length);
            Assert.Equal([PlayerDecisionProviders.PlanningRole, PlayerDecisionProviders.RoutineRole],
                inheritedRoutes.Select(item => item.Role).Order(StringComparer.Ordinal));
            Assert.All(inheritedRoutes, assignment =>
            {
                Assert.Equal(PlayerDecisionProviders.Inherit, assignment.Provider);
                Assert.Null(assignment.Model);
                Assert.Null(assignment.CredentialSlotId);
                Assert.Null(assignment.SelectionReason);
            });
            Assert.Contains(providerStatus.CredentialSlots ?? [], item => item.Id == slotId);
            Assert.Contains(placementLog.Messages, message => message.Contains(
                "founder_setup outcome=undone world_tick=0", StringComparison.Ordinal));
            var replacementId = "founder:" + Guid.NewGuid().ToString("N");
            var replacement = new OwnerFounderPlacementAction(replacementId, positions[3].X, positions[3].Y,
                new OwnerProviderConfigurationAction("personal", "openai", "gpt-5-mini", null,
                    false, replacementId, slotId));
            using var replaced = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/founders/place", replacement,
                OwnerHttpBinding.FounderPlacementPayload(replacement));
            Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
            Assert.Equal(4, runtime.FounderSetup.FounderIds.Count);
            var start = WorldControl(host, "start-world");
            using var started = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/control/start-world", start, OwnerActionPayload.WorldControl(start));
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            Assert.False(runtime.Society.IsPaused);
            Assert.True(runtime.FounderSetup!.Started);
            using var lateMove = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/founders/move", firstFounderMove!,
                OwnerHttpBinding.FounderMovePayload(firstFounderMove!));
            Assert.Equal(HttpStatusCode.Conflict, lateMove.StatusCode);
            using var lateUndo = await SendSignedAsync(host, client, key, device.DeviceId,
                undoPath, new OwnerFounderUndoAction(replacementId),
                OwnerHttpBinding.FounderUndoPayload(new OwnerFounderUndoAction(replacementId)));
            Assert.Equal(HttpStatusCode.Conflict, lateUndo.StatusCode);
            var agentId = "agent:" + Guid.NewGuid().ToString("N");
            var observationStore = host.Services.GetRequiredService<OwnerWorldObservationStore>();
            var stateBeforePreview = WorldRuntimeCodec.Encode(runtime.ExportState());
            var preview = observationStore.GetSnapshot();
            Assert.Equal(stateBeforePreview, WorldRuntimeCodec.Encode(runtime.ExportState()));
            var placement = AgentPlacementRules.Resolve(
                preview.PlacedBuildings.Where(building => building.HouseholdId is not null &&
                    4 >= building.Position.X && 4 < building.Position.X + building.Width &&
                    2 >= building.Position.Y && 2 < building.Position.Y + building.Height)
                    .Select(building => building.HouseholdId),
                preview.Towns.Where(town => town.BorderTiles.Any(tile => tile.X == 4 && tile.Y == 2))
                    .Select(town => town.Id));
            Assert.False(placement.IsAmbiguous);
            var adultCognition = new OwnerProviderConfigurationAction("personal", "openai", "gpt-5-mini", null,
                false, agentId, slotId);
            var adult = new OwnerAgentPlacementAction(agentId, 4, 2, adultCognition,
                placement.HouseholdIdFor(agentId), placement.TownId);
            const string agentPath = "/api/v1/owner/agents/place";
            var signedAdult = await CreateSignedRequestAsync(host, client, key, device.DeviceId, agentPath,
                adult, OwnerHttpBinding.AgentPlacementPayload(adult));
            using var tamperedAdult = await client.PostAsJsonAsync(agentPath, signedAdult with
            {
                Action = adult with { ExpectedTownId = "town:stale" },
            });
            Assert.False(tamperedAdult.IsSuccessStatusCode);
            Assert.Equal(4, runtime.Agents.Count);

            var staleId = "agent:" + Guid.NewGuid().ToString("N");
            var stale = new OwnerAgentPlacementAction(staleId, adult.X, adult.Y,
                adultCognition with { AgentId = staleId }, null, "town:stale");
            using var changed = await SendSignedAsync(host, client, key, device.DeviceId, agentPath,
                stale, OwnerHttpBinding.AgentPlacementPayload(stale));
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            var changedFailure = await changed.Content.ReadFromJsonAsync<OwnerControlFailure>();
            Assert.Equal("placement_changed", changedFailure!.Code);
            Assert.Equal(4, runtime.Agents.Count);
            Assert.DoesNotContain(host.Services.GetRequiredService<ProviderConfigurationStore>().CaptureStatus()
                .Assignments ?? [], assignment => assignment.AgentId == staleId);

            using var added = await SendSignedAsync(host, client, key, device.DeviceId, agentPath,
                adult, OwnerHttpBinding.AgentPlacementPayload(adult));
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
            var addedReceipt = await added.Content.ReadFromJsonAsync<OwnerAgentPlacementReceipt>();
            Assert.Null(addedReceipt!.HouseholdId);
            Assert.Equal(placement.TownId, addedReceipt.TownId);
            Assert.Equal(5, runtime.Agents.Count);
            Assert.Null(runtime.Society.GetAgent(agentId).HouseholdId);
            Assert.Contains(agentId, runtime.Towns.Single().ResidentIds);
            var observedAdult = observationStore.GetSnapshot().Agents.Single(person => person.Id == agentId);
            Assert.Equal("active", observedAdult.Lifecycle);
            Assert.Equal("unhoused", observedAdult.DecisionFactors.Single(factor => factor.Key == "household").Detail);
            using var duplicate = await SendSignedAsync(host, client, key, device.DeviceId, agentPath,
                adult, OwnerHttpBinding.AgentPlacementPayload(adult));
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
            Assert.Contains(placementLog.Messages, message => message.Contains("AgentPlaced AgentId=" + agentId, StringComparison.Ordinal));
            var rename = new OwnerAgentRenameAction(agentId, "Nova");
            const string renamePath = "/api/v1/owner/agents/rename";
            var signedRename = await CreateSignedRequestAsync(host, client, key, device.DeviceId, renamePath,
                rename, OwnerActionPayload.AgentRename(rename));
            using var tamperedRename = await client.PostAsJsonAsync(renamePath, signedRename with
            {
                Action = rename with { Name = "Someone else" },
            });
            Assert.False(tamperedRename.IsSuccessStatusCode);
            Assert.Equal("New agent", runtime.Society.GetAgent(agentId).Name);
            using var renamed = await SendSignedAsync(host, client, key, device.DeviceId, renamePath,
                rename, OwnerActionPayload.AgentRename(rename));
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
            Assert.Equal("Nova", host.Services.GetRequiredService<OwnerWorldObservationStore>()
                .GetSnapshot().Agents.Single(person => person.Id == agentId).DisplayName);
            Assert.Contains(placementLog.Messages, message => message.Contains("AgentRenamed AgentId=" + agentId, StringComparison.Ordinal));
            Assert.DoesNotContain(placementLog.Messages, message => message.Contains("test-secret-key", StringComparison.Ordinal));
            Assert.DoesNotContain("test-secret-key", File.ReadAllText(host.Services.GetRequiredService<WorldStateFile>().Path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
