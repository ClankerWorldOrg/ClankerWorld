using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Worlds;
using Microsoft.Extensions.DependencyInjection;
using ClientAction = ClankerWorld.GodotClient.Protocol.OwnerInstructionAction;
using ClientAuthority = ClankerWorld.GodotClient.Pairing.OwnerAuthorityIdentity;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task RetainedInstructionCannotCrossWorldsAndRecoversOriginalReceiptAfterReturning()
    {
        var directory = Directory.CreateTempSubdirectory("instruction-world-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            client.BaseAddress = new Uri("http://127.0.0.1/");
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
            runtime.Pause();
            var entryA = catalog.Active();
            var worldA = runtime.Society.WorldId;
            using var other = new WorldRuntime("instruction-world-B");
            other.Pause();
            var entryB = catalog.Add("Other", other.ExportState());
            Assert.NotEqual(worldA, other.Society.WorldId);
            Assert.Contains(other.Agents, person => person.AgentId == "founder-mira");

            // Persist exactly the client-owned payload, as if the accepted receipt was lost.
            var authority = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
            var binding = OwnerPendingSubmissionBinding.Create(new ClientAuthority(authority.WorldId, authority.ServerAuthorityId),
                device.DeviceId, device.PublicKeyFingerprint, client.BaseAddress!);
            var store = new OwnerPendingSubmissionStore(Path.Combine(directory.FullName, "pending.json"));
            var pending = OwnerPendingSubmission.ForInstruction(binding,
                new ClientAction("world-bound-retry", "founder-mira", "suggestion", "wait safely", worldA));
            Assert.True(store.TrySave(pending));
            var action = new OwnerInstructionAction("world-bound-retry", "founder-mira", "suggestion", "wait safely", worldA);
            const string path = "/api/v1/owner/instructions";
            using var accepted = await SendSignedAsync(host, client, key, device.DeviceId, path, action,
                OwnerHttpBinding.InstructionPayload(action));
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var beforeUnscoped = WorldRuntimeCodec.Encode(runtime.ExportState());
            var unscopedEnvelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId, path, action,
                OwnerHttpBinding.InstructionPayload(action));
            using var unscoped = await client.PostAsJsonAsync(path,
                unscopedEnvelope with { Action = action with { WorldId = null! } });
            Assert.Equal(HttpStatusCode.BadRequest, unscoped.StatusCode);
            Assert.Equal(beforeUnscoped, WorldRuntimeCodec.Encode(runtime.ExportState()));
            var originalReceipt = await accepted.Content.ReadFromJsonAsync<OwnerInstructionReceipt>();
            var switchToB = new OwnerManualSaveAction("select-world", entryB.Id);
            using var selected = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/worlds/select", switchToB, OwnerHttpBinding.ManualSavePayload(switchToB));
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
            var retained = Assert.IsType<OwnerPendingSubmission>(store.TryLoad(binding));
            var payload = retained.Instruction!.ToAction();
            var retry = new OwnerInstructionAction(payload.IdempotencyKey, payload.TargetAgentId,
                payload.Kind, payload.Text, payload.WorldId);
            Assert.False(retained.Instruction.CanRetryIn(runtime.Society.WorldId));
            var beforeB = WorldRuntimeCodec.Encode(runtime.ExportState());
            using var wrongWorld = await SendSignedAsync(host, client, key, device.DeviceId, path, retry,
                OwnerHttpBinding.InstructionPayload(retry));
            Assert.Equal(HttpStatusCode.Conflict, wrongWorld.StatusCode);
            Assert.Equal(beforeB, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.NotNull(store.TryLoad(binding));
            using var tampered = await SendSignedAsync(host, client, key, device.DeviceId, path,
                retry with { WorldId = runtime.Society.WorldId }, OwnerHttpBinding.InstructionPayload(retry));
            Assert.False(tampered.IsSuccessStatusCode);
            Assert.Equal(beforeB, WorldRuntimeCodec.Encode(runtime.ExportState()));

            var switchToA = new OwnerManualSaveAction("select-world", entryA.Id);
            using var returned = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/worlds/select", switchToA, OwnerHttpBinding.ManualSavePayload(switchToA));
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            Assert.True(retained.Instruction.CanRetryIn(runtime.Society.WorldId));
            using var confirmed = await SendSignedAsync(host, client, key, device.DeviceId, path, retry,
                OwnerHttpBinding.InstructionPayload(retry));
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.Equal(originalReceipt, await confirmed.Content.ReadFromJsonAsync<OwnerInstructionReceipt>());
            Assert.Single(runtime.ExportState().Instructions!);
            Assert.Empty(catalog.Read(entryB.Id).Instructions!);
            Assert.True(store.TryClear(retained));
            runtime.Validate();
        }
        finally { directory.Delete(recursive: true); }
    }
}
