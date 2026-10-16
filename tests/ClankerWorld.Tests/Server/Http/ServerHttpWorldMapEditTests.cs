using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;
using ClientPayload = ClankerWorld.GodotClient.Protocol.OwnerWorldActionPayload;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task MapEditsAuthenticateEveryFieldOfferNativeChoicesAndPersistOnlyCompleteBatches()
    {
        var directory = Directory.CreateTempSubdirectory("map-edit-http-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var world = host.Services.GetRequiredService<WorldRuntime>();
            world.Pause();
            var points = WorldMapEditTests.FreeTiles(world);
            var command = WorldMapEditTests.Edit(world,
                WorldMapEditTests.Operation("set_terrain", "forest", points[0]),
                WorldMapEditTests.Operation("place_resource", "food", points[1]),
                WorldMapEditTests.Operation("place_object", "storage", points[2]));
            var timeline = world.ExportObservation().Timeline;
            var action = new OwnerAuthoringBatchAction(command.BatchId, command.Operations.Select(operation =>
                new OwnerAuthoringOperationAction(operation.Kind, operation.Id, operation.Value, operation.SecondaryValue,
                    operation.X, operation.Y, operation.IsRenewable)).ToArray(), command.WorldId, command.ExpectedEventId, command.RunEpoch, new(timeline.InstanceId, timeline.Generation));
            const string path = "/api/v1/owner/authoring";
            var before = WorldRuntimeCodec.Encode(world.ExportState());
            var file = host.Services.GetRequiredService<WorldStateFile>();
            file.Save(world);
            var saved = File.ReadAllBytes(file.Path);
            var first = action.Operations[0];
            foreach (var changed in new[]
            {
                action with { WorldId = "other" }, action with { ExpectedEventId = action.ExpectedEventId + 1 },
                action with { RunEpoch = action.RunEpoch + 1 }, action with { BatchId = "other" },
                action with { Timeline = action.Timeline! with { InstanceId = "other" } },
                action with { Timeline = action.Timeline! with { Generation = action.Timeline.Generation + 1 } },
                action with { Operations = action.Operations.Skip(1).ToArray() },
                action with { Operations = [first with { Kind = "place_object" }] },
                action with { Operations = [first with { Id = "other" }] },
                action with { Operations = [first with { Value = "meadow" }] },
                action with { Operations = [first with { SecondaryValue = "other" }] },
                action with { Operations = [first with { X = first.X + 1 }] },
                action with { Operations = [first with { Y = first.Y + 1 }] },
                action with { Operations = [first with { IsRenewable = true }] },
            })
            {
                var envelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path, action,
                    OwnerHttpBinding.AuthoringPayload(action));
                using var response = await client.PostAsJsonAsync(path, envelope with { Action = changed });
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
                Assert.Equal(saved, File.ReadAllBytes(file.Path));
            }
            foreach (var invalid in new[]
            {
                action with { WorldId = "other" }, action with { RunEpoch = action.RunEpoch + 1 },
                action with { Timeline = action.Timeline! with { Generation = action.Timeline.Generation + 1 } },
                action with { ExpectedEventId = action.ExpectedEventId - 1 },
                action with { Operations = [first, first with { X = -1 }] },
                new OwnerAuthoringBatchAction(action.BatchId, action.Operations), // Legacy unsigned-world shape cannot edit.
            })
            {
                using var response = await SendSignedAsync(host, client, key, device.DeviceId, path, invalid,
                    OwnerHttpBinding.AuthoringPayload(invalid));
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var refusal = await response.Content.ReadFromJsonAsync<OwnerControlFailure>();
                Assert.Equal("world_edit_refused", refusal!.Code);
                Assert.False(string.IsNullOrWhiteSpace(refusal.Detail));
                Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
                Assert.Equal(saved, File.ReadAllBytes(file.Path));
            }
            var payload = ClientPayload.Authoring(action);
            Assert.Equal(OwnerHttpBinding.AuthoringPayload(action), payload);
            var catalog = host.Services.GetRequiredService<OwnerWorldObservationStore>().GetSnapshot().Authoring!.EditTools;
            Assert.Equal(WorldMapEditRules.Choices.Select(choice => (choice.Operation, choice.Value, choice.DisplayName, choice.IsRenewable)),
                catalog.SelectMany(tool => tool.Kinds.Select(kind => (tool.Operation, kind.Value, kind.DisplayName, kind.IsRenewable))));
            using var accepted = await SendSignedAsync(host, client, key, device.DeviceId, path, action, payload);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var receipt = await accepted.Content.ReadFromJsonAsync<OwnerAuthoringBatchReceipt>();
            Assert.True(receipt!.Applied);
            var bytes = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            Assert.Equal(world.ExportState().Map.ManifestDigest, receipt.CurrentMapManifestDigest);
            using var retry = await SendSignedAsync(host, client, key, device.DeviceId, path, action, payload);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            Assert.True((await retry.Content.ReadFromJsonAsync<OwnerAuthoringBatchReceipt>())!.Applied);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Single(world.ExportState().Events, item => item.Payload is DeveloperEditEventPayload { Operation: "edit_world" });
            var prepared = action with
            {
                BatchId = "prepared-before-load",
                ExpectedEventId = world.ExportState().Events[^1].EventId,
                Operations = [first with { X = points[3].X, Y = points[3].Y }],
            };
            // Loading this exact checkpoint preserves world ID, epoch and event ID.
            // The observer generation still prevents an old in-flight edit from landing.
            world.LoadPausedCheckpoint(world.ExportState());
            using var afterLoad = await SendSignedAsync(host, client, key, device.DeviceId, path, prepared,
                OwnerHttpBinding.AuthoringPayload(prepared));
            Assert.Equal(HttpStatusCode.Conflict, afterLoad.StatusCode);
            Assert.Contains("loaded or changed", await afterLoad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        }
        finally { directory.Delete(true); }
    }
}
